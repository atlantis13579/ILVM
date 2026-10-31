/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */

using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace IFix.Core
{
    //Utilities used by the virtual machine.
    public static class Utils
    {
        /// <summary>
        /// Check whether a method can be assigned to a delegate variable.
        /// </summary>
        /// <param name="delegateMethod">The delegate type's Invoke method.</param>
        /// <param name="method">The method to assign.</param>
        /// <returns>Whether the assignment is valid.</returns>
        public static bool IsAssignable(MethodInfo delegateMethod, MethodInfo method)
        {
            if (delegateMethod == null || method == null)
            {
                return false;
            }
            if (delegateMethod.ReturnType != method.ReturnType)
            {
                return false;
            }
            ParameterInfo[] lhsParams = delegateMethod.GetParameters();
            ParameterInfo[] rhsParams = method.GetParameters();
            if (lhsParams.Length != rhsParams.Length)
            {
                return false;
            }

            for (int i = 0; i < lhsParams.Length; i++)
            {
                if (lhsParams[i].ParameterType != rhsParams[i].ParameterType
                    || lhsParams[i].IsOut != rhsParams[i].IsOut)
                {
                    return false;
                }
            }

            return true;
        }

        //Without the adapter cache, every lookup repeatedly calls the expensive IsAssignable check.
        // The wrapper type owns the cache so a retired assembly can be collected.
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Type,
            Dictionary<Type, MethodInfo>> delegateAdptCache =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Type, Dictionary<Type, MethodInfo>>();

        /// <summary>
        /// Find a method on a wrapper that can adapt to a delegate type.
        /// </summary>
        /// <param name="obj">The wrapper object.</param>
        /// <param name="delegateType">The delegate type.</param>
        /// <param name="perfix">A method-name prefix used to exclude methods such as constructors.</param>
        /// <returns></returns>
        public static Delegate TryAdapterToDelegate(object obj, Type delegateType, string perfix)
        {
            var cache = delegateAdptCache.GetValue(obj.GetType(), _ => new Dictionary<Type, MethodInfo>());
            MethodInfo method;
            lock (cache)
            {
                if (!cache.TryGetValue(delegateType, out method))
                {
                    MethodInfo delegateMethod = delegateType.GetMethod("Invoke");
                    method = obj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .FirstOrDefault(candidate => candidate.Name.StartsWith(perfix) && IsAssignable(delegateMethod, candidate));
                    cache[delegateType] = method;
                }
            }
            if (method == null)
            {
                return null;
            }
            else
            {
                return Delegate.CreateDelegate(delegateType, obj, method);
            }
        }
    }
}
