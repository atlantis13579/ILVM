using UnrealBuildTool;
using System.IO;

public class ILVM : ModuleRules
{
    public ILVM(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        PublicDependencyModuleNames.AddRange(new[] { "Core", "CoreUObject", "Engine" });
        PrivateDependencyModuleNames.AddRange(new[] { "Projects", "Json" });
        PrivateDefinitions.Add("ILVM_WITH_UNREAL=1");
        if (Target.Platform != UnrealTargetPlatform.Win64)
            throw new BuildException("ILVM currently supports Win64 only.");

        foreach (string DirectoryName in new[] { "Managed", "DotNet" })
        {
            string DirectoryPath = Path.Combine(PluginDirectory, "Binaries", DirectoryName);
            if (Directory.Exists(DirectoryPath))
                foreach (string FilePath in Directory.GetFiles(DirectoryPath, "*", SearchOption.AllDirectories))
                    RuntimeDependencies.Add(FilePath, StagedFileType.NonUFS);
        }
    }
}
