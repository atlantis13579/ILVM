#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "ILVMSubsystem.generated.h"

class FJsonObject;

UCLASS()
class ILVM_API UILVMSubsystem : public UGameInstanceSubsystem
{
    GENERATED_BODY()

public:
    // Relative paths are resolved against Content/Hotfix. Failed loads preserve the current VM.
    UFUNCTION(BlueprintCallable, Category = "ILVM")
    bool LoadHotfix(const FString& AssemblyPath, FString& Error);

    // ArgumentsJson is an array; floating-point and 64-bit values use explicit type tags.
    UFUNCTION(BlueprintCallable, Category = "ILVM")
    bool Execute(const FString& Method, const FString& ArgumentsJson, FString& ResultJson, FString& Error);

    UFUNCTION(BlueprintCallable, Category = "ILVM")
    bool ExecuteInt(const FString& Method, const TArray<int32>& Arguments, int32& Result, FString& Error);

    UFUNCTION(BlueprintCallable, Category = "ILVM")
    void UnloadHotfix();

    virtual void Deinitialize() override;

private:
    bool Send(const TSharedRef<FJsonObject>& Request, TSharedPtr<FJsonObject>& Response, FString& Error);
    int64 SessionId = 0;
};
