using System;
using System.IO;
using UE4Jit;
using System.Collections.Generic;

namespace JitDemo
{
    class Program
    {
        static unsafe void Main(string[] args)
        {
            // LuaBenchmark();

            Console.WriteLine($"Running C#...");

            string Dll = CompileDll();
            if (Dll.Length > 0)
            {
                ExecutorVM exec = new ExecutorVM();
                exec.Load(Dll);

                exec.GetVM().InitExecuter();

                var Ret = exec.Execute<int>("CRBGame.HotfixTest::Add", new object[] { 3, 2 });
                Console.WriteLine($"Add({3}, {2}) = {Ret}");

                RunTestVM(exec, "FileVMBuildBaseTest");
                // RunUnitTestVM(exec);

                // CSBenchmark(exec);

                InjectDll(Dll);
            }

            Console.ReadKey();
        }

        static string CompileDll()
        {
            Complier c = new Complier();

            string Path = Directory.GetCurrentDirectory() + "/../../";
            List<string> SrcFiles = new List<string>();
            SrcFiles.Add(Path + "JitDemo.cs");
            SrcFiles.Add(Path + "Test/UnitTest.cs");
            SrcFiles.Add(Path + "Test/BaseTest.cs");
            SrcFiles.Add(Path + "Test/RedirectBaseTest.cs");
            bool success = c.CompileAsDll(SrcFiles, "Jit.dll");

            if (!success)
            {
                return "";
            }

            return c.AssemblyPath;
        }

        static void CSBenchmark(ExecutorVM exec)
        {
            int Ret = 0;
            long start = DateTime.Now.Ticks;
            int Count = 1;
            for (int i = 0; i < Count; ++i)
            {
                Ret = exec.Execute<int>("CRBGame.HotfixTest::Benchmark");
            }
            TimeSpan elapsedSpan = new TimeSpan(DateTime.Now.Ticks - start);
            Console.WriteLine($"R = {Ret}, TimeSpan = {elapsedSpan.TotalMilliseconds / Count} ms");
        }

        static void LuaBenchmark()
        {
#if DEBUG
            string LuaExec = Directory.GetCurrentDirectory() + "/../../../UE4Lua/x64/Debug/UE4Lua.exe";
#else
            string LuaExec = Directory.GetCurrentDirectory() + "/../../../UE4Lua/x64/Release/UE4Lua.exe";
#endif
            if (!File.Exists(LuaExec))
            {
                Console.WriteLine($"Cannot find Lua exe: {LuaExec}");
                return;
            }

            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.FileName = LuaExec;
            p.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory() + "/../../../UE4Lua";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            Console.WriteLine(output);

            return;
        }

        static void InjectDll(string Path)
        {
            string Dll = Directory.GetCurrentDirectory() + "/../../../../Plugins/MonoRuntime/Content/CSharpUE4.Windows.dll";

            Injector j = new Injector(Dll);
            j.Inject(Path);

            return;
        }

        static void RunUnitTestCS()
        {
            Console.WriteLine($"----- Unit Test Begin -----");
            IFix.Test.VirtualMachineTest.RunAll();
            Console.WriteLine($"----- Unit Test End -----");
        }

        static void RunTestVM(ExecutorVM exec, string Name)
        {
            Console.WriteLine($"Running {Name}");
            string func = $"IFix.Test.VirtualMachineTest::{Name}";
            exec.Execute(func);
        }

        static void RunUnitTestVM(ExecutorVM exec)
        {
            Console.WriteLine($"----- Unit Test Begin -----");
            RunTestVM(exec, "FileVMBuildBaseTest");
            RunTestVM(exec, "RefBase");
            RunTestVM(exec, "ExceptionBase");
            RunTestVM(exec, "LeavePoint");
            RunTestVM(exec, "TryCatchFinally");
            RunTestVM(exec, "CatchByNextLevel");
            RunTestVM(exec, "ClassBase");
            RunTestVM(exec, "StructBase");
            RunTestVM(exec, "PassByValue");
            RunTestVM(exec, "VirtualFunc");
            RunTestVM(exec, "InterfaceTest");
            RunTestVM(exec, "VirtualFuncOfStruct");
            RunTestVM(exec, "ItfWithRefParam");
            RunTestVM(exec, "LdTokenBase");
            RunTestVM(exec, "UnboxBase");
            RunTestVM(exec, "GenericOverload");
            RunTestVM(exec, "StaticFieldBase");
            RunTestVM(exec, "ConvI4Base");
            RunTestVM(exec, "LdLen");
            RunTestVM(exec, "Newarr");
            RunTestVM(exec, "Cast");
            RunTestVM(exec, "Array");
            RunTestVM(exec, "LogicalOperator");
            RunTestVM(exec, "Ldflda");
            RunTestVM(exec, "Conv_Ovf_I");
            RunTestVM(exec, "Ceq");
            RunTestVM(exec, "BitsOp");
            RunTestVM(exec, "Conv_U1");
            RunTestVM(exec, "Ldelema");
            RunTestVM(exec, "Bgt");
            RunTestVM(exec, "Ldsflda");
            RunTestVM(exec, "Initobj");
            RunTestVM(exec, "Arithmetic");
            RunTestVM(exec, "NaNFloat");
            RunTestVM(exec, "Rem");
            RunTestVM(exec, "Ldc_R8");
            RunTestVM(exec, "Ldc_I8");
            RunTestVM(exec, "Int64");
            RunTestVM(exec, "Closure");
            RunTestVM(exec, "Conv_R_Un");
            RunTestVM(exec, "NaNFloatBranch");
            Console.WriteLine($"----- Unit Test End -----");
        }
    }
}
