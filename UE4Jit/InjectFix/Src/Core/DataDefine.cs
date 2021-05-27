/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */
using System;

namespace IFix.Core
{
    using System.Runtime.InteropServices;
    public enum ValueType
    {
        Integer,
        Long,
        Float,
        Double,
        StackReference,//Value = pointer, 
        StaticFieldReference,
        FieldReference,//Value1 = objIdx, Value2 = fieldIdx
        ChainFieldReference,
        Object,        //Value1 = objIdx
        ValueType,     //Value1 = objIdx
        ArrayReference,//Value1 = objIdx, Value2 = elemIdx


        UnmanagedStruct = ValueType,
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Pack = 8)]
    public struct Value
    {
        [FieldOffset(0)]
        public ValueType Type;
        [FieldOffset(8)]
        public int Value1;
        [FieldOffset(12)]
        public int Value2;
        [FieldOffset(8)]
        public long I64Value;
        [FieldOffset(8)]
        public System.IntPtr StructPointer;
        [FieldOffset(8)]
        public System.Runtime.InteropServices.GCHandle ObjectHandle;
        public unsafe void SetSmallTypeValue<T>(T v, ValueType t = ValueType.Integer) where T : unmanaged
        {
            System.Diagnostics.Contracts.Contract.Assert(sizeof(T)<=8);
            Type = t;
            fixed (int* p = &this.Value1)
            {
                *(T*)p = v;
            }
        }
        public unsafe void SetStructValue<T>(T v) where T : unmanaged
        {
            Type = ValueType.UnmanagedStruct;
            StructPointer = AllocStruct<T>();
            *(T*)StructPointer.ToPointer() = v;
        }
        public unsafe void SetValue<T>(T v, ValueType t = ValueType.Integer) where T : unmanaged
        {
            if (sizeof(T) <= 8)
            {
                SetSmallTypeValue<T>(v, t);
            }
            else
            {
                SetStructValue<T>(v);
            }
        }
        public unsafe T GetValue<T>() where T : unmanaged
        {
            if (Type == ValueType.ValueType)
            {
                return *GetStructPointer<T>();
            }
            else
            {
                fixed (int* p = &Value1)
                {
                    return *(T*)p;
                }
            }
        }
        public unsafe T* GetStructPointer<T>() where T : unmanaged
        {
            System.Diagnostics.Debug.Assert(Type == ValueType.UnmanagedStruct);
            return (T*)StructPointer.ToPointer();
        }
        public unsafe static IntPtr AllocStruct<T>() where T : unmanaged
        {//todo:使用小内存池技术解决alloc-free效率问题
            return System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(T));
        }
        public void Dispose()
        {
            if (Type == ValueType.UnmanagedStruct)
            {
                if (StructPointer != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(StructPointer);
                    StructPointer = IntPtr.Zero;
                }
            }
        }
    }
    class Example
    {
        int Member0;
        float TestFuncion0(int a)
        {
            return 2.0f;
        }
        unsafe class ExampleWrapper
        {
            static void GetMember0(Value* tar, Example obj)
            {
                tar->Dispose();
                tar->SetValue<int>(obj.Member0);
            }
            static void SetMember0(Example obj, Value* tar)
            {
                obj.Member0 = tar->GetValue<int>();
            }
            static void TestFuncion0(Example obj, Value* stacks)
            {
                var arg0 = &stacks[0];//ldarg0
                var ret = obj.TestFuncion0(arg0->GetValue<int>());
                //push ret
            }
        }
    }
}