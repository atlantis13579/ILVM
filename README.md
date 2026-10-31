# ILVM

A C# hot update plugin for UE5 that preserves the original InjectFix/ILVM software
interpreter. The current integration target is **UE5 / Windows x64**, and the UE
interfaces were checked against the local UE **5.4.4** source. The managed part
uses **.NET 8**, with the Mono.Cecil dependency restored from NuGet.

Call chain: `UE5 GameInstanceSubsystem → C++ hostfxr host → ILVM.Runtime → IL translator → C# software interpreter`.

## Interpretation scope

- Hot update entry points and translatable method bodies are executed by the software interpreter; recursion, virtual calls, reflection entry points, and delegates return to the interpreter through generated wrapper methods.
- The CLR provides objects, GC, the base class library, and interop. Constructors, open generic methods, and external libraries still execute through the CLR, so this plugin is not a complete standalone CLR and does not provide JIT-free / AOT platform support.
- The sample pins C# 7.3 to avoid new compiler syntax introducing unvalidated IL. Async state machines, arbitrary generic hot updates, and the full C# language feature set are outside the current validation scope.
- This repository originally had no UE module or UObject binding generator; the new plugin provides Blueprint/C++ entry points but does not yet provide automatic UObject/UFUNCTION C# bindings.
- The C++ IL interpreter under `ILVM/` remains a historical unfinished prototype and does not participate in the new build. The C# translator and software interpreter sources and namespaces have been unified under `UE5Jit`; Lua sources and the demo project have been unified under `UE5Lua`.

Reloading replaces the assembly with the same name and version and creates a brand-new script state. **Old objects, old delegates, and static fields are not migrated to the new version.** Once the old callbacks are no longer used, the old assembly can be collected by the GC. If loading fails, the current VM is retained.

The native hosting interface follows the [.NET native hosting documentation](https://learn.microsoft.com/en-us/dotnet/core/tutorials/netcore-hosting). Third-party licenses: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## UE5Lua

`Plugins/UE5Lua` is a standalone UE5 Win64 plugin that does not depend on the C#
plugin or .NET. It reuses the in-repo Lua 5.4.3 sources and
`UE5Lua/ExecutorLua.cpp`, integrated through a static library.

The generic `Execute` node passes nil, int64, double, bool, and strings via
`FUE5LuaValue` and returns the first result; tables, functions, and userdata are
not currently passed across the UE boundary. Reloading creates a new Lua state,
clearing previous globals and the module cache; syntax errors or initialization
errors do not replace the existing state. Each GameInstance is independent, calls
are restricted to the game thread, and the state is released when the GameInstance
ends. Automatic UObject binding is not included.

The Lua C API follows the [Lua 5.4 reference manual](https://www.lua.org/manual/5.4/manual.html).
