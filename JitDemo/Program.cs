using System;
using System.IO;
using System.Text.Json;
using ILVM.Runtime;

namespace JitDemo
{
    class Program
    {
        static int Main(string[] args)
        {
            long sessionId = 0;
            try
            {
                if (args.Length != 0 && args.Length != 3)
                    throw new ArgumentException("Usage: JitDemo [assembly-path method arguments-json]");
                string path = args.Length == 0 ? Path.Combine(AppContext.BaseDirectory, "Hotfix", "Game.Hotfix.dll") : Path.GetFullPath(args[0]);
                string method = args.Length == 0 ? "Game.Hotfix.Entry::Add" : args[1];
                using var arguments = JsonDocument.Parse(args.Length == 0 ? "[3,2]" : args[2]);
                sessionId = Request(new { operation = "create" }).GetInt64();
                Request(new { operation = "load", sessionId, assemblyPath = path });
                var result = Request(new { operation = "invoke", sessionId, method, arguments = arguments.RootElement });
                Console.WriteLine(method + " = " + result.GetRawText());
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.Message);
                return 1;
            }
            finally
            {
                if (sessionId != 0) NativeBridge.Process(JsonSerializer.Serialize(new { operation = "destroy", sessionId }));
            }
        }

        static JsonElement Request(object request)
        {
            using var response = JsonDocument.Parse(NativeBridge.Process(JsonSerializer.Serialize(request)));
            if (!response.RootElement.GetProperty("ok").GetBoolean())
                throw new InvalidOperationException(response.RootElement.GetProperty("error").GetString());
            return response.RootElement.GetProperty("result").Clone();
        }
    }
}
