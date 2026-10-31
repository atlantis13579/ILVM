using UnrealBuildTool;

public class UE5Lua : ModuleRules
{
    public UE5Lua(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        PublicDependencyModuleNames.AddRange(new[] { "Core", "CoreUObject", "Engine" });
        PrivateDependencyModuleNames.Add("UE5LuaRuntime");
    }
}
