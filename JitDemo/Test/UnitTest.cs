/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */
 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace IFix.Test
{
    class Assertion
    {
        public static void AreEqual(object expected, object actual)
        {
            if (expected == actual)
            {
                return;
            }
            if (Equals(expected, actual))
            {
                return;
            }
            if (expected.ToString() == actual.ToString())
            {
                return;
            }
            System.Diagnostics.Debug.Assert(false);
        }

        public static void True(bool value)
        {
            System.Diagnostics.Debug.Assert(value);
        }

        public static void False(bool value)
        {
            System.Diagnostics.Debug.Assert(!value);
        }
    }

    public class VirtualMachineTest
    {
        public static void RunAll()
        {
            VirtualMachineTest.FileVMBuildBaseTest();
            VirtualMachineTest.RefBase();
            VirtualMachineTest.ExceptionBase();
            VirtualMachineTest.LeavePoint();
            VirtualMachineTest.TryCatchFinally();
            VirtualMachineTest.CatchByNextLevel();
            VirtualMachineTest.ClassBase();
            VirtualMachineTest.StructBase();
            VirtualMachineTest.PassByValue();
            VirtualMachineTest.VirtualFunc();
            VirtualMachineTest.InterfaceTest();
            VirtualMachineTest.VirtualFuncOfStruct();
            VirtualMachineTest.ItfWithRefParam();
            VirtualMachineTest.LdTokenBase();
            VirtualMachineTest.UnboxBase();
            VirtualMachineTest.GenericOverload();
            VirtualMachineTest.StaticFieldBase();
            VirtualMachineTest.ConvI4Base();
            VirtualMachineTest.LdLen();
            VirtualMachineTest.Newarr();
            VirtualMachineTest.Cast();
            VirtualMachineTest.Array();
            VirtualMachineTest.LogicalOperator();
            VirtualMachineTest.Ldflda();
            VirtualMachineTest.Conv_Ovf_I();
            VirtualMachineTest.Ceq();
            VirtualMachineTest.BitsOp();
            VirtualMachineTest.Conv_U1();
            VirtualMachineTest.Ldelema();
            VirtualMachineTest.Bgt();
            VirtualMachineTest.Ldsflda();
            VirtualMachineTest.Initobj();
            VirtualMachineTest.Arithmetic();
            VirtualMachineTest.NaNFloat();
            VirtualMachineTest.Rem();
            VirtualMachineTest.Ldc_R8();
            VirtualMachineTest.Ldc_I8();
            VirtualMachineTest.Int64();
            VirtualMachineTest.Closure();
            VirtualMachineTest.Conv_R_Un();
            VirtualMachineTest.NaNFloatBranch();
        }


        static public void FileVMBuildBaseTest()
        {
            for (int i = 0; i < 10; i++)
            {
                Assertion.AreEqual(BaseTest.Base(i), Redirect.BaseTest.Base(i));
            }
        }

        static public void RefBase()
        {
            //几个典型基础值类型的引用类型测试
            int a1 = 2;
            long b1 = 5;
            int c1 = 1;
            long r1 = BaseTest.Ref(ref a1, a1, ref b1, b1, out c1);

            int a2 = 2;
            long b2 = 5;
            int c2 = 1;
            long r2 = Redirect.BaseTest.Ref(ref a2, a2, ref b2, b2, out c2);

            Assertion.AreEqual(a1, a2);
            Assertion.AreEqual(b1, b2);
            Assertion.AreEqual(c1, c2);
            Assertion.AreEqual(r1, r2);

            //对象的引用测试
            object o1 = new object();
            object o2 = new object();

            object p1 = o1;
            object p2 = o2;
            Redirect.BaseTest.Ref(ref p1, ref p2);
            Assertion.True(ReferenceEquals(o1, p2));
            Assertion.True(ReferenceEquals(o2, p1));

            //结构体的引用测试
            Redirect.ValueTypeCounter v1 = new Redirect.ValueTypeCounter(1);
            Redirect.ValueTypeCounter v2 = new Redirect.ValueTypeCounter(2);

            Redirect.BaseTest.Ref(ref v1, ref v2);
            Assertion.AreEqual(1, v2.Val);
            Assertion.AreEqual(2, v1.Val);

            for (int i = 0; i < 10240; i++)
            {
                int a = 2;
                long b = 5;
                int c = 1;
                Redirect.BaseTest.Ref(ref a, a, ref b, b, out c);
            }
        }


        static public void ExceptionBase()
        {
            //BaseTest.ExceptionBase(1);
            //BaseTest.ExceptionBase(-1);
            //之所以要进行MAX_EVALUATION_STACK_SIZE次测试，是测试有漏清理栈对象
            for (int j = 2; j < 10240 + 2; j++)
            {
                int tmp1 = j;
                BaseTest.ExceptionBase(ref tmp1);
                int tmp2 = j;
                //Console.WriteLine("before:" + tmp2);
                Redirect.BaseTest.ExceptionBase(ref tmp2);
                //Console.WriteLine("after:" + tmp2);
                Assertion.AreEqual(tmp1, tmp2);
            }

            //基础异常流程测试
            // int i1 = -1, i2 = -1;
            // Assertion.That(() => BaseTest.ExceptionBase(ref i1), Throws.ArgumentException);
            // Assertion.That(() => Redirect.BaseTest.ExceptionBase(ref i2), Throws.ArgumentException);
            //Console.WriteLine(i2);

            // i1 = 0;
            // i2 = 0;
            // Assertion.That(() => BaseTest.ExceptionBase(ref i1), Throws.InvalidOperationException);
            // Assertion.That(() => Redirect.BaseTest.ExceptionBase(ref i2), Throws.InvalidOperationException);
            //Console.WriteLine(i);

            //BaseTest.ExceptionBase(10);
            // Assertion.Throws<InvalidOperationException>(() => BaseTest.Rethrow());
            // Assertion.Throws<InvalidOperationException>(() => Redirect.BaseTest.Rethrow());
        }

        //各种异常跳出点的测试

        static public void LeavePoint()
        {
            int a1 = 0, b1 = 0, c1 = 0, a2 = 0, b2 = 0, c2 = 0;

            BaseTest.LeavePoint(0, ref a1, ref b1, ref c1);
            Redirect.BaseTest.LeavePoint(0, ref a2, ref b2, ref c2);
            Assertion.AreEqual(a1, a2);
            Assertion.AreEqual(b1, b2);
            Assertion.AreEqual(c1, c2);

            a1 = 0; b1 = 0; c1 = 0; a2 = 0; b2 = 0; c2 = 0;
            BaseTest.LeavePoint(1, ref a1, ref b1, ref c1);
            Redirect.BaseTest.LeavePoint(1, ref a2, ref b2, ref c2);
            Assertion.AreEqual(a1, a2);
            Assertion.AreEqual(b1, b2);
            Assertion.AreEqual(c1, c2);

            a1 = 0; b1 = 0; c1 = 0; a2 = 0; b2 = 0; c2 = 0;
            BaseTest.LeavePoint(2, ref a1, ref b1, ref c1);
            Redirect.BaseTest.LeavePoint(2, ref a2, ref b2, ref c2);
            Assertion.AreEqual(a1, a2);
            Assertion.AreEqual(b1, b2);
            Assertion.AreEqual(c1, c2);

            a1 = 0; b1 = 0; c1 = 0; a2 = 0; b2 = 0; c2 = 0;
            BaseTest.LeavePoint(3, ref a1, ref b1, ref c1);
            Redirect.BaseTest.LeavePoint(3, ref a2, ref b2, ref c2);
            Assertion.AreEqual(a1, a2);
            Assertion.AreEqual(b1, b2);
            Assertion.AreEqual(c1, c2);
        }

        //finally逻辑的测试

        static public void TryCatchFinally()
        {
            bool t1, t2;
            bool c1, c2;
            bool f1, f2;
            bool e1, e2;

            t1 = c1 = f1 = e1 = false;
            t2 = c2 = f2 = e2 = false;
            BaseTest.TryCatchFinally(false, ref t1, ref c1, ref f1, ref e1);
            Redirect.BaseTest.TryCatchFinally(false, ref t2, ref c2, ref f2, ref e2);
            Assertion.AreEqual(t1, t2);
            Assertion.AreEqual(c1, c2);
            Assertion.AreEqual(f1, f2);
            Assertion.AreEqual(e1, e2);

            t1 = c1 = f1 = e1 = false;
            t2 = c2 = f2 = e2 = false;
            BaseTest.TryCatchFinally(true, ref t1, ref c1, ref f1, ref e1);
            Redirect.BaseTest.TryCatchFinally(true, ref t2, ref c2, ref f2, ref e2);
            Assertion.AreEqual(t1, t2);
            Assertion.AreEqual(c1, c2);
            Assertion.AreEqual(f1, f2);
            Assertion.AreEqual(e1, e2);

            //BaseTest.ConstrainedInstruction(true, 1);
        }

        //try-catch嵌套测试

        static public void CatchByNextLevel()
        {
            bool a1, a2, a3;
            bool b1, b2, b3;
            BaseTest.CatchByNextLevel(out a1, out a2, out a3);
            Redirect.BaseTest.CatchByNextLevel(out b1, out b2, out b3);
            Assertion.AreEqual(a1, b1);
            Assertion.AreEqual(a2, b2);
            Assertion.AreEqual(a3, b3);
        }

        //public class ShallowCloneTest
        //{
        //    public int Foo;
        //    public long Bar;

        //    public ShallowCloneTest Clone()
        //    {
        //        return (ShallowCloneTest)base.MemberwiseClone();
        //    }
        //}

        //class基础测试

        static public void ClassBase()
        {
            Redirect.RefTypeCounter rtc = new Redirect.RefTypeCounter();
            int c = rtc.Val;
            rtc.Inc();//TODO: 反射访问字段非常慢
            Assertion.AreEqual(rtc.Val, c + 1);
            //var MemberwiseClone = typeof(object).GetMethod("MemberwiseClone",
            //    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            //object vt = new ValueTypeCounter();
            ////ShallowCloneTest t1 = new ShallowCloneTest() { Bar = 1, Foo = 2 };
            //Stopwatch sw = Stopwatch.StartNew();
            ////Console.Write("m:" + MemberwiseClone);
            //var objClone = new ObjectClone();
            //for (int i = 0; i < 1000000; ++i)
            //{
            //    //var cloned = t1.Clone();
            //    //MemberwiseClone.Invoke(t1, null);
            //    //MemberwiseClone.Invoke(vt, null);
            //    //var cloned = ObjectCloner.Clone(t1);
            //    var cloned = objClone.Clone(vt);
            //}
            //Console.WriteLine("Took {0:0.00}s", sw.Elapsed.TotalSeconds);
        }

        //结构体基础测试

        static public void StructBase()
        {
            Redirect.ValueTypeCounter vtc = new Redirect.ValueTypeCounter();
            int c = vtc.Val;
            vtc.Inc();
            Assertion.AreEqual(vtc.Val, c + 1);
        }

        //参数值传递测试

        static public void PassByValue()
        {
            Redirect.ValueTypeCounter c1 = new Redirect.ValueTypeCounter();
            Redirect.RefTypeCounter c2 = new Redirect.RefTypeCounter();
            Redirect.BaseTest.PassByValue(ref c1, c2);
            //Console.WriteLine("c1.v:" + c1.Val + ",c2.v:" + c2.Val);
            Assertion.AreEqual(2, c2.Val);
            Assertion.AreEqual(1, c1.Val);
        }

        //虚函数测试

        static public void VirtualFunc()
        {
            int r1, r2;
            Redirect.BaseTest.VirtualFunc(out r1, out r2);
            Assertion.AreEqual(0, r1);
            Assertion.AreEqual(1, r2);
            Redirect.BaseClass o1 = new Redirect.BaseClass();
            Redirect.BaseClass o2 = new Redirect.DrivenClass();
            Assertion.AreEqual(0, o1.Foo());
            Assertion.AreEqual(1, o2.Foo());
        }

        //接口测试

        static public void InterfaceTest()
        {
            Assertion.AreEqual(30, Redirect.BaseTest.InterfaceTest(1, 2, 10));
        }

        //结构体虚函数测试

        static public void VirtualFuncOfStruct()
        {
            Redirect.ValueTypeCounter c1 = new Redirect.ValueTypeCounter();
            c1.Inc();
            c1.Inc();
            Assertion.AreEqual("ValueTypeCounter { 2 }", c1.ToString());

            Assertion.AreEqual(c1.ToString() + ",hashcode:" + c1.GetHashCode(), Redirect.BaseTest.VirtualFuncOfStruct(c1));
        }

        //带ref参数的interface测试

        static public void ItfWithRefParam()
        {
            int a = 10;
            int b;
            int ret = Redirect.BaseTest.ItfWithRefParam(ref a, out b);
            //Console.WriteLine("a:" + a + ",b:" + b + ",ret:" + ret);
            Assertion.AreEqual(20, a);
            Assertion.AreEqual(21, b);
            Assertion.AreEqual(20, ret);
        }

        //ldtoken指令的测试

        static public void LdTokenBase()
        {
            Assertion.AreEqual(typeof(int), Redirect.BaseTest.GetIntType());
        }

        //unbox指令测试

        static public void UnboxBase()
        {
            Redirect.ValueTypeCounter c1 = new Redirect.ValueTypeCounter();
            Redirect.ValueTypeCounter c2 = new Redirect.ValueTypeCounter();
            c1.Inc();
            Assertion.AreEqual(1, c1.CompareTo(c2));
            c2.Inc();
            Assertion.AreEqual(0, c1.CompareTo(c2));
            c2.Inc();
            Assertion.AreEqual(-1, c1.CompareTo(c2));

            // Assertion.That(() => c1.CompareTo(1), Throws.ArgumentException);
        }

        //泛型签名测试

        static public void GenericOverload()
        {
            Assertion.AreEqual(BaseTest.GenericOverload(), Redirect.BaseTest.GenericOverload());
            //Console.WriteLine(Redirect.BaseTest.GenericOverload());
        }

        //静态字段测试

        static public void StaticFieldBase()
        {
            for (int i = 0; i < 10; i++)
            {
                Assertion.AreEqual(BaseTest.StaticFieldBase(), Redirect.BaseTest.StaticFieldBase());
            }
        }

        //ConvI4指令

        static public void ConvI4Base()
        {
            Assertion.AreEqual(BaseTest.Conv_I4((float)uint.MaxValue),
                Redirect.BaseTest.Conv_I4((float)uint.MaxValue));
            Assertion.AreEqual(BaseTest.Conv_I4((double)uint.MaxValue),
                Redirect.BaseTest.Conv_I4((double)uint.MaxValue));
            Assertion.AreEqual(BaseTest.Conv_I4(long.MaxValue),
                Redirect.BaseTest.Conv_I4(long.MaxValue));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Conv_Ovf_I4_Un(uint.MaxValue));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Conv_Ovf_I4(long.MaxValue));
        }

        //LdLen指令

        static public void LdLen()
        {
            for (int i = 0; i < 10; i++)
            {
                Assertion.AreEqual(i, Redirect.BaseTest.Ldlen(new int[i]));
            }
        }

        //Newarr指令

        static public void Newarr()
        {
            for (int i = 0; i < 10; i++)
            {
                Assertion.AreEqual(i, Redirect.BaseTest.Newarr(i).Length);
            }
        }

        //Isinst，Castclass指令

        static public void Cast()
        {
            Redirect.BaseClass bc = new Redirect.BaseClass();
            Assertion.AreEqual(null, Redirect.BaseTest.Isinst(bc));
            // Assertion.Throws<InvalidCastException>(() => Redirect.BaseTest.Castclass(bc));
        }

        //数组测试

        static public void Array()
        {
            object[] objArr = new object[2];
            Redirect.BaseTest.ArraySet(objArr, 0);
            var now = DateTime.Now;
            Redirect.BaseTest.ArraySet(objArr, 1, now);
            Assertion.AreEqual(1, objArr[0]);
            Assertion.AreEqual(now, objArr[1]);
            Assertion.AreEqual(1, Redirect.BaseTest.ArrayGet(objArr, 0));
            Assertion.AreEqual(now, Redirect.BaseTest.ArrayGet(objArr, 1));
            // Assertion.Throws<NullReferenceException>(() => Redirect.BaseTest.ArraySet(null, 1));
            // Assertion.Throws<IndexOutOfRangeException>(() => Redirect.BaseTest.ArraySet(objArr, -1));
            // Assertion.Throws<IndexOutOfRangeException>(() => Redirect.BaseTest.ArraySet(objArr, 2));
            byte[] byteArr = new byte[2] { 1, 2 };
            Assertion.AreEqual(1, Redirect.BaseTest.ArrayGet(byteArr, 0));
            Assertion.AreEqual(2, Redirect.BaseTest.ArrayGet(byteArr, 1));
            Redirect.BaseTest.ArraySet(byteArr, 0, 10);
            Assertion.AreEqual(10, byteArr[0]);
            Assertion.AreEqual(2, byteArr[1]);
            Redirect.BaseTest.ArraySet(byteArr, 1, 20);
            Assertion.AreEqual(10, byteArr[0]);
            Assertion.AreEqual(20, byteArr[1]);

            int[] intArr = new int[2] { 1, 2 };
            Assertion.AreEqual(1, Redirect.BaseTest.ArrayGet(intArr, 0));
            Assertion.AreEqual(2, Redirect.BaseTest.ArrayGet(intArr, 1));
            Redirect.BaseTest.ArraySet(intArr, 0, 10);
            Assertion.AreEqual(10, intArr[0]);
            Assertion.AreEqual(2, intArr[1]);
            Redirect.BaseTest.ArraySet(intArr, 1, 20);
            Assertion.AreEqual(10, intArr[0]);
            Assertion.AreEqual(20, intArr[1]);

            intArr = new int[2] { 1, 2 };
            Assertion.AreEqual(1, Redirect.BaseTest.ArrayGet(intArr, (uint)0));
            Assertion.AreEqual(2, Redirect.BaseTest.ArrayGet(intArr, (uint)1));
            Redirect.BaseTest.ArraySet(intArr, (uint)0, 10);
            Assertion.AreEqual(10, intArr[0]);
            Assertion.AreEqual(2, intArr[1]);

            uint[] uintArr = new uint[2] { 1, 2 };
            Assertion.AreEqual(1, Redirect.BaseTest.ArrayGet(uintArr, 0));
            Assertion.AreEqual(2, Redirect.BaseTest.ArrayGet(uintArr, 1));
            Redirect.BaseTest.ArraySet(uintArr, 0, 10);
            Assertion.AreEqual(10, uintArr[0]);
            Assertion.AreEqual(2, uintArr[1]);
            Redirect.BaseTest.ArraySet(uintArr, 1, 20);
            Assertion.AreEqual(10, uintArr[0]);
            Assertion.AreEqual(20, uintArr[1]);

            float[] floatArr = new float[2] { 1, 2 };
            Assertion.AreEqual(1, Redirect.BaseTest.ArrayGet(floatArr, 0));
            Assertion.AreEqual(2, Redirect.BaseTest.ArrayGet(floatArr, 1));
            Redirect.BaseTest.ArraySet(floatArr, 0, 10);
            Assertion.AreEqual(10, floatArr[0]);
            Assertion.AreEqual(2, floatArr[1]);
            Redirect.BaseTest.ArraySet(floatArr, 1, 20);
            Assertion.AreEqual(10, floatArr[0]);
            Assertion.AreEqual(20, floatArr[1]);

            double[] doubleArr = new double[2] { 1, 2 };
            Assertion.AreEqual(1, Redirect.BaseTest.ArrayGet(doubleArr, 0));
            Assertion.AreEqual(2, Redirect.BaseTest.ArrayGet(doubleArr, 1));
            Redirect.BaseTest.ArraySet(doubleArr, 0, 10);
            Assertion.AreEqual(10, doubleArr[0]);
            Assertion.AreEqual(2, doubleArr[1]);
            Redirect.BaseTest.ArraySet(doubleArr, 1, 20);
            Assertion.AreEqual(10, doubleArr[0]);
            Assertion.AreEqual(20, doubleArr[1]);

            short[] shortArr = new short[2] { 1, 2 };
            Assertion.AreEqual(1, Redirect.BaseTest.ArrayGet(shortArr, 0));
            Assertion.AreEqual(2, Redirect.BaseTest.ArrayGet(shortArr, 1));
            Redirect.BaseTest.ArraySet(shortArr, 0, 10);
            Assertion.AreEqual(10, shortArr[0]);
            Assertion.AreEqual(2, shortArr[1]);
            Redirect.BaseTest.ArraySet(shortArr, 1, 20);
            Assertion.AreEqual(10, shortArr[0]);
            Assertion.AreEqual(20, shortArr[1]);

            ushort[] ushortArr = new ushort[2] { 1, 2 };
            Assertion.AreEqual(1, Redirect.BaseTest.ArrayGet(ushortArr, 0));
            Assertion.AreEqual(2, Redirect.BaseTest.ArrayGet(ushortArr, 1));
            Redirect.BaseTest.ArraySet(ushortArr, 0, 10);
            Assertion.AreEqual(10, ushortArr[0]);
            Assertion.AreEqual(2, ushortArr[1]);
            Redirect.BaseTest.ArraySet(ushortArr, 1, 20);
            Assertion.AreEqual(10, ushortArr[0]);
            Assertion.AreEqual(20, ushortArr[1]);

            char[] charArr = new char[2] { 'a', 'b' };
            Assertion.AreEqual('a', Redirect.BaseTest.ArrayGet(charArr, 0));
            Assertion.AreEqual('b', Redirect.BaseTest.ArrayGet(charArr, 1));
            Redirect.BaseTest.ArraySet(charArr, 0, 'c');
            Assertion.AreEqual('c', charArr[0]);
            Assertion.AreEqual('b', charArr[1]);
            Redirect.BaseTest.ArraySet(charArr, 1, 'd');
            Assertion.AreEqual('c', charArr[0]);
            Assertion.AreEqual('d', charArr[1]);

            IntPtr[] intPtrArr = new IntPtr[] { new IntPtr(int.MaxValue), new IntPtr(int.MinValue) };
            Assertion.AreEqual((long)int.MaxValue, Redirect.BaseTest.ArrayGet(intPtrArr, 0).ToInt64());
            Assertion.AreEqual((long)int.MinValue, Redirect.BaseTest.ArrayGet(intPtrArr, 1).ToInt64());
            Redirect.BaseTest.ArraySet(intPtrArr, 0, new IntPtr(1));
            Assertion.AreEqual((long)1, Redirect.BaseTest.ArrayGet(intPtrArr, 0).ToInt64());
            Assertion.AreEqual((long)int.MinValue, Redirect.BaseTest.ArrayGet(intPtrArr, 1).ToInt64());
            Redirect.BaseTest.ArraySet(intPtrArr, 1, new IntPtr(2));
            Assertion.AreEqual((long)1, Redirect.BaseTest.ArrayGet(intPtrArr, 0).ToInt64());
            Assertion.AreEqual((long)2, Redirect.BaseTest.ArrayGet(intPtrArr, 1).ToInt64());

            UIntPtr[] uintPtrArr = new UIntPtr[] { new UIntPtr(int.MaxValue), new UIntPtr(0) };
            Assertion.AreEqual((ulong)int.MaxValue, Redirect.BaseTest.ArrayGet(uintPtrArr, 0).ToUInt64());
            Assertion.AreEqual((ulong)0, Redirect.BaseTest.ArrayGet(uintPtrArr, 1).ToUInt64());
            Redirect.BaseTest.ArraySet(uintPtrArr, 0, new UIntPtr(1));
            Assertion.AreEqual((ulong)1, Redirect.BaseTest.ArrayGet(uintPtrArr, 0).ToUInt64());
            Assertion.AreEqual((ulong)0, Redirect.BaseTest.ArrayGet(uintPtrArr, 1).ToUInt64());
            Redirect.BaseTest.ArraySet(uintPtrArr, 1, new UIntPtr(2));
            Assertion.AreEqual((ulong)1, Redirect.BaseTest.ArrayGet(uintPtrArr, 0).ToUInt64());
            Assertion.AreEqual((ulong)2, Redirect.BaseTest.ArrayGet(uintPtrArr, 1).ToUInt64());
        }

        //逻辑操作符

        static public void LogicalOperator()
        {
            int a = 321312, b = 954932;
            Assertion.AreEqual(a & b, Redirect.BaseTest.And(a, b));
            Assertion.AreEqual(a | b, Redirect.BaseTest.Or(a, b));
            long c = 415661, d = 5415513;
            Assertion.AreEqual(c & d, Redirect.BaseTest.And(c, d));
            Assertion.AreEqual(c | d, Redirect.BaseTest.Or(c, d));
        }

        //Ldflda指令

        static public void Ldflda()
        {
            Redirect.ValueTypeCounter c = new Redirect.ValueTypeCounter();
            Redirect.BaseTest.Ldflda(ref c);
            Assertion.AreEqual(10, c.Val);
            Redirect.BaseTest.Ldflda(ref c);
            c.Inc();
            Assertion.AreEqual(21, c.Val);

            c = new Redirect.ValueTypeCounter();
            Redirect.ValueTypeCounterContainer cc = new Redirect.ValueTypeCounterContainer();
            cc.c = c;
            Redirect.BaseTest.Ldflda(ref cc);
            Assertion.AreEqual(10, cc.c.i);
            Redirect.BaseTest.Ldflda(ref cc);
            cc.c.Inc();
            Assertion.AreEqual(21, cc.c.Val);

            Redirect.W1 w1 = new Redirect.W1()
            {
                F = new Redirect.ValueTypeCounter()
            };

            Redirect.W2 w2 = new Redirect.W2()
            {
                F = w1
            };

            Redirect.W3 w3 = new Redirect.W3()
            {
                F = w2
            };

            Redirect.BaseTest.Ldflda(ref w1);
            Assertion.AreEqual(10, w1.F.i);

            Redirect.BaseTest.Ldflda(ref w2);
            Assertion.AreEqual(10, w2.F.F.i);

            Redirect.BaseTest.Ldflda(ref w3);
            Assertion.AreEqual(10, w3.F.F.F.i);

            Assertion.AreEqual(10, Redirect.BaseTest.Ldflda_m(ref w3));
        }

        //Conv_Ovf_I指令

        static public void Conv_Ovf_I()
        {
            int i = 10;
            Assertion.AreEqual(i, Redirect.BaseTest.Conv_Ovf_I(i).Length);
        }

        //Ceq指令

        static public void Ceq()
        {
            Assertion.True(Redirect.BaseTest.Ceq(1, 1));
            Assertion.False(Redirect.BaseTest.Ceq(321, 1));
            Assertion.True(Redirect.BaseTest.Ceq((double)1, 1));
            Assertion.False(Redirect.BaseTest.Ceq((double)321, 1));
        }

        //位操作符

        static public void BitsOp()
        {
            int a = 321312;
            int bits = 5;
            long b = a;
            uint ua = uint.MaxValue;
            ulong ub = ulong.MaxValue;
            Assertion.AreEqual(a << bits, Redirect.BaseTest.Shl(a, bits));
            Assertion.AreEqual(b << bits, Redirect.BaseTest.Shl(b, bits));
            Assertion.AreEqual(a >> bits, Redirect.BaseTest.Shr(a, bits));
            Assertion.AreEqual(b >> bits, Redirect.BaseTest.Shr(b, bits));
            Assertion.AreEqual(ua >> bits, Redirect.BaseTest.Shr_Un(ua, bits));
            Assertion.AreEqual(ub >> bits, Redirect.BaseTest.Shr_Un(ub, bits));
            long c = 321421;
            Assertion.AreEqual(a ^ bits, Redirect.BaseTest.Xor(a, bits));
            Assertion.AreEqual(b ^ c, Redirect.BaseTest.Xor(b, c));

            Assertion.AreEqual(~a, Redirect.BaseTest.Not(a));
            Assertion.AreEqual(~b, Redirect.BaseTest.Not(b));
        }

        //Conv_U1指令

        static public void Conv_U1()
        {
            int a = 1024;
            Assertion.AreEqual(0, Redirect.BaseTest.Conv_U1(a));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Conv_Ovf_U1(a));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Conv_Ovf_U1_Un((uint)a));
        }


        static public void Ldelema()
        {
            int[] arr = new int[] { 1, 2 };
            Redirect.BaseTest.Ldelema(arr, 0);
            Assertion.AreEqual(11, arr[0]);
            Assertion.AreEqual(2, arr[1]);
            Redirect.BaseTest.Ldelema(arr, 1);
            Assertion.AreEqual(11, arr[0]);
            Assertion.AreEqual(12, arr[1]);
        }


        static public void Bgt()
        {
            Assertion.AreEqual(1, Redirect.BaseTest.Bgt(3, 2));
            Assertion.AreEqual(-1, Redirect.BaseTest.Bgt(2, 3));
            Assertion.AreEqual(0, Redirect.BaseTest.Bgt(3, 3));
        }


        static public void Ldsflda()
        {
            Assertion.AreEqual(10, Redirect.BaseTest.Ldsflda());
            Assertion.AreEqual(20, Redirect.BaseTest.Ldsflda());
        }


        static public void Initobj()
        {
            Assertion.AreEqual(BaseTest.Initobj(42), Redirect.BaseTest.Initobj(42));
        }

        //数学运算测试，checked关键字测试

        static public void Arithmetic()
        {
            int a0 = 1, b0 = 2;
            long a1 = 324, b1 = 4314;
            float a2 = 321.41f, b2 = 31254.99f;
            double a3 = 321321.314312f, b3 = 3214321.31255;
            Assertion.AreEqual(a0 / b0, Redirect.BaseTest.Div(a0, b0));
            Assertion.AreEqual(a1 / b1, Redirect.BaseTest.Div(a1, b1));
            Assertion.AreEqual(a2 / b2, Redirect.BaseTest.Div(a2, b2));
            Assertion.AreEqual(a3 / b3, Redirect.BaseTest.Div(a3, b3));

            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Mul_Ovf(int.MaxValue, 2));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Mul_Ovf_Un(uint.MaxValue, 2));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Add_Ovf(int.MaxValue, 1));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Add_Ovf_Un(uint.MaxValue, 1));

            Assertion.AreEqual(1 / uint.MaxValue, Redirect.BaseTest.Div_Un(1, uint.MaxValue));

            Assertion.AreEqual(-a0, Redirect.BaseTest.Neg(a0));
            Assertion.AreEqual(-a1, Redirect.BaseTest.Neg(a1));
            Assertion.AreEqual(-a2, Redirect.BaseTest.Neg(a2));
            Assertion.AreEqual(-a3, Redirect.BaseTest.Neg(a3));
        }

        //Nan运算

        static public void NaNFloat()
        {
            for (int i = 0; i < 4; i++)
            {
                //Console.WriteLine("nan nan " + i);
                Assertion.AreEqual(BaseTest.NaNFloat(i, float.NaN, float.NaN),
                    Redirect.BaseTest.NaNFloat(i, float.NaN, float.NaN));
            }

            for (int i = 0; i < 4; i++)
            {
                // Console.WriteLine("1 nan " + i);
                Assertion.AreEqual(BaseTest.NaNFloat(i, 1, float.NaN), Redirect.BaseTest.NaNFloat(i, 1, float.NaN));
            }

            for (int i = 0; i < 4; i++)
            {
                // Console.WriteLine("nan 1 " + i);
                Assertion.AreEqual(BaseTest.NaNFloat(i, float.NaN, 1), Redirect.BaseTest.NaNFloat(i, float.NaN, 1));
            }
        }


        static public void Rem()
        {
            Assertion.AreEqual(BaseTest.Rem(32, 7), Redirect.BaseTest.Rem(32, 7));
            Assertion.AreEqual(BaseTest.Rem(32.1f, 7), Redirect.BaseTest.Rem(32.1f, 7));

            Assertion.AreEqual(BaseTest.Rem(uint.MaxValue, 7), Redirect.BaseTest.Rem(uint.MaxValue, 7));
        }


        static public void Ldc_R8()
        {
            Assertion.AreEqual(BaseTest.Ldc_R8(), Redirect.BaseTest.Ldc_R8());
        }


        static public void Ldc_I8()
        {
            Assertion.AreEqual(BaseTest.Ldc_I8(), Redirect.BaseTest.Ldc_I8());
        }

        //64位测试

        static public void Int64()
        {
            float a = ulong.MaxValue;
            Assertion.AreEqual(BaseTest.Conv_I8(a), Redirect.BaseTest.Conv_I8(a));
            Assertion.AreEqual(BaseTest.Conv_U8(a), Redirect.BaseTest.Conv_U8(a));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Conv_Ovf_U8(-1));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Conv_Ovf_U8(a * 2));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Conv_Ovf_I8(ulong.MaxValue));
            // Assertion.Throws<OverflowException>(() => Redirect.BaseTest.Conv_Ovf_I8(a * 2));
        }

        class AnonymousClass2 : Redirect.AnonymousClass
        {
            public override void FAdd()
            {
                f += 5;
            }
        }

        static int sum_of_enumerator(System.Collections.IEnumerator enumerator)
        {
            int sum = 0;
            while (enumerator.MoveNext())
            {
                object c = enumerator.Current;
                if (c is int)
                {
                    sum += (int)c;
                }
            }
            return sum;
        }

        static int sum_of_enumerator(IEnumerator<int> enumerator)
        {
            int sum = 0;
            while (enumerator.MoveNext())
            {
                sum += enumerator.Current;
            }
            return sum;
        }


        static public void Closure()
        {
            Redirect.AnonymousClass anony = new Redirect.AnonymousClass();
            int local = 0, field = 0, staticField = 0;
            anony.CallRepeat(10, out local, out field, out staticField);
            //Console.WriteLine("local:" + local + ",field:" + field + ",static field:" + staticField);
            Assertion.AreEqual(10, local);
            Assertion.AreEqual(10, field);
            Assertion.AreEqual(10, staticField);
            anony.CallRepeat(6, out local, out field, out staticField);
            Assertion.AreEqual(6, local);
            Assertion.AreEqual(16, field);
            Assertion.AreEqual(16, staticField);

            anony.CallRepeat(2, out field, out staticField);
            Assertion.AreEqual(20, field);
            Assertion.AreEqual(20, staticField);

            anony.CallRepeat(1, out field);
            Assertion.AreEqual(23, field);

            List<int> list = new List<int> { 43, 5, 7, 8, 9, 2, 200 };
            anony.Lessthan(list, 40);
            Assertion.AreEqual(5, list.Count);
            anony.Lessthan(list, 5);
            Assertion.AreEqual(2, list.Count);
            anony.Lessthan(list, 1);
            Assertion.AreEqual(0, list.Count);

            List<int> list2 = new List<int> { 43, 5, 7, 8, 9, 2, 200 };
            anony.LessthanField(list2);
            Assertion.AreEqual(5, list2.Count);
            anony.Lessthan5(list2);
            Assertion.AreEqual(2, list2.Count);

            AnonymousClass2 anony2 = new AnonymousClass2();
            anony2.CallRepeat(3, out field);
            Assertion.AreEqual(15, field);

            Redirect.AnonymousClass a = new Redirect.AnonymousClass();
            AnonymousClass b = new AnonymousClass();
            Assertion.AreEqual(sum_of_enumerator(a.Generator()), sum_of_enumerator(b.Generator()));
            for (int i = 0; i < 10; i++)
            {
                Assertion.AreEqual(sum_of_enumerator(a.Generator(i)), sum_of_enumerator(b.Generator(i)));
            }

            Assertion.AreEqual(sum_of_enumerator(a.GetEnumerable().GetEnumerator()),
                sum_of_enumerator(b.GetEnumerable().GetEnumerator()));
        }


        static public void Conv_R_Un()
        {
            Assertion.AreEqual(BaseTest.Conv_R_Un(uint.MaxValue), Redirect.BaseTest.Conv_R_Un(uint.MaxValue));
            Assertion.AreEqual(BaseTest.Conv_R_Un(ulong.MaxValue), Redirect.BaseTest.Conv_R_Un(ulong.MaxValue));
        }


        static public void NaNFloatBranch()
        {
            Assertion.AreEqual(BaseTest.Blt_Un(float.NaN, float.NaN), Redirect.BaseTest.Blt_Un(float.NaN, float.NaN));
            Assertion.AreEqual(BaseTest.Blt_Un(1, float.NaN), Redirect.BaseTest.Blt_Un(1, float.NaN));
            Assertion.AreEqual(BaseTest.Blt_Un(float.NaN, 1), Redirect.BaseTest.Blt_Un(float.NaN, 1));
            Assertion.AreEqual(BaseTest.Bgt_Un(float.NaN, float.NaN), Redirect.BaseTest.Bgt_Un(float.NaN, float.NaN));
            Assertion.AreEqual(BaseTest.Bgt_Un(1, float.NaN), Redirect.BaseTest.Bgt_Un(1, float.NaN));
            Assertion.AreEqual(BaseTest.Bgt_Un(float.NaN, 1), Redirect.BaseTest.Bgt_Un(float.NaN, 1));
        }

        //TODO: Conv_U2 Ble_Un Conv_R8 Conv_R4 Bge_Un Conv_I2 Conv_Ovf_I2 Conv_Ovf_I2_Un
        //Conv_U4 Conv_Ovf_U4 Conv_Ovf_U4_Un
        //Conv_I1 Conv_Ovf_I1 Conv_Ovf_I1_Un
    }
}