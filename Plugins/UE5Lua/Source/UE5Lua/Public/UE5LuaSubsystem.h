#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "UE5LuaSubsystem.generated.h"

namespace UE5Lua { class ExecutorLua; }

UENUM(BlueprintType)
enum class EUE5LuaValueType : uint8
{
    Nil, Integer, Number, Boolean, String
};

USTRUCT(BlueprintType)
struct UE5LUA_API FUE5LuaValue
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Lua")
    EUE5LuaValueType Type = EUE5LuaValueType::Nil;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Lua")
    int64 IntegerValue = 0;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Lua")
    double NumberValue = 0;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Lua")
    bool BooleanValue = false;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Lua")
    FString StringValue;
};

UCLASS()
class UE5LUA_API UUE5LuaSubsystem : public UGameInstanceSubsystem
{
    GENERATED_BODY()

public:
    virtual ~UUE5LuaSubsystem() override;

    // Relative paths are resolved against Content/Lua; failures preserve the previous state.
    UFUNCTION(BlueprintCallable, Category = "Lua")
    bool LoadScript(const FString& ScriptPath, FString& Error);

    UFUNCTION(BlueprintCallable, Category = "Lua")
    bool LoadSource(const FString& Source, FString& Error);

    UFUNCTION(BlueprintCallable, Category = "Lua")
    bool Execute(const FString& Function, const TArray<FUE5LuaValue>& Arguments, FUE5LuaValue& Result, FString& Error);

    UFUNCTION(BlueprintCallable, Category = "Lua")
    bool ExecuteInt(const FString& Function, const TArray<int64>& Arguments, int64& Result, FString& Error);

    UFUNCTION(BlueprintCallable, Category = "Lua")
    void UnloadScript();

    virtual void Deinitialize() override;

private:
    UE5Lua::ExecutorLua* Runtime = nullptr;
};
