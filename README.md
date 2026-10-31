# ILVM

A C# hot update plugin for UE5 that preserves the original InjectFix/ILVM software interpreter. The current integration target is **UE5 / Windows x64**, and the UE interfaces were checked against the local UE **5.4.4** source. The managed part uses **.NET 8**, with the Mono.Cecil dependency restored from NuGet.

Call chain: `UE5 GameInstanceSubsystem → C++ hostfxr host → ILVM.Runtime → IL translator → C# software interpreter`.

Standalone managed tests and native host tests are complete; per project conventions, the UE5 project was not compiled, and no editor PIE or packaged-run validation was performed.

## Building and testing

Environment: Windows x64, Visual Studio 2022 (including the C++ toolchain), CMake, .NET SDK 8/9, and .NET 8 x64 Runtime. `ILVM.sln` is the VS2022 solution containing the C# projects and the `UE5Lua` native project; the native project uses the v143 toolset.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The script builds `ILVM.sln` with VS2022 MSBuild, then builds the `UE5Lua` static library and runs the C#, Lua, and native host regressions. Managed dependencies are published to `Plugins/ILVM/Binaries/Managed`, and the Lua static library to `Plugins/UE5Lua/Source/ThirdParty`. The script **does not invoke UnrealBuildTool or build UE5**. Use `-Configuration Debug` to validate Debug; use `-SkipTests` to only prepare plugin dependencies.

You can also bundle the .NET 8 runtime from the current machine into the plugin so target machines do not need a separate install:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -BundleRuntime
```

The runtime is stored in `Plugins/ILVM/Binaries/DotNet`. `-DotNetRoot` specifies the x64 dotnet install path; `-CMakeGenerator` specifies an installed Visual Studio CMake generator. Generated binaries and test artifacts are not added to Git.

To validate the managed interpreter on its own:

```powershell
dotnet run --project Tests/ILVM.Tests -c Release
dotnet run --project JitDemo -c Release
```

The default demo prints `Game.Hotfix.Entry::Add = 5` and requires no original Mono installation, `ILVM.dll`, debug symbols, or fixed working directory. To specify the assembly and method:

```powershell
dotnet run --project JitDemo -c Release -- Samples/Hotfix/bin/Release/net8.0/Game.Hotfix.dll Game.Hotfix.Entry::Add '[3,2]'
```

## Integrating with UE5

1. Run the build script above and copy the full `Plugins/ILVM` directory to your game project's `Plugins/ILVM`, including the generated `Binaries/Managed` and the optional `Binaries/DotNet`.
2. Enable `{ "Name": "ILVM", "Enabled": true }` in the `Plugins` array of the `.uproject`. If calling from C++, add `ILVM` to your game module's `Build.cs` dependencies.
3. Build `Samples/Hotfix/Hotfix.csproj` and place the generated `Game.Hotfix.dll` into the game's `Content/Hotfix`.
4. In Blueprint, get the `ILVMSubsystem` Game Instance Subsystem, call `LoadHotfix("Game.Hotfix.dll")`, then call `ExecuteInt("Game.Hotfix.Entry::Add", [3, 2])`. The expected result is `5`. Read `Error` when a node returns `false`.

C++ call example:

```cpp
#include "ILVMSubsystem.h"
#include "Engine/GameInstance.h"

UILVMSubsystem* VM = GameInstance->GetSubsystem<UILVMSubsystem>();
FString Error;
int32 Result = 0;
if (VM->LoadHotfix(TEXT("Game.Hotfix.dll"), Error))
{
    VM->ExecuteInt(TEXT("Game.Hotfix.Entry::Add"), {3, 2}, Result, Error);
}
```

The plugin's managed dependencies and optional runtime are packaged as NonUFS files through `RuntimeDependencies`. The game's hot update DLL must also be a plain file on disk; add the following to `Config/DefaultGame.ini`:

```ini
[/Script/UnrealEd.ProjectPackagingSettings]
+DirectoriesToAlwaysStageAsNonUFS=(Path="Hotfix")
```

To update, write the new DLL to an accessible location and call `LoadHotfix` again. The path may be absolute or relative to `Content/Hotfix`. An assembly with the same name and version can also be replaced; if loading fails the current VM is retained, and if it succeeds a brand-new script state is created. **Old objects, old delegates, and static fields are not migrated to the new version.** Once the old callbacks are no longer used, the old assembly can be collected by the GC.

The host searches in order: the plugin's bundled `Binaries/DotNet`, `ILVM_DOTNET_ROOT`, `DOTNET_ROOT_X64`, `DOTNET_ROOT`, `Program Files/dotnet`. All UE calls execute on the game thread; each GameInstance has an independent session that is destroyed automatically when it ends.

## Parameters and return values

`ExecuteInt` works with int32 parameters and return values. The generic Blueprint node `Execute` accepts a JSON parameter array and returns a full JSON response, e.g. `{"ok":true,"result":5}`.

| Parameter | JSON example |
| --- | --- |
| int32, string, bool, null | `[3,"hello",true,null]` |
| float / double | `[{"type":"single","value":1.5},{"type":"double","value":2.0}]` |
| 64-bit integers | `[{"type":"int64","value":"9007199254740993"}]` |
| uint32 / uint64 | `[{"type":"uint32","value":4294967295}]` |
| int32 array | `[{"type":"int32[]","value":[1,2,3]}]` |

int64/uint64 return values use `{"type":"int64","value":"..."}` to avoid precision loss from UE JSON's double number representation. Other return values must be JSON-serializable. Arbitrary managed objects, pointers, and ref/out parameters are not currently passed across the native boundary; ref/out inside scripts is covered by regression tests.

When overloads exist, use the full Cecil signature, e.g. `System.Int32 Game.Hotfix.Entry::Overload(System.Int32)`; using the short name alone reports an ambiguity error. Callable signatures can be inspected via managed `ExecutorVM.MethodNames` or the native protocol's `describe` operation.

## Interpretation scope

- Hot update entry points and translatable method bodies are executed by the software interpreter; recursion, virtual calls, reflection entry points, and delegates return to the interpreter through generated wrapper methods.
- The CLR provides objects, GC, the base class library, and interop. Constructors, open generic methods, and external libraries still execute through the CLR, so this plugin is not a complete standalone CLR and does not provide JIT-free / AOT platform support.
- The sample pins C# 7.3 to avoid new compiler syntax introducing unvalidated IL. Async state machines, arbitrary generic hot updates, and the full C# language feature set are outside the current validation scope.
- This repository originally had no UE module or UObject binding generator; the new plugin provides Blueprint/C++ entry points but does not yet provide automatic UObject/UFUNCTION C# bindings.
- The C++ IL interpreter under `ILVM/` remains a historical unfinished prototype and does not participate in the new build. The C# translator and software interpreter sources and namespaces have been unified under `UE5Jit`; Lua sources and the demo project have been unified under `UE5Lua`.

## Validation

- All 41 original tests pass: arithmetic, 64-bit integers, floating point/NaN, arrays, references, structs, interfaces, virtual calls, exceptions/finally, closures, and more; assertions are also active in Release.
- New validation covers different parameters/return values, recursion and callbacks actually entering the interpreter, overload selection, missing methods, bad arguments, stack recovery after consecutive exceptions, bad DLL load rollback, repeated replacement of same-named assemblies, and old assembly collection.
- A standalone C++ test reuses the plugin's `DotNetHost.cpp` to validate C++ → hostfxr → managed bridge → interpreter, plus v1 → v2 hot reload and session destruction; it can run from a working directory outside the repository.
- UE API static checks reference the UE 5.4.4 source. UE module compilation, PIE, Shipping packaging, and other UE5 versions still need validation in the target project.

The native hosting interface follows the [.NET native hosting documentation](https://learn.microsoft.com/en-us/dotnet/core/tutorials/netcore-hosting). Third-party licenses: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## UE5Lua

`Plugins/UE5Lua` is a standalone UE5 Win64 plugin that does not depend on the C# plugin or .NET. It reuses the in-repo Lua 5.4.3 sources and `UE5Lua/ExecutorLua.cpp`, integrated through a static library. To build and validate it separately:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-lua.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-lua.ps1 -Configuration Debug
```

CMake and the Visual Studio 2022 C++ toolchain are required. The script generates `artifacts/lua/<config>/UE5Lua.exe` and the static library, and copies headers, libs, and licenses to the plugin's `Source/ThirdParty/UE5LuaRuntime`. UE uses the Release library by default; the Debug library is used only in Debug when `bDebugBuildsActuallyUseDebugCRT=true`. The old Visual Studio project was also renamed to `UE5Lua/UE5Lua.vcxproj`, updated to v143 / C++17, and can build the demo independently.

1. After building Release, copy the full `Plugins/UE5Lua` to the game project, including the generated `Source/ThirdParty`. Enable `{ "Name": "UE5Lua", "Enabled": true }` in the `.uproject`.
2. Place `UE5Lua/test.lua` into the game's `Content/Lua/test.lua`.
3. In Blueprint, get the `UE5LuaSubsystem` Game Instance Subsystem, call `LoadScript("test.lua")`, then `ExecuteInt("Add", [1, 2])`, which should return `3`. Check the bool return value and `Error`.

When calling from C++, add `UE5Lua` to your game module dependencies:

```cpp
#include "UE5LuaSubsystem.h"
#include "Engine/GameInstance.h"

UUE5LuaSubsystem* Lua = GameInstance->GetSubsystem<UUE5LuaSubsystem>();
FString Error;
int64 Result = 0;
if (Lua->LoadScript(TEXT("test.lua"), Error))
{
    Lua->ExecuteInt(TEXT("Add"), {1, 2}, Result, Error);
}
```

The generic `Execute` node passes nil, int64, double, bool, and strings via `FUE5LuaValue` and returns the first result; tables, functions, and userdata are not currently passed across the UE boundary. `LoadSource` can execute a source string directly; `LoadScript` accepts an absolute path or a path relative to `Content/Lua`. `require` searches the script directory's `?.lua` and `?/init.lua` first; on Windows, Lua's built-in file functions use the system narrow-character path, so ASCII paths are recommended for module search directories.

Reloading creates a new Lua state, clearing previous globals and the module cache; syntax errors or initialization errors do not replace the existing state. Each GameInstance is independent, calls are restricted to the game thread, and the state is released by `UnloadScript` or when the GameInstance ends. Automatic UObject binding is not included.

Add the following packaging entry to `Config/DefaultGame.ini` so scripts can be read and replaced directly at runtime:

```ini
[/Script/UnrealEd.ProjectPackagingSettings]
+DirectoriesToAlwaysStageAsNonUFS=(Path="Lua")
```

The Lua regression includes 16 groups of checks and 10,000 error recovery loops, run in both Release and Debug; the original benchmark can be run with `artifacts/lua/Release/UE5Lua.exe UE5Lua/test.lua --benchmark`. The standalone test links the same static library as the UE5 plugin; the UE5 module has only had its source interfaces checked and has not been compiled, run in PIE, or validated in a packaged build. The Lua C API follows the [Lua 5.4 reference manual](https://www.lua.org/manual/5.4/manual.html).
