using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CSharp;
using System.Collections.Generic;
using System.CodeDom.Compiler;

namespace UE4Jit
{
    public class Complier
    {
        public System.Reflection.Assembly CompiledAssembly;
        public string AssemblyPath;

        public Complier()
        {
        }

        System.CodeDom.Compiler.CompilerParameters CreateComplierParameters(string AssemblyFile)
        {
            System.CodeDom.Compiler.CompilerParameters parameters = new CompilerParameters();
            parameters.CompilerOptions = "-optimize -unsafe";
            parameters.IncludeDebugInformation = true;
            parameters.GenerateExecutable = false;
            parameters.GenerateInMemory = AssemblyFile.Length == 0;
            parameters.OutputAssembly = AssemblyFile;
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Console.dll");
            return parameters;
        }

        private CompilerResults GenerateCodeFromSource(string[] SourceCode, string AssemblyFile)
        {
            CSharpCodeProvider codeProvider = new CSharpCodeProvider();
            System.CodeDom.Compiler.CompilerParameters parameters = CreateComplierParameters(AssemblyFile);
            CompilerResults results = codeProvider.CompileAssemblyFromSource(parameters, SourceCode);
            return results;
        }

        private CompilerResults GenerateCodeFromFiles(string[] SrcFiles, string AssemblyFile)
        {
            CSharpCodeProvider codeProvider = new CSharpCodeProvider();
            System.CodeDom.Compiler.CompilerParameters parameters = CreateComplierParameters(AssemblyFile);
            CompilerResults results = codeProvider.CompileAssemblyFromFile(parameters, SrcFiles);
            return results;
        }

        public bool CompileFromString(string[] SourceCode, string AssemblyFile)
        {
            CompilerResults results = GenerateCodeFromSource(SourceCode, AssemblyFile);

            if (results.Errors.Count > 0)
            {
                Console.WriteLine("Compile Error.");

                foreach (CompilerError CompErr in results.Errors)
                {
                    Console.WriteLine($"Line number {CompErr.Line}, Error Number: {CompErr.ErrorNumber}, {CompErr.ErrorText}");
                }

                return false;
            }

            Console.WriteLine("Compile Done.");
            CompiledAssembly = results.CompiledAssembly;
            AssemblyPath = results.PathToAssembly;
            return true;
        }

        public bool CompileFromFiles(string[] SrcFiles, string AssemblyFile)
        {
            CompilerResults results = GenerateCodeFromFiles(SrcFiles, AssemblyFile);

            if (results.Errors.Count > 0)
            {
                Console.WriteLine("Compile Error.");

                foreach (CompilerError CompErr in results.Errors)
                {
                    Console.WriteLine($"File {CompErr.FileName}, Line number {CompErr.Line}, Error Number: {CompErr.ErrorNumber}, {CompErr.ErrorText}");
                }

                return false;
            }

            Console.WriteLine("Compile Done.");
            CompiledAssembly = results.CompiledAssembly;
            AssemblyPath = results.PathToAssembly;
            return true;
        }

        public bool CompileAsDll(List<string> SrcFiles, string AssemblyFile)
        {
            foreach (string s in SrcFiles)
            {
                if (!File.Exists(s))
                {
                    return false;
                }
            }

            string[] Codes = new string[SrcFiles.Count];
            for (int i = 0; i < SrcFiles.Count; ++i)
            {
                Codes[i] = File.ReadAllText(SrcFiles[i]);
            }

            return CompileFromString(Codes, AssemblyFile);
        }
    }
}
