/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

//IFix analyzes closures and generates adapters for their delegate types.
//A repair can introduce a closure where there was none, causing a missing-adapter error.
//CustomBridge configuration can avoid this, but users often cannot predict the need.
//For common cases with up to four reference-type parameters,
//and either no return value or a reference-type return, generate an adapter using generics.

namespace IFix.Core
{
    internal class GenericDelegateFactory
    {
        //Generic methods without a return value.
        static MethodInfo[] genericAction = null;
        //Generic methods with a return value.
        static MethodInfo[] genericFunc = null;

        //Cache of generic delegate adapter constructors.
        static System.Runtime.CompilerServices.ConditionalWeakTable<Type, Func<GenericDelegate, Delegate>> genericDelegateCreatorCache
            = new System.Runtime.CompilerServices.ConditionalWeakTable<Type, Func<GenericDelegate, Delegate>>();

        //Prevent unity il2cpp code stripping
        static void PreventStripping(object obj)
        {
            if (obj != null)
            {
                var gd = new GenericDelegate(null, -1, null);
                gd.Action();
                gd.Action(obj);
                gd.Action(obj, obj);
                gd.Action(obj, obj, obj);
                gd.Action(obj, obj, obj, obj);

                gd.Func<object>();
                gd.Func<object, object>(obj);
                gd.Func<object, object, object>(obj, obj);
                gd.Func<object, object, object, object>(obj, obj, obj);
                gd.Func<object, object, object, object, object>(obj, obj, obj, obj);
            }
        }

        internal static Delegate Create(Type delegateType, VirtualMachine virtualMachine, int methodId, object anonObj)
        {
            Func<GenericDelegate, Delegate> genericDelegateCreator;
            if (!genericDelegateCreatorCache.TryGetValue(delegateType, out genericDelegateCreator))
            {
                //Initialize the generic method arrays if needed.
                if (genericAction == null)
                {
                    PreventStripping(null);
                    var methods = typeof(GenericDelegate).GetMethods(BindingFlags.Instance | BindingFlags.Public
                        | BindingFlags.DeclaredOnly);
                    genericAction = methods.Where(m => m.Name == "Action").OrderBy(m => m.GetParameters().Length)
                        .ToArray();
                    genericFunc = methods.Where(m => m.Name == "Func").OrderBy(m => m.GetParameters().Length).ToArray();
                }

                MethodInfo delegateMethod = delegateType.GetMethod("Invoke");

                var parameters = delegateMethod.GetParameters();
                if ((delegateMethod.ReturnType.IsValueType && delegateMethod.ReturnType != typeof(void)) 
                    || parameters.Length > 4
                    || parameters.Any(p => p.ParameterType.IsValueType || p.ParameterType.IsByRef)
                    )
                {
                    //Outside the supported range, generate a constructor that always returns null.
                    genericDelegateCreator = (x) => null;
                }
                else
                {
                    if (delegateMethod.ReturnType == typeof(void) && parameters.Length == 0)
                    {
                        //Special case for no parameters and no return value.
                        var methodInfo = genericAction[0];
                        genericDelegateCreator = (o) => Delegate.CreateDelegate(delegateType, o, methodInfo);
                    }
                    else
                    {
                        //Select the generic implementation by parameter count and return type.
                        var typeArgs = parameters.Select(pinfo => pinfo.ParameterType);
                        MethodInfo genericMethodInfo = null;
                        if (delegateMethod.ReturnType == typeof(void))
                        {
                            genericMethodInfo = genericAction[parameters.Length];
                        }
                        else
                        {
                            genericMethodInfo = genericFunc[parameters.Length];
                            //Add the return type to the generic arguments when there is a return value.
                            typeArgs = typeArgs.Concat(new Type[] { delegateMethod.ReturnType });
                        }
                        //Instantiate the generic method.
                        var methodInfo = genericMethodInfo.MakeGenericMethod(typeArgs.ToArray());
                        //Constructor.
                        genericDelegateCreator = (o) => Delegate.CreateDelegate(delegateType, o, methodInfo);
                    }
                }
                //Cache the constructor for later calls.
                genericDelegateCreatorCache.Add(delegateType, genericDelegateCreator);
            }
            //Create the delegate.
            return genericDelegateCreator(new GenericDelegate(virtualMachine, methodId, anonObj));
        }
    }

    //Generic adapter.
    internal class GenericDelegate
    {
        //Reference to the virtual machine.
        VirtualMachine virtualMachine;

        //Virtual-machine method ID.
        int methodId;

        //Bound anonymous object.
        object anonObj;

        //Precomputed flag indicating whether to push anonObj.
        bool pushSelf;

        //Include anonObj in the argument count when it is pushed.
        int extraArgNum;

        internal GenericDelegate(VirtualMachine virtualMachine, int methodId, object anonObj)
        {
            this.virtualMachine = virtualMachine;
            this.methodId = methodId;
            this.anonObj = anonObj;
            pushSelf = anonObj != null;
            extraArgNum = pushSelf ? 1 : 0;
        }

        public void Action()
        {
            Call call = Call.Begin();
            if (pushSelf)
            {
                call.PushObject(anonObj);
            }
            virtualMachine.Execute(methodId, ref call, extraArgNum);
        }

        public void Action<T1>(T1 p1)
            where T1 : class
        {
            //Create the call object.
            Call call = Call.Begin();
            if (pushSelf)
            {
                //Push the bound anonymous object, if any.
                call.PushObject(anonObj);
            }
            //Push the first parameter.
            call.PushObject(p1);
            //Call the virtual-machine method with the specified ID.
            virtualMachine.Execute(methodId, ref call, 1 + extraArgNum);
        }

        public void Action<T1, T2>(T1 p1, T2 p2) 
            where T1 : class
            where T2 : class
        {
            Call call = Call.Begin();
            if (pushSelf)
            {
                call.PushObject(anonObj);
            }
            call.PushObject(p1);
            call.PushObject(p2);
            virtualMachine.Execute(methodId, ref call, 2 + extraArgNum);
        }

        public void Action<T1, T2, T3>(T1 p1, T2 p2, T3 p3)
            where T1 : class
            where T2 : class
            where T3 : class
        {
            Call call = Call.Begin();
            if (pushSelf)
            {
                call.PushObject(anonObj);
            }
            call.PushObject(p1);
            call.PushObject(p2);
            call.PushObject(p3);
            virtualMachine.Execute(methodId, ref call, 3 + extraArgNum);
        }

        public void Action<T1, T2, T3, T4>(T1 p1, T2 p2, T3 p3, T4 p4)
            where T1 : class
            where T2 : class
            where T3 : class
            where T4 : class
        {
            Call call = Call.Begin();
            if (pushSelf)
            {
                call.PushObject(anonObj);
            }
            call.PushObject(p1);
            call.PushObject(p2);
            call.PushObject(p3);
            call.PushObject(p4);
            virtualMachine.Execute(methodId, ref call, 4 + extraArgNum);
        }

        public TResult Func<TResult>()
            where TResult : class
        {
            Call call = Call.Begin();
            if (pushSelf)
            {
                call.PushObject(anonObj);
            }
            virtualMachine.Execute(methodId, ref call, extraArgNum);
            return (TResult)call.GetObject();
        }

        public TResult Func<T1, TResult>(T1 p1)
            where T1 : class
            where TResult : class
        {
            Call call = Call.Begin();
            if (pushSelf)
            {
                call.PushObject(anonObj);
            }
            call.PushObject(p1);
            virtualMachine.Execute(methodId, ref call, 1 + extraArgNum);
            //Read the result from the stack.
            return (TResult)call.GetObject();
        }

        public TResult Func<T1, T2, TResult>(T1 p1, T2 p2)
            where T1 : class
            where T2 : class
            where TResult : class
        {
            Call call = Call.Begin();
            if (pushSelf)
            {
                call.PushObject(anonObj);
            }
            call.PushObject(p1);
            call.PushObject(p2);
            virtualMachine.Execute(methodId, ref call, 2 + extraArgNum);
            return (TResult)call.GetObject();
        }

        public TResult Func<T1, T2, T3, TResult>(T1 p1, T2 p2, T3 p3)
            where T1 : class
            where T2 : class
            where T3 : class
            where TResult : class
        {
            Call call = Call.Begin();
            if (pushSelf)
            {
                call.PushObject(anonObj);
            }
            call.PushObject(p1);
            call.PushObject(p2);
            call.PushObject(p3);
            virtualMachine.Execute(methodId, ref call, 3 + extraArgNum);
            return (TResult)call.GetObject();
        }

        public TResult Func<T1, T2, T3, T4, TResult>(T1 p1, T2 p2, T3 p3, T4 p4)
            where T1 : class
            where T2 : class
            where T3 : class
            where T4 : class
            where TResult : class
        {
            Call call = Call.Begin();
            if (pushSelf)
            {
                call.PushObject(anonObj);
            }
            call.PushObject(p1);
            call.PushObject(p2);
            call.PushObject(p3);
            call.PushObject(p4);
            virtualMachine.Execute(methodId, ref call, 4 + extraArgNum);
            return (TResult)call.GetObject();
        }
    }
}
