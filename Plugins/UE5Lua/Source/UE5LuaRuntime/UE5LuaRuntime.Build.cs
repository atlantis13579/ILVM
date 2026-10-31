using UnrealBuildTool;
using System.IO;

public class UE5LuaRuntime : ModuleRules
{
    public UE5LuaRuntime(ReadOnlyTargetRules Target) : base(Target)
    {
        Type = ModuleType.External;
        if (Target.Platform != UnrealTargetPlatform.Win64)
            throw new BuildException("UE5Lua currently supports Win64 only.");
        string Root = Path.GetFullPath(Path.Combine(ModuleDirectory, "../ThirdParty/UE5LuaRuntime"));
        string Configuration = Target.Configuration == UnrealTargetConfiguration.Debug && Target.bDebugBuildsActuallyUseDebugCRT ? "Debug" : "Release";
        string Library = Path.Combine(Root, "lib", "Win64", Configuration, "UE5LuaRuntime.lib");
        if (!File.Exists(Library) || !File.Exists(Path.Combine(Root, "include", "ExecutorLua.h")))
            throw new BuildException("Prepare UE5Lua first: run build-lua.ps1 -Configuration " + Configuration + " in the ILVM repository, then copy the plugin including Source/ThirdParty.");
        PublicSystemIncludePaths.Add(Path.Combine(Root, "include"));
        PublicAdditionalLibraries.Add(Library);
    }
}
