# UE5Lua

Independent Win64 Lua plugin for Unreal Engine 5. The UE integration is checked
against UE 5.4.4 source; the Unreal module has not been compiled or run in PIE.

Before copying this plugin into a game project, run `build-lua.ps1` from the ILVM
repository root. Copy the entire plugin, including the generated
`Source/ThirdParty/UE5LuaRuntime` headers, library and Lua license.

Enable the `UE5Lua` plugin, put a UTF-8 Lua source file in `Content/Lua`, and use
the `UE5LuaSubsystem` Game Instance Subsystem:

- `LoadScript`: load or replace a script; a failed load preserves the previous state.
- `LoadSource`: load a source string.
- `ExecuteInt`: call a global function with int64 arguments and result.
- `Execute`: call a global function using typed nil/integer/number/boolean/string values.
- `UnloadScript`: release the current state.

All calls run on the game thread. Each GameInstance owns an independent Lua state.
Add `Lua` to `DirectoriesToAlwaysStageAsNonUFS` in the project's packaging settings.
The plugin links Lua statically and does not need .NET or a Lua DLL.
