using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using UE5Jit;

namespace ILVM.Runtime
{
    public static unsafe class NativeBridge
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<long, ExecutorVM> Sessions = new Dictionary<long, ExecutorVM>();
        private static long nextSession;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int Dispatch(byte* request, IntPtr* response)
        {
            if (response == null) return -1;
            *response = IntPtr.Zero;
            try
            {
                string json = Process(Marshal.PtrToStringUTF8((IntPtr)request));
                *response = Marshal.StringToCoTaskMemUTF8(json);
                return 0;
            }
            catch
            {
                // Managed exceptions must never cross the native ABI.
                return -1;
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static void FreeResponse(IntPtr response)
        {
            Marshal.FreeCoTaskMem(response);
        }

        public static string Process(string request)
        {
            lock (Sync)
            {
                try
                {
                    using var document = JsonDocument.Parse(request);
                    var root = document.RootElement;
                    string operation = root.GetProperty("operation").GetString();
                    object result;
                    if (operation == "create")
                    {
                        long id = ++nextSession;
                        Sessions.Add(id, new ExecutorVM());
                        result = id;
                    }
                    else
                    {
                        long id = root.GetProperty("sessionId").GetInt64();
                        if (!Sessions.TryGetValue(id, out var vm))
                            throw new InvalidOperationException("Unknown ILVM session: " + id);
                        switch (operation)
                        {
                            case "load":
                                vm.Load(root.GetProperty("assemblyPath").GetString());
                                result = vm.MethodNames.Length;
                                break;
                            case "invoke":
                                var values = new List<object>();
                                if (root.TryGetProperty("arguments", out var arguments))
                                    foreach (var argument in arguments.EnumerateArray()) values.Add(ReadArgument(argument));
                                result = vm.Invoke(root.GetProperty("method").GetString(), values.ToArray());
                                break;
                            case "describe":
                                result = new { methods = vm.MethodNames, interpretedCalls = vm.GetVM()?.InterpretedCallCount ?? 0 };
                                break;
                            case "destroy":
                                Sessions.Remove(id);
                                vm.Dispose();
                                result = null;
                                break;
                            default:
                                throw new ArgumentException("Unknown ILVM operation: " + operation);
                        }
                    }
                    // Preserve all 64 bits when UE parses JSON numbers as doubles.
                    if (operation == "invoke" && result is long signed)
                        result = new { type = "int64", value = signed.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                    else if (operation == "invoke" && result is ulong unsigned)
                        result = new { type = "uint64", value = unsigned.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                    return JsonSerializer.Serialize(new { ok = true, result });
                }
                catch (Exception error)
                {
                    return JsonSerializer.Serialize(new { ok = false, error = error.ToString() });
                }
            }
        }

        private static object ReadArgument(JsonElement argument)
        {
            if (argument.ValueKind == JsonValueKind.Null) return null;
            if (argument.ValueKind == JsonValueKind.String) return argument.GetString();
            if (argument.ValueKind == JsonValueKind.True || argument.ValueKind == JsonValueKind.False) return argument.GetBoolean();
            if (argument.ValueKind == JsonValueKind.Number) return argument.GetInt32();
            string type = argument.GetProperty("type").GetString();
            var value = argument.GetProperty("value");
            switch (type)
            {
                case "int32": return value.GetInt32();
                case "uint32": return value.GetUInt32();
                case "int64": return value.ValueKind == JsonValueKind.String ? long.Parse(value.GetString(), System.Globalization.CultureInfo.InvariantCulture) : value.GetInt64();
                case "uint64": return value.ValueKind == JsonValueKind.String ? ulong.Parse(value.GetString(), System.Globalization.CultureInfo.InvariantCulture) : value.GetUInt64();
                case "single": return value.GetSingle();
                case "double": return value.GetDouble();
                case "boolean": return value.GetBoolean();
                case "string": return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
                case "int32[]": return JsonSerializer.Deserialize<int[]>(value.GetRawText());
                default: throw new NotSupportedException("Unsupported argument type: " + type);
            }
        }
    }
}
