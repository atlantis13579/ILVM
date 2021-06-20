using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using IFix.Core;
using IFix;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace UE4Jit
{
    class ExecutorVM
    {
        private VirtualMachine vm;
        public VirtualMachine GetVM()
        {
            return vm;
        }
        private Dictionary<string, int> methodToId = new Dictionary<string, int>();

        string ParseFuncName(string Name)
        {
            int Idx1 = Name.IndexOf(" ");
            if (Idx1 < 0)
            {
                return Name;
            }
            Idx1 += 1;

            int Idx2 = Name.IndexOf("(", Idx1);
            if (Idx2 < 0)
            {
                return Name.Substring(Idx1);
            }

            return Name.Substring(Idx1, Idx2 - Idx1);
        }

        string GenerateFuncName(string Name, MethodReference method)
        {
            string BaseName = ParseFuncName(Name);
            string FinalName = BaseName;
            int Idx = 0;
            while (methodToId.ContainsKey(FinalName))
            {
                FinalName = BaseName + Idx;
                Idx += 1;
            }
            return FinalName;
        }

        static MethodBase readMethod(CodeTranslator tranlater, MethodReference method, Type[] externTypes)
        {
            bool isGenericInstance = method.IsGenericInstance;
            BindingFlags flag = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.NonPublic | BindingFlags.Public;
            if (isGenericInstance)
            {
                Type declaringType = externTypes[tranlater.externTypeToId[method.DeclaringType]];
                string methodName = method.Name;
                var typeArgs = ((GenericInstanceMethod)method).GenericArguments;
                int genericArgCount = typeArgs.Count;
                Type[] genericArgs = new Type[genericArgCount];
                for (int j = 0; j < genericArgCount; j++)
                {
                    if (tranlater.isCompilerGenerated(typeArgs[j]))
                    {
                        typeArgs[j] = tranlater.itfBridgeType;
                    }
                    genericArgs[j] = externTypes[tranlater.externTypeToId[typeArgs[j]]];
                }
                int paramCount = method.Parameters.Count;
                object[] paramMatchInfo = new object[paramCount];
                for (int j = 0; j < paramCount; j++)
                {
                    var p = method.Parameters[j];
                    bool isGeneric = p.ParameterType.HasGenericArgumentFromMethod();
                    if (isGeneric)
                    {
                        if (p.ParameterType.IsGenericParameter)
                        {
                            paramMatchInfo[j] = p.ParameterType.Name;
                        }
                        else
                        {
                            paramMatchInfo[j] = p.ParameterType.GetAssemblyQualifiedName(method.DeclaringType, true);
                        }
                    }
                    else
                    {
                        if (p.ParameterType.IsGenericParameter)
                        {
                            paramMatchInfo[j] = externTypes[tranlater.externTypeToId[(p.ParameterType as GenericParameter)
                                .ResolveGenericArgument(method.DeclaringType)]];
                        }
                        else
                        {
                            paramMatchInfo[j] = externTypes[tranlater.externTypeToId[p.ParameterType]];
                        }
                    }

                }
                MethodInfo matchMethod = null;
                MethodInfo[] infos = declaringType.GetMethods(flag);
                for (int k = 0; k < infos.Length; k++)
                {
                    MethodInfo m = infos[k];
                    var paramInfos = m.GetParameters();

                    Type[] genericArgInfos = null;
                    if (m.IsGenericMethodDefinition)
                    {
                        genericArgInfos = m.GetGenericArguments();
                    }
                    bool paramMatch = paramInfos.Length == paramCount && m.Name == methodName;
                    if (paramMatch && genericArgCount > 0) // need a generic method
                    {
                        if (!m.IsGenericMethodDefinition || genericArgInfos.Length != genericArgCount)
                        {
                            paramMatch = false;
                        }
                    }
                    if (paramMatch)
                    {
                        for (int j = 0; j < paramCount; j++)
                        {
                            string strMatchInfo = paramMatchInfo[j] as string;
                            if (strMatchInfo != null)
                            {
                                if (!m.IsGenericMethodDefinition)
                                {
                                    paramMatch = false;
                                    break;
                                }
                                strMatchInfo = System.Text.RegularExpressions.Regex
                                    .Replace(strMatchInfo, @"!!\d+", l =>
                                        genericArgInfos[int.Parse(l.Value.Substring(2))].Name);
                                if (strMatchInfo != paramInfos[j].ParameterType.ToString())
                                {
                                    paramMatch = false;
                                    break;
                                }
                            }
                            else
                            {
                                if ((paramMatchInfo[j] as Type) != paramInfos[j].ParameterType)
                                {
                                    paramMatch = false;
                                    break;
                                }
                            }
                        }
                    }
                    if (paramMatch)
                    {
                        matchMethod = m;
                        break;
                    }
                }
                if (matchMethod == null)
                {
                    throw new Exception("can not load generic method [" + method.FullName + "] of " + declaringType);
                }
                return matchMethod.MakeGenericMethod(genericArgs);
            }
            else
            {
                Type declaringType = externTypes[tranlater.externTypeToId[method.DeclaringType]];
                string methodName = method.Name;
                int paramCount = method.Parameters.Count;
                Type[] paramTypes = new Type[paramCount];
                for (int j = 0; j < paramCount; j++)
                {
                    var p = method.Parameters[j];
                    var paramType = p.ParameterType;
                    if (paramType.IsGenericParameter)
                    {
                        paramType = (paramType as GenericParameter).ResolveGenericArgument(method.DeclaringType);
                    }
                    if (paramType.IsRequiredModifier)
                    {
                        paramType = (paramType as RequiredModifierType).ElementType;
                    }
                    if (!tranlater.externTypeToId.ContainsKey(paramType))
                    {
                        throw new Exception("externTypeToId do not exist key: " + paramType
                            + ", while process parameter of method: " + method);
                    }

                    paramTypes[j] = externTypes[tranlater.externTypeToId[paramType]];
                }

                bool isConstructor = methodName == ".ctor" || methodName == ".cctor";
                MethodBase externMethod = null;
                if (isConstructor)
                {
                    externMethod = declaringType.GetConstructor(BindingFlags.Public | (methodName == ".ctor" ?
                        BindingFlags.Instance : BindingFlags.Static) |
                        BindingFlags.NonPublic, null, paramTypes, null);
                }
                else
                {
                    foreach (var m in declaringType.GetMethods(flag))
                    {
                        if (m.Name == methodName && !m.IsGenericMethodDefinition
                            && m.GetParameters().Length == paramCount)
                        {
                            var methodParameterTypes = m.GetParameters().Select(p => p.ParameterType);
                            if (methodParameterTypes.SequenceEqual(paramTypes))
                            {
                                externMethod = m;
                                break;
                            }
                        }
                    }
                }
                if (externMethod == null)
                {
                    throw new Exception("can not load method [" + methodName + "] of " + declaringType);
                }
                return externMethod;
            }
        }

        static int[] readSlotInfo(CodeTranslator trans, TypeDefinition type, Dictionary<MethodInfo, int> itfMethodToId, Type[] externTypes, int maxId)
        {
            int interfaceCount = type.Interfaces.Count;

            if (interfaceCount == 0) return null;

            int[] slots = new int[maxId + 1];
            for (int j = 0; j < slots.Length; j++)
            {
                slots[j] = -1;
            }

            //VirtualMachine._Info(string.Format("-------{0}----------", interfaceCount));
            for (int i = 0; i < interfaceCount; i++)
            {
                var ii = type.Interfaces[i];
                var itf = trans.bridgeInterfaces.Find(t => t.AreEqualIgnoreAssemblyVersion(ii.InterfaceType));
                //Console.WriteLine(itf.ToString());
                var itfDef = itf.Resolve();

                int itfId = trans.externTypeToId[itf];
                Type tf = externTypes[itfId];
                //VirtualMachine._Info(itf.ToString());

                int[] methodIds = new int[itfDef.Methods.Count];
                for ( int j = 0; j < itfDef.Methods.Count; ++j)
                {
                    var method = itfDef.Methods[j];
                    var itfMethod = itf.IsGenericInstance ? method.MakeGeneric(itf) : method.TryImport(itf.Module);
                    var implMethod = type.Methods.SingleOrDefault(m => itfMethod.CheckImplemention(m));
                    if (implMethod == null)
                    {
                        //Console.WriteLine(string.Format("check {0} in {1}", itfMethod, type));
                        //foreach(var cm in type.Methods)
                        //{
                        //    Console.WriteLine(string.Format("{0} {1}", cm, itfMethod.CheckImplemention(cm)));
                        //}
                        throw new Exception(string.Format("can not find method {0} of {1}", itfMethod, itf));
                    }
                    //Console.WriteLine(string.Format(">>>{0} [{1}]", itfMethod, methodToId[implMethod]));
                    methodIds[j] = trans.methodToId[implMethod];
                }

                MethodInfo[] methods = tf.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);

                System.Diagnostics.Debug.Assert(methods.Length == methodIds.Length);

                for (int j = 0; j < methods.Length; ++j)
                {
                    int methodId = methodIds[j];
                    MethodInfo method = methods[j];
                    if (!itfMethodToId.ContainsKey(method))
                    {
                        continue;
                        throw new Exception("can not find slot for " + method + " of " + itf);
                    }
                    slots[itfMethodToId[method]] = methodId;
                    //VirtualMachine._Info(string.Format("<<< {0} [{1}]", method, methodId));
                }
            }
            return slots;
        }

        public unsafe bool Load(string dllName)
        {
            if (!File.Exists(dllName))
            {
                return false;
            }

#if DEBUG
            string core_path = Directory.GetCurrentDirectory() + "/../../bin/Debug/JitDemo.exe";
#else
            string core_path = Directory.GetCurrentDirectory() + "/../../bin/Release/JitDemo.exe";
#endif

            AssemblyDefinition assembly_core = AssemblyDefinition.ReadAssembly(core_path, new ReaderParameters { ReadSymbols = true });

            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dllName, new ReaderParameters { ReadSymbols = true });
            if (assembly == null)
            {
                Console.WriteLine($"Inject Load assembly failed: {dllName}");
                return false;
            }

            CodeTranslator trans = new CodeTranslator();
            if (!trans.ProcessEx(assembly, assembly_core))
            {
                Console.WriteLine(dllName + " process yet!");
                return false;
            }

            foreach (var item in trans.methodToId)
            {
                string Name = GenerateFuncName(item.Key.FullName, item.Key);
                this.methodToId.Add(Name, item.Value);
            }

            Type[] externTypes;
            MethodBase[] externMethods;
            List<IFix.Core.ExceptionHandler[]> exceptionHandlers = new List<IFix.Core.ExceptionHandler[]>();
            Dictionary<int, NewFieldInfo> newFieldInfo = new Dictionary<int, NewFieldInfo>();
            string[] internStrings;
            FieldInfo[] fieldInfos;
            AnonymousStoreyInfo[] anonymousStoreyInfos;
            Type[] staticFieldTypes;
            int[] cctors;

            List<IntPtr> nativePointers = new List<IntPtr>();
            IntPtr nativePointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(IFix.Core.Instruction*) * trans.codes.Count);

            IFix.Core.Instruction** unmanagedCodes = (IFix.Core.Instruction**)nativePointer.ToPointer();
            nativePointers.Add(nativePointer);

            int externTypeCount = trans.externTypes.Count;
            externTypes = new Type[externTypeCount];
            for (int i = 0; i < externTypeCount; i++)
            {
                string assemblyQualifiedName = trans.externTypes[i].GetAssemblyQualifiedName(trans.contextTypeOfExternType[i]);
                externTypes[i] = Type.GetType(assemblyQualifiedName);
                if (externTypes[i] == null)
                {
                    throw new Exception("can not load type [" + assemblyQualifiedName + "]");
                }
            }

            externMethods = new MethodBase[trans.externMethods.Count];
            for (int i = 0; i < externMethods.Length; i++)
            {
                externMethods[i] = readMethod(trans, trans.externMethods[i], externTypes);
                if (externMethods[i] == null)
                {
                    throw new Exception("can not load extern method [" + trans.externMethods[i].FullName + "] ");
                }
            }

            foreach (var item in trans.codes)
            {
                int i = item.Key;
                List<IFix.Core.Instruction> Instructions = item.Value;
                nativePointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(
                    sizeof(IFix.Core.Instruction) * Instructions.Count);
                unmanagedCodes[i] = (IFix.Core.Instruction*)nativePointer.ToPointer();
                for (int j = 0; j < Instructions.Count; j++)
                {
                    unmanagedCodes[i][j] = Instructions[j];
                }
                nativePointers.Add(nativePointer);

                IFix.Core.ExceptionHandler[] ehsOfMethod = trans.methodIdToExceptionHandler[i];
                for (int k = 0; k < ehsOfMethod.Length; k++)
                {
                    IFix.Core.ExceptionHandler ehOfMethod = ehsOfMethod[k];
                    if (ehOfMethod.HandlerType == IFix.Core.ExceptionHandlerType.Catch)
                    {
                        ehOfMethod.CatchType = ehOfMethod.CatchTypeId == -1 ?
                            typeof(object) : externTypes[ehOfMethod.CatchTypeId];
                    }
                }
                exceptionHandlers.Add(ehsOfMethod);
            }

            fieldInfos = new FieldInfo[trans.fields.Count];
            for (int i = 0; i < fieldInfos.Length; i++)
            {
                int Idx = trans.addExternType(trans.fields[i].DeclaringType);
                var declaringType = externTypes[Idx];
                var fieldName = trans.fields[i].Name;

                fieldInfos[i] = declaringType.GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

                if (fieldInfos[i] == null)
                {
                    throw new Exception("can not load field [" + fieldName + "] " + " of " + declaringType);
                }
            }

            internStrings = new string[trans.internStrings.Count];
            for (int i = 0; i < internStrings.Length; ++i)
            {
                internStrings[i] = trans.internStrings[i];
            }

            staticFieldTypes = new Type[trans.fieldsStoreInVirtualMachine.Count];
            cctors = new int[staticFieldTypes.Length];
            for (int i = 0; i < staticFieldTypes.Length; i++)
            {
                var fieldType = trans.fieldsStoreInVirtualMachine[i].FieldType;
                if (trans.isCompilerGenerated(fieldType) || trans.isNewClass(fieldType as TypeDefinition))
                {
                    fieldType = trans.objType;
                }
                staticFieldTypes[i] = externTypes[trans.addExternType(fieldType)];
                cctors[i] = trans.typeToCctor[trans.fieldsStoreInVirtualMachine[i].DeclaringType];
            }

            Dictionary<MethodInfo, int> itfMethodToId = new Dictionary<MethodInfo, int>();
            int maxId = 0;

            var interfaceBridgeTypeName = trans.itfBridgeType.GetAssemblyQualifiedName();
            var interfaceBridgeType = Type.GetType(interfaceBridgeTypeName);
            if (interfaceBridgeType == null)
            {
                // TODO(fishezahang)
                // throw new Exception("assembly may be not injected yet, cat find interfaceBridgeType");
            }
            else
            {
                var interfaces = interfaceBridgeType.GetInterfaces();
                foreach (var itf in interfaces)
                {
                    InterfaceMapping map = interfaceBridgeType.GetInterfaceMap(itf);
                    for (int i = 0; i < map.InterfaceMethods.Length; i++)
                    {
                        IDTagAttribute idTag = Attribute.GetCustomAttribute(map.TargetMethods[i],
                            typeof(IDTagAttribute), false) as IDTagAttribute;
                        MethodInfo im = map.InterfaceMethods[i];
                        if (idTag == null)
                        {
                            throw new Exception(string.Format("can not find id for {0}", im));
                        }
                        int id = idTag.ID;
                        //VirtualMachine._Info(string.Format("{0} [{1}]", im, id));
                        maxId = id > maxId ? id : maxId;
                        itfMethodToId.Add(im, id);
                    }
                }
            }

            anonymousStoreyInfos = new AnonymousStoreyInfo[trans.anonymousTypeInfos.Count];
            for (int i = 0; i < anonymousStoreyInfos.Length; i++)
            {
                var anonymousType = trans.anonymousTypeInfos[i].DeclaringType as TypeDefinition;
                List<FieldDefinition> anonymousTypeFields = new List<FieldDefinition>();
                if (trans.isNewClass(trans.anonymousTypeInfos[i].DeclaringType as TypeDefinition))
                {
                    var temp = anonymousType;
                    while (temp != null && trans.isNewClass(temp as TypeDefinition))
                    {
                        if (temp.Fields != null)
                        {
                            foreach (var fi in temp.Fields)
                            {
                                anonymousTypeFields.Add(fi);
                            }
                        }
                        temp = temp.BaseType as TypeDefinition;
                    }
                }
                else
                {
                    anonymousTypeFields.AddRange(trans.anonymousTypeInfos[i].DeclaringType.Fields);
                }
                int fieldNum = anonymousTypeFields.Count;
                int[] fieldTypes = new int[fieldNum];
                for (int fieldIdx = 0; fieldIdx < fieldNum; ++fieldIdx)
                {
                    int fieldType = 0;
                    if (anonymousTypeFields[fieldIdx].FieldType.IsPrimitive)
                    {
                        fieldType = 0;
                    }
                    else if (anonymousTypeFields[fieldIdx].FieldType.IsValueType)
                    {
                        fieldType = trans.externTypeToId[anonymousTypeFields[fieldIdx].FieldType] + 1;
                    }
                    else
                    {
                        fieldType = -2;
                    }

                    fieldTypes[fieldIdx] = fieldType;
                }
                
                int ctorId = trans.methodToId[trans.anonymousTypeInfos[i]];
                int ctorParamNum = trans.anonymousTypeInfos[i].Parameters.Count;

                var slots = readSlotInfo(trans, anonymousType, itfMethodToId, externTypes, maxId);

                List<MethodDefinition> vT = trans.getVirtualMethodForType(anonymousType);
                int virtualMethodNum = vT.Count;
                int[] vTable = new int[virtualMethodNum];
                for (int vm = 0; vm < virtualMethodNum; vm++)
                {
                    vTable[vm] = -1;
                }
                trans.writeVTable(anonymousType, vTable, vT);

                anonymousStoreyInfos[i] = new AnonymousStoreyInfo()
                {
                    CtorId = ctorId,
                    FieldNum = fieldNum,
                    FieldTypes = fieldTypes,
                    CtorParamNum = ctorParamNum,
                    Slots = slots,
                    VTable = vTable
                };
            }

            vm = new VirtualMachine(unmanagedCodes, () =>
            { 
                for (int i = 0; i < nativePointers.Count; i++)
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(nativePointers[i]);
                }
            })
            {
                ExternTypes = externTypes,
                ExternMethods = externMethods,
                ExceptionHandlers = exceptionHandlers.ToArray(),
                InternStrings = internStrings,
                FieldInfos = fieldInfos,
                NewFieldInfos = newFieldInfo,
                AnonymousStoreyInfos = anonymousStoreyInfos,
                StaticFieldTypes = staticFieldTypes,
                Cctors = cctors
            };

            // string wrappersManagerImplName = trans.wrapperMgrImpl.GetAssemblyQualifiedName();
            // var wrapperManagerType = Type.GetType(wrappersManagerImplName, true);
            // WrappersManager wrapperManager = Activator.CreateInstance(wrapperManagerType, vm) as WrappersManager;
            // vm.WrappersManager = wrapperManager;

            if (vm == null)
            {
                return false;
            }

            return true;
        }

        private Call _Execute(int MethodIdx, object[] args)
        {
            Call call = Call.Begin();
            int argc = 0;
            if (args != null)
            {
                for (int i = 0; i < args.Length; ++i)
                {
                    object obj = args[i];
                    if (obj.GetType() == typeof(int))
                    {
                        call.PushInt32((int)obj);
                    }
                    else if (obj.GetType() == typeof(float))
                    {
                        call.PushSingle((int)obj);
                    }
                }
                argc = args.Length;
            }

            vm.Execute(MethodIdx, ref call, argc);
            Call.End(ref call);
            return call;
        }

        public void Execute(string Method, object[] args = null)
        {
            if (!this.methodToId.ContainsKey(Method))
            {
                Console.WriteLine($"Method not found: {Method}");
                return;
            }
            int Idx = this.methodToId[Method];
            try
            {
                Call call = _Execute(Idx, args);
            }
            catch 
            {
                Console.WriteLine($"Execute fail {Method}");
            }
        }

        public T Execute<T>(string Method, object[] args = null) where T : unmanaged
        {
            if (!this.methodToId.ContainsKey(Method))
            {
                Console.WriteLine($"Method not found: {Method}");
                return default(T);
            }
            int Idx = this.methodToId[Method];
            try
            {
                Call call = _Execute(Idx, args);
                return call.GetAsTypeEx<T>();
            }
            catch 
            {
                Console.WriteLine($"Execute fail {Method}");
                return default(T);
            }
        }
    }
}
