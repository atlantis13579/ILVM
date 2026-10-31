using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using IFix.Core;
using IFix;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace UE5Jit
{
    public class ExecutorVM : IDisposable
    {
        private VirtualMachine vm;
        private AssemblyLoadContext loadContext;
        private readonly object sync = new object();
        private Dictionary<int, MethodInfo> methods = new Dictionary<int, MethodInfo>();
        private HashSet<string> ambiguousNames = new HashSet<string>();
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

        public bool Load(string dllName)
        {
            lock (sync)
            {
                var candidate = new ExecutorVM();
                try
                {
                    candidate.LoadCore(Path.GetFullPath(dllName));
                    Dispose();
                    vm = candidate.vm;
                    loadContext = candidate.loadContext;
                    methodToId = candidate.methodToId;
                    methods = candidate.methods;
                    ambiguousNames = candidate.ambiguousNames;
                    return true;
                }
                catch
                {
                    candidate.Dispose();
                    throw;
                }
            }
        }

        private unsafe void LoadCore(string dllName)
        {
            if (!File.Exists(dllName))
            {
                throw new FileNotFoundException("Hotfix assembly was not found.", dllName);
            }
            using var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(dllName));
            resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(VirtualMachine).Assembly.Location));
            resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
            var reader = new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = false };
            using var assembly_core = AssemblyDefinition.ReadAssembly(typeof(VirtualMachine).Assembly.Location, reader);
            using var assembly = AssemblyDefinition.ReadAssembly(dllName, reader);

            CodeTranslator trans = new CodeTranslator();
            if (!trans.ProcessEx(assembly, assembly_core))
            {
                throw new InvalidOperationException("Failed to translate " + dllName);
            }

            loadContext = new HotfixLoadContext(dllName);
            Assembly loadedAssembly;
            using (var stream = new MemoryStream())
            {
                assembly.Write(stream);
                stream.Position = 0;
                loadedAssembly = loadContext.LoadFromStream(stream);
            }

            foreach (var item in trans.methodToId)
            {
                methodToId[item.Key.FullName] = item.Value;
                string name = ParseFuncName(item.Key.FullName);
                if (methodToId.ContainsKey(name)) ambiguousNames.Add(name);
                else methodToId.Add(name, item.Value);
                var declaringType = loadedAssembly.GetType(item.Key.DeclaringType.FullName.Replace('/', '+'));
                if (declaringType != null)
                {
                    var method = declaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .FirstOrDefault(m => m.MetadataToken == item.Key.MetadataToken.ToInt32());
                    if (method != null) methods[item.Value] = method;
                }
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
            try
            {
                IntPtr nativePointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(IFix.Core.Instruction*) * trans.codes.Count);

                IFix.Core.Instruction** unmanagedCodes = (IFix.Core.Instruction**)nativePointer.ToPointer();
                nativePointers.Add(nativePointer);

                int externTypeCount = trans.externTypes.Count;
                externTypes = new Type[externTypeCount];
                for (int i = 0; i < externTypeCount; i++)
                {
                    string assemblyQualifiedName = trans.externTypes[i].GetAssemblyQualifiedName(trans.contextTypeOfExternType[i]);
                    externTypes[i] = ResolveType(assemblyQualifiedName);
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

                foreach (var item in trans.codes.OrderBy(pair => pair.Key))
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
                var interfaceBridgeType = ResolveType(interfaceBridgeTypeName);
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

                var wrapperManagerType = ResolveType(trans.wrapperMgrImpl.GetAssemblyQualifiedName());
                vm.WrappersManager = (WrappersManager)Activator.CreateInstance(wrapperManagerType, vm);
                var wrappers = (System.Array)vm.WrappersManager.InitWrapperArray(trans.codes.Count);
                for (int id = 0; id < wrappers.Length; id++)
                    wrappers.SetValue(vm.WrappersManager.CreateWrapper(id), id);
            }
            catch
            {
                if (vm != null) vm.Dispose();
                else foreach (var pointer in nativePointers) System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);
                vm = null;
                throw;
            }
        }

        private Type ResolveType(string name)
        {
            return Type.GetType(name, assemblyName => loadContext.LoadFromAssemblyName(assemblyName), null, true);
        }

        public string[] MethodNames
        {
            get { lock (sync) return methodToId.Where(k => k.Key.Contains("(") && methods.ContainsKey(k.Value)).Select(k => k.Key).OrderBy(k => k).ToArray(); }
        }

        public object Invoke(string methodName, params object[] args)
        {
            lock (sync)
            {
                if (vm == null) throw new InvalidOperationException("No hotfix assembly is loaded.");
                if (ambiguousNames.Contains(methodName))
                    throw new AmbiguousMatchException("Use the full IL signature for " + methodName);
                if (!methodToId.TryGetValue(methodName, out int id) || !methods.TryGetValue(id, out var method))
                    throw new MissingMethodException(methodName);
                return InvokeCore(id, method, args ?? System.Array.Empty<object>());
            }
        }

        private unsafe object InvokeCore(int id, MethodInfo method, object[] args)
        {
            var parameters = method.GetParameters();
            int instanceCount = method.IsStatic ? 0 : 1;
            if (args.Length != parameters.Length + instanceCount)
                throw new TargetParameterCountException(method.ToString());
            Call call = Call.Begin();
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    Type type = i < instanceCount ? method.DeclaringType : parameters[i - instanceCount].ParameterType;
                    if (type.IsByRef || type.IsPointer)
                        throw new NotSupportedException("The public entry point does not accept ref/out/pointer arguments: " + method);
                    if ((args[i] == null && type.IsValueType && Nullable.GetUnderlyingType(type) == null) ||
                        (args[i] != null && !type.IsInstanceOfType(args[i])))
                        throw new ArgumentException("Argument " + i + " must have type " + type);
                    if (i < instanceCount && args[i] == null) throw new ArgumentNullException("instance");
                    EvaluationStackOperation.PushObject(call.evaluationStackBase, call.currentTop,
                        call.managedStack, args[i], type);
                    call.currentTop++;
                }
                vm.Execute(id, ref call, args.Length);
                return method.ReturnType == typeof(void) ? null : EvaluationStackOperation.ToObject(
                    call.evaluationStackBase, call.argumentBase, call.managedStack, method.ReturnType, vm);
            }
            finally
            {
                // Restore the entry frame even when validation or execution throws.
                int start = (int)(call.argumentBase - call.evaluationStackBase);
                System.Array.Clear(call.managedStack, start, call.managedStack.Length - start);
                *call.topWriteBack = call.argumentBase;
            }
        }

        public void Execute(string method, object[] args = null)
        {
            Invoke(method, args);
        }

        public T Execute<T>(string method, object[] args = null)
        {
            return (T)Invoke(method, args);
        }

        public void Dispose()
        {
            lock (sync)
            {
                vm?.Dispose();
                vm = null;
                methods.Clear();
                methodToId.Clear();
                ambiguousNames.Clear();
                loadContext?.Unload();
                loadContext = null;
            }
        }

        private sealed class HotfixLoadContext : AssemblyLoadContext
        {
            private readonly string directory;

            public HotfixLoadContext(string assemblyPath) : base(isCollectible: true)
            {
                directory = Path.GetDirectoryName(assemblyPath);
            }

            protected override Assembly Load(AssemblyName name)
            {
                if (name.Name == typeof(VirtualMachine).Assembly.GetName().Name)
                    return typeof(VirtualMachine).Assembly;
                string path = Path.Combine(directory, name.Name + ".dll");
                return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
            }
        }
    }
}
