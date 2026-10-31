/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */

using Mono.Cecil;
using System.Reflection;
using System.Text;
using System;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;

namespace IFix
{
    static internal class CecilExtensions
    {
        /// <summary>
        /// Resolve a generic parameter using contextType.
        /// </summary>
        /// <param name="gp">The generic parameter.</param>
        /// <param name="contextType">The containing type.</param>
        /// <returns></returns>
        public static TypeReference ResolveGenericArgument(this GenericParameter gp, TypeReference contextType)
        {
            if (contextType.IsGenericInstance)
            {
                var genericIns = ((GenericInstanceType)contextType);
                var genericTypeRef = genericIns.ElementType;
                var genericTypeDef = genericTypeRef.Resolve();
                for (int i = 0; i < genericTypeRef.GenericParameters.Count; i++)
                {
                    if (genericTypeRef.GenericParameters[i] == gp)
                    {
                        return genericIns.GenericArguments[i];
                    }
                    if (genericTypeDef != null && genericTypeDef.GenericParameters[i] == gp)
                    {
                        return genericIns.GenericArguments[i];
                    }
                }
            }

            if (contextType.IsNested)
            {
                return gp.ResolveGenericArgument(contextType.DeclaringType);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Resolve a generic parameter using contextMethod.
        /// </summary>
        /// <param name="gp">The generic parameter.</param>
        /// <param name="contextMethod">The containing method.</param>
        /// <returns></returns>
        public static TypeReference ResolveGenericArgument(this GenericParameter gp, MethodReference contextMethod)
        {
            if (contextMethod.IsGenericInstance)
            {
                var genericIns = contextMethod as GenericInstanceMethod;
                return genericIns.GenericArguments[gp.Position];
            }
            return null;
        }

        /// <summary>
        /// Substitute generic arguments; TypeReference.FullName alone leaves parameters such as T uninstantiated,
        /// which prevents a patch from finding the correct generic method.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="contextMethod"></param>
        /// <param name="contextType"></param>
        /// <returns></returns>
        public static TypeReference FillGenericArgument(this TypeReference type, MethodReference contextMethod,
            TypeReference contextType)
        {
            if (type.IsGenericParameter)
            {
                var gp = type as GenericParameter;
                if (gp.Type == GenericParameterType.Type)
                {
                    return gp.ResolveGenericArgument(contextType);
                }
                else
                {
                    return gp.ResolveGenericArgument(contextMethod);
                }
            }
            else if (type.IsGenericInstance && type.IsGeneric())
            {
                var genericIns = ((GenericInstanceType)type);
                var module = contextType == null ? contextMethod.Module : contextType.Module;
                var newGenericIns = new GenericInstanceType(TryImport(type.Resolve(), module));
                foreach (var arg in genericIns.GenericArguments)
                {
                    newGenericIns.GenericArguments.Add(arg.FillGenericArgument(contextMethod, contextType));
                }
                return newGenericIns;
            }
            else
            {
                return type;
            }
        }

        static void getFullNameWithoutGenericParameter(TypeReference typeReference, StringBuilder sb)
        {
            if (typeReference.IsNested)
            {
                getFullNameWithoutGenericParameter(typeReference.DeclaringType, sb);
                sb.Append("+");
                sb.Append(typeReference.Name);
            }
            else
            {
                if (!string.IsNullOrEmpty(typeReference.Namespace))
                {
                    sb.Append(typeReference.Namespace);
                    sb.Append(".");
                }
                sb.Append(typeReference.Name);
            }
        }

        static string getAssemblyFullName(TypeReference typeReference)
        {
            return (typeReference.Scope is AssemblyNameReference) ? (typeReference.Scope as AssemblyNameReference)
                .FullName : typeReference.Module.Assembly.FullName;
        }

        static string getAssemblyName(TypeReference typeReference)
        {
            return (typeReference.Scope is AssemblyNameReference) ? (typeReference.Scope as AssemblyNameReference)
                .Name : typeReference.Module.Assembly.Name.Name;
        }

        /// <summary>
        /// Compare types while ignoring assembly versions.
        /// </summary>
        /// <param name="left">The first argument.</param>
        /// <param name="right">The second argument.</param>
        /// <returns></returns>
        public static bool AreEqualIgnoreAssemblyVersion(this TypeReference left, TypeReference right)
        {
            return left.FullName == right.FullName && getAssemblyName(left) == getAssemblyName(right);
        }

        static TypeReference getElementType(TypeReference type, TypeReference contextType)
        {
            if (type.IsByReference)
            {
                return getElementType((type as ByReferenceType).ElementType, contextType);
            }
            if (type.IsArray)
            {
                return getElementType((type as ArrayType).ElementType, contextType);
            }
            if (type.IsGenericParameter)
            {
                return (type as GenericParameter).ResolveGenericArgument(contextType);
            }
            else
            {
                return type;
            }
        }

        /// <summary>
        /// Get a type's assembly-qualified name.
        /// </summary>
        /// <param name="typeReference">The type reference.</param>
        /// <param name="contextType">The containing type, typically an outer class.</param>
        /// <param name="skipAssemblyQualified">Omit the assembly name.</param>
        /// <param name="skipAssemblyQualifiedOnce">Omit the assembly name during one recursive generic-type expansion; add it after substituting arguments.</param>
        /// <returns></returns>
        public static string GetAssemblyQualifiedName(this TypeReference typeReference,
            TypeReference contextType = null, bool skipAssemblyQualified = false,
            bool skipAssemblyQualifiedOnce = false)
        {
            if (typeReference.IsGenericParameter)
            {
                if ((typeReference as GenericParameter).Type == GenericParameterType.Method)
                {
                    return typeReference.Name;
                }
                else
                {
                    if (contextType == null)
                    {
                        throw new System.ArgumentException("no context type for " + typeReference);
                    }
                    return (typeReference as GenericParameter).ResolveGenericArgument(contextType)
                        .GetAssemblyQualifiedName(contextType, skipAssemblyQualified, skipAssemblyQualifiedOnce);
                }
            }

            TypeReference assemblyType = getElementType(typeReference, contextType);
            if (assemblyType == null)
            {
                assemblyType = typeReference;
            }

            StringBuilder sb = new StringBuilder();
            if (typeReference.IsArray)
            {
                var arrayType = typeReference as ArrayType;
                sb.Append(arrayType.ElementType.GetAssemblyQualifiedName(contextType, skipAssemblyQualified, true));
                sb.Append('[');
                sb.Append(',', arrayType.Rank - 1);
                sb.Append(']');
            }
            else if (typeReference.IsByReference)
            {
                var refType = typeReference as ByReferenceType;
                sb.Append(refType.ElementType.GetAssemblyQualifiedName(contextType, skipAssemblyQualified, true));
                sb.Append('&');
            }
            else
            {
                getFullNameWithoutGenericParameter(typeReference, sb);

                if (typeReference.IsGenericInstance)
                {
                    bool isFirst = true;
                    var genericInstance = ((GenericInstanceType)typeReference);
                    sb.Append("[");
                    for (int i = 0; i < genericInstance.GenericArguments.Count; i++)
                    {
                        var genericArg = genericInstance.GenericArguments[i];
                        if (!isFirst)
                        {
                            sb.Append(",");
                        }
                        else
                        {
                            isFirst = false;
                        }
                        var strGenericArg = genericArg.GetAssemblyQualifiedName(contextType, skipAssemblyQualified);

                        if (skipAssemblyQualified || (genericArg.IsGenericParameter
                            && (genericArg as GenericParameter).Type == GenericParameterType.Method))
                        {
                            sb.Append(strGenericArg);
                        }
                        else
                        {
                            sb.Append(string.Format("[{0}]", strGenericArg));
                        }
                    }
                    sb.Append("]");
                }
            }
            return (skipAssemblyQualified | skipAssemblyQualifiedOnce) ?
                sb.ToString() : Assembly.CreateQualifiedName(getAssemblyFullName(assemblyType), sb.ToString());
        }

        /// <summary>
        /// Check whether a type is a delegate.
        /// </summary>
        /// <param name="typeDefinition">The type to check.</param>
        /// <returns></returns>
        public static bool IsDelegate(this TypeDefinition typeDefinition)
        {
            if (typeDefinition.BaseType == null)
            {
                return false;
            }
            return typeDefinition.BaseType.FullName == "System.MulticastDelegate";
        }

        /// <summary>
        /// Check whether a type is generic.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns></returns>
        public static bool IsGeneric(this TypeReference type)
        {
            if (type.HasGenericParameters || type.IsGenericParameter)
            {
                return true;
            }
            if (type.IsByReference)
            {
                return ((ByReferenceType)type).ElementType.IsGeneric();
            }
            if (type.IsArray)
            {
                return ((ArrayType)type).ElementType.IsGeneric();
            }
            if (type.IsGenericInstance)
            {
                foreach (var typeArg in ((GenericInstanceType)type).GenericArguments)
                {
                    if (typeArg.IsGeneric())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check whether a type contains a method-level generic argument.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns></returns>
        public static bool HasGenericArgumentFromMethod(this TypeReference type)
        {
            if (type.IsGenericParameter)
            {
                GenericParameter t = type as GenericParameter;
                return t.Type == GenericParameterType.Method;
            }

            if (type.IsByReference)
            {
                return ((ByReferenceType)type).ElementType.HasGenericArgumentFromMethod();
            }
            if (type.IsArray)
            {
                return ((ArrayType)type).ElementType.HasGenericArgumentFromMethod();
            }
            if (type.IsGenericInstance)
            {
                foreach (var typeArg in ((GenericInstanceType)type).GenericArguments)
                {
                    if (typeArg.HasGenericArgumentFromMethod())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check whether a method is generic.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns></returns>
        public static bool IsGeneric(this MethodReference method)
        {
            if (method.HasGenericParameters) return true;
            //if (method.ReturnType.IsGeneric()) return true;
            //foreach (var paramInfo in method.Parameters)
            //{
            //    if (paramInfo.ParameterType.IsGeneric()) return true;
            //}
            if (method.IsGenericInstance)
            {
                foreach (var typeArg in ((GenericInstanceMethod)method).GenericArguments)
                {
                    if (typeArg.IsGeneric())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check whether a field's type is generic.
        /// </summary>
        /// <param name="field">The field to check.</param>
        /// <returns></returns>
        public static bool IsGeneric(this FieldReference field)
        {
            return field.FieldType.IsGeneric();
        }

        /// <summary>
        /// Check whether two types are identical.
        /// </summary>
        /// <param name="left">The first type.</param>
        /// <param name="right">The second type.</param>
        /// <returns></returns>
        public static bool IsSameType(this TypeReference left, TypeReference right)
        {
            return left.FullName == right.FullName
                && left.Module.Assembly.FullName == right.Module.Assembly.FullName
                && left.Module.FileName == right.Module.FileName;
        }

        /// <summary>
        /// Check whether two type names match.
        /// </summary>
        /// <param name="left">The first type.</param>
        /// <param name="right">The second type.</param>
        /// <returns></returns>
        public static bool IsSameName(this TypeReference left, TypeReference right)
        {
            return left.FullName == right.FullName;
        }

        /// <summary>
        /// Compare method parameter and return type names.
        /// </summary>
        /// <param name="left">The first method.</param>
        /// <param name="right">The second method.</param>
        /// <returns></returns>
        public static bool IsTheSame(this MethodReference left, MethodReference right)
        {
            if (left.Parameters.Count != right.Parameters.Count
                        || left.Name != right.Name
                        || !left.ReturnType.IsSameName(right.ReturnType)
                        || !left.DeclaringType.IsSameName(right.DeclaringType)
                        || left.HasThis != right.HasThis
                        || left.GenericParameters.Count != right.GenericParameters.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Parameters.Count; i++)
            {
                if (left.Parameters[i].Attributes != right.Parameters[i].Attributes
                    || !left.Parameters[i].ParameterType.IsSameName(right.Parameters[i].ParameterType))
                {
                    return false;
                }
            }

            for (int i = 0; i < left.GenericParameters.Count; i++)
            {
                if (left.GenericParameters[i].IsSameName(right.GenericParameters[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check whether a method is a finalizer.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns></returns>
        public static bool IsFinalizer(this MethodDefinition method)
        {
            return method.Name == "Finalize" && method.IsVirtual && method.Parameters.Count == 0
                && method.ReturnType != null && method.ReturnType.FullName == "System.Void";
        }

        /// <summary>
        /// Try to import a type.
        /// </summary>
        /// <param name="toImport">The type to import.</param>
        /// <param name="module">The destination module.</param>
        /// <returns></returns>
        // #lizard forgives
        public static TypeReference TryImport(this TypeReference toImport, ModuleDefinition module)
        {
            if (toImport == null) return null;
            if (toImport.Namespace == "System")
            {
                if (toImport.Name == "Boolean")
                {
                    return module.TypeSystem.Boolean; //Use a built-in type so getMap can resolve it.
                }
                else if (toImport.Name == "Byte")
                {
                    return module.TypeSystem.Byte;
                }
                else if (toImport.Name == "SByte")
                {
                    return module.TypeSystem.SByte;
                }
                else if (toImport.Name == "Int16")
                {
                    return module.TypeSystem.Int16;
                }
                else if (toImport.Name == "Char")
                {
                    return module.TypeSystem.Char;
                }
                else if (toImport.Name == "UInt16")
                {
                    return module.TypeSystem.UInt16;
                }
                else if (toImport.Name == "Int32")
                {
                    return module.TypeSystem.Int32;
                }
                else if (toImport.Name == "UInt32")
                {
                    return module.TypeSystem.UInt32;
                }
                else if (toImport.Name == "Int64")
                {
                    return module.TypeSystem.Int64;
                }
                else if (toImport.Name == "UInt64")
                {
                    return module.TypeSystem.UInt64;
                }
                else if (toImport.Name == "Single")
                {
                    return module.TypeSystem.Single;
                }
                else if (toImport.Name == "Double")
                {
                    return module.TypeSystem.Double;
                }
                else if (toImport.Name == "IntPtr")
                {
                    return module.TypeSystem.IntPtr;
                }
                else if (toImport.Name == "UIntPtr")
                {
                    return module.TypeSystem.UIntPtr;
                }
            }
            if (toImport.IsGenericParameter) return toImport;
            if (toImport.IsGenericInstance)
            {
                var genericIns = toImport as GenericInstanceType;
                var newGenericIns = new GenericInstanceType(TryImport(toImport.Resolve(), module));
                foreach (var ga in genericIns.GenericArguments)
                {
                    newGenericIns.GenericArguments.Add(TryImport(ga, module));
                }
                return newGenericIns;
            }
            if (module.Assembly.FullName == toImport.Module.Assembly.FullName
                && module.FileName == toImport.Module.FileName)
            {
                return toImport;
            }
            else
            {
                return module.ImportReference(toImport);
            }
        }

        /// <summary>
        /// Try to import a method.
        /// </summary>
        /// <param name="toImport"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        public static MethodReference TryImport(this MethodReference toImport, ModuleDefinition module)
        {
            if (toImport == null) return null;
            if (module.Assembly.FullName == toImport.Module.Assembly.FullName
                && module.FileName == toImport.Module.FileName)
            {
                return toImport;
            }
            else
            {
                return module.ImportReference(toImport);
            }
        }

        /// <summary>
        /// Create a generic reference.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="declaringType"></param>
        /// <returns></returns>
        public static MethodReference MakeGeneric(this MethodDefinition method, TypeReference declaringType)
        {
            var reference = new MethodReference(method.Name, TryImport(method.ReturnType, declaringType.Module),
                declaringType)
            {
                HasThis = method.HasThis,
                ExplicitThis = method.ExplicitThis,
                CallingConvention = method.CallingConvention
            };
            foreach (var parameter in method.Parameters)
            {
                reference.Parameters.Add(new ParameterDefinition(TryImport(parameter.ParameterType,
                    declaringType.Module)));
            }
            foreach (var generic_parameter in method.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));
            return reference;
        }

        static bool isMatch(TypeReference left, TypeReference right, TypeReference context, bool isDefinition)
        {
            if (left.IsGenericParameter)
            {
                var gp = left as GenericParameter;
                var genericInstance = ((GenericInstanceType)context);
                var genericDefinition = isDefinition ? genericInstance.Resolve() : genericInstance.ElementType;
                for (int i = 0; i < genericDefinition.GenericParameters.Count; i++)
                {
                    if (genericDefinition.GenericParameters[i] == gp)
                    {
                        left = genericInstance.GenericArguments[i];
                    }
                }
            }
            if (left.FullName == right.FullName)
            {
                return true;
            }
            if (left.IsGenericInstance && right.IsGenericInstance)
            {
                return GetAssemblyQualifiedName(left, context, true) == GetAssemblyQualifiedName(right, context, true);
            }
            else
            {
                return false;
            }
        }

        static string stripName(string name)
        {
            var dot = name.LastIndexOf('.');
            if (dot >= 0)
            {
                return name.Substring(dot + 1);
            }
            else
            {
                return name;
            }
        }

        public static bool IsMatch(this MethodReference left, MethodDefinition right, TypeReference context)
        {
            if (left.Parameters.Count != right.Parameters.Count || stripName(left.Name) != stripName(right.Name))
            {
                return false;
            }

            if (!isMatch(left.ReturnType, right.ReturnType, context, left is MethodDefinition))
            {
                return false;
            }
            bool paramMatch = true;
            for (int i = 0; i < left.Parameters.Count; i++)
            {
                if (!isMatch(left.Parameters[i].ParameterType, right.Parameters[i].ParameterType, context,
                    left is MethodDefinition))
                {
                    paramMatch = false;
                    break;
                }
            }
            return paramMatch;
        }

        /// <summary>
        /// Check whether two method signatures match.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="checkDefineEqual"></param>
        /// <returns></returns>
        public static bool AreSignaturesEqual(MethodReference left, MethodReference right,
            bool checkDefineEqual = false)
        {
            if (left == right) return true;
            if (left.HasThis != right.HasThis || left.Parameters.Count != right.Parameters.Count
                || left.Name != right.Name
                || left.GenericParameters.Count != right.GenericParameters.Count)
            {
                return false;
            }

            if (checkDefineEqual && left.Resolve().ToString() != right.Resolve().ToString())
            {
                return false;
            }

            if (left.ReturnType.FillGenericArgument(left, left.DeclaringType).FullName
                != right.ReturnType.FillGenericArgument(right, right.DeclaringType).FullName)
            {
                return false;
            }

            for (int i = 0; i < left.Parameters.Count; i++)
            {
                if (left.Parameters[i].ParameterType.FillGenericArgument(left, left.DeclaringType).FullName !=
                    right.Parameters[i].ParameterType.FillGenericArgument(right, right.DeclaringType).FullName)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool CheckImplemention(this MethodReference itfMethod, MethodDefinition impl)
        {
            //A class may have multiple methods with the same signature; check Overrides in that case.
            if (impl.Overrides.Count > 0)
            {
                foreach (var o in impl.Overrides)
                {
                    if (AreSignaturesEqual(itfMethod, o, true))
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                return AreSignaturesEqual(itfMethod, impl);
            }
        }

        public static MethodReference FindMatch(this TypeReference itf, MethodDefinition method)
        {
            var itfDef = itf.Resolve();

            MethodDefinition found = null;

            foreach (var itfMethod in itfDef.Methods)
            {
                if (IsMatch(itfMethod, method, itf))
                {
                    found = itfMethod;
                    break;
                }
            }

            return ((found != null && itf.IsGenericInstance) ? 
                found.MakeGeneric(itf) : TryImport(found, itf.Module));
        }

        static int getLDCOperand(Instruction instrunction)
        {
            switch (instrunction.OpCode.Code)
            {
                case Code.Ldc_I4_0:
                    return 0;
                case Code.Ldc_I4_1:
                    return 1;
                case Code.Ldc_I4_2:
                    return 2;
                case Code.Ldc_I4_3:
                    return 3;
                case Code.Ldc_I4_4:
                    return 4;
                case Code.Ldc_I4_5:
                    return 5;
                case Code.Ldc_I4_6:
                    return 6;
                case Code.Ldc_I4_7:
                    return 7;
                case Code.Ldc_I4_8:
                    return 8;
                case Code.Ldc_I4_M1:
                    return -1;
                case Code.Ldc_I4_S:
                    return (byte)instrunction.Operand;
                case Code.Ldc_I4:
                    return (int)instrunction.Operand;
                default:
                    throw new Exception("no a ldc_i4 instrunction");
            }
        }

        public enum InjectType
        {
            Redirect,
            Switch,
            None
        }

        public static void AnalysisMethod(MethodDefinition method, out InjectType injectType, out int id,
            out int firstInstruction)
        {
            firstInstruction = 0;

            if (method.Body == null || method.Body.Instructions == null) goto NotInjectYet;

            var instructions = method.Body.Instructions;

            if (instructions.Count > 2 && instructions[1].OpCode.Code == Code.Call)
            {
                var callMethod = instructions[1].Operand as MethodReference;
                if (callMethod.DeclaringType.FullName == "IFix.Core.SwitchFlag" && callMethod.Name == "Get")
                {
                    injectType = InjectType.Switch;
                }
                else if (callMethod.DeclaringType.FullName == "IFix.WrappersManagerImpl" && callMethod.Name == "Get")
                {
                    injectType = InjectType.Redirect;
                }
                else
                {
                    goto NotInjectYet;
                }
                id = getLDCOperand(instructions[0]);
                for (int i = 0; i < instructions.Count; i++)
                {
                    if (instructions[i].OpCode.Code == Code.Ret)
                    {
                        firstInstruction = i + 1;
                        break;
                    }
                }
                return;
            }

            NotInjectYet:
            injectType = InjectType.None;
            id = -1;
        }

        //For an injected method, return its injection type, ID, and corresponding new method.
        public static void AnalysisMethod(Dictionary<string, Dictionary<string, List<MethodDefinition>>> searchData,
            MethodDefinition method, out InjectType injectType, out int id, out MethodDefinition foundMethod)
        {
            int firstInstruction;
            AnalysisMethod(method, out injectType, out id, out firstInstruction);
            foundMethod = null;
            if (id >= 0)
            {
                Dictionary<string, List<MethodDefinition>> methodsOfType;
                List<MethodDefinition> overloads;
                if (searchData.TryGetValue(method.DeclaringType.FullName, out methodsOfType)
                    && methodsOfType.TryGetValue(method.Name, out overloads))
                {
                    foreach (var overload in overloads)
                    {
                        if (overload.IsTheSame(method))
                        {
                            foundMethod = overload;
                            break;
                        }
                    }
                }
            }
        }

        static void addTypeAndNestType(List<TypeDefinition> result, TypeDefinition type)
        {
            result.Add(type);
            foreach (var nt in type.NestedTypes)
            {
                addTypeAndNestType(result, nt);
            }
        }

        /// <summary>
        /// Enumerate all types in an assembly, including nested types.
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static List<TypeDefinition> GetAllType(this AssemblyDefinition assembly)
        {
            List<TypeDefinition> result = new List<TypeDefinition>();

            foreach (var type in (from module in assembly.Modules from type in module.Types select type))
            {
                addTypeAndNestType(result, type);
            }

            return result;
        }
    }
}