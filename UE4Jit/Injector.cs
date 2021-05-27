using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace UE4Jit
{
    class Injector
    {
        private string AssemblyPath;

        public Injector(string _Dll)
        {
            AssemblyPath = _Dll;
        }

        static Dictionary<string, MethodDefinition> LoadSymbol(string Assembly)
        {
            var assembly = AssemblyDefinition.ReadAssembly(Assembly, new ReaderParameters { ReadSymbols = true });
            if (assembly == null)
            {
                return null;
            }

            Dictionary<string, MethodDefinition> dict = new Dictionary<string, MethodDefinition>();
            foreach (var type in assembly.MainModule.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (method.IsConstructor || method.IsGetter || method.IsSetter)
                        continue;
                    if (dict.ContainsKey(method.FullName))
                    {
                        Console.WriteLine($"Symbol already exist: {method.FullName}");
                        continue;
                    }
                    dict.Add(method.FullName, method);
                }
            }

            return dict;
        }

        public bool Inject(string InjectAssembly)
        {
            if (!File.Exists(AssemblyPath))
            {
                Console.WriteLine($"File not exist: {AssemblyPath}");
                return false;
            }

            Mono.Cecil.AssemblyDefinition assembly = null;
            Dictionary<string, MethodDefinition> InjectDict = null;

            try
            {
                assembly = AssemblyDefinition.ReadAssembly(AssemblyPath, new ReaderParameters { ReadSymbols = true });
                if (assembly == null)
                {
                    Console.WriteLine($"Inject Load assembly failed: {AssemblyPath}");
                    return false;
                }

                InjectDict = LoadSymbol(InjectAssembly);
                if (InjectDict == null)
                {
                    Console.WriteLine($"Inject Load assembly failed: {AssemblyPath}");
                    return false;
                }

                int InjectCount = 0;
                var module = assembly.MainModule;
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.IsConstructor || method.IsGetter || method.IsSetter)
                            continue;
                        if (!InjectDict.ContainsKey(method.FullName))
                            continue;
                        InjectCount += InjectMethod(method, InjectDict[method.FullName]);
                    }
                }

                if (InjectCount > 0)
                {
                    assembly.Write(AssemblyPath, new WriterParameters { WriteSymbols = true });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Inject failed: {ex}");
                throw;
            }
            finally
            {
                if (assembly.MainModule.SymbolReader != null)
                {
                    // Console.WriteLine("Inject SymbolReader.Dispose Succeed");
                    assembly.MainModule.SymbolReader.Dispose();
                }
            }

            Console.WriteLine($"Inject Success: {InjectAssembly} ==> {AssemblyPath}");
            return true;
        }

        private bool IsSameIL(MethodBody method, MethodBody method_inject)
        {
            return method.ToString() == method_inject.ToString();
        }

        private int InjectMethod(MethodDefinition method, MethodDefinition method_inject)
        {
            if (IsSameIL(method.Body, method_inject.Body))
            {
                return 0;
            }
            method.Body = method_inject.Body;
            return 1;
        }

        private void InjectMethod_Add(MethodDefinition method)
        {
            /*
            public static int Add(int a, int b)
            {
                return a + b;
            }
            */

            var insertPoint = method.Body.Instructions[0];
            var ilProcessor = method.Body.GetILProcessor();
            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldarg_0));
            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldarg_1));
            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Add));
            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ret));
        }

    }
}