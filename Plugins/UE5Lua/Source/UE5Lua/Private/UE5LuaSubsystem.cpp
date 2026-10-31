#include "UE5LuaSubsystem.h"
#include "ExecutorLua.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"

namespace
{
    bool CheckGameThread(FString& Error)
    {
        Error.Reset();
        if (IsInGameThread()) return true;
        Error = TEXT("Lua subsystem calls must run on the game thread.");
        return false;
    }
}

UUE5LuaSubsystem::~UUE5LuaSubsystem()
{
    delete Runtime;
}

bool UUE5LuaSubsystem::LoadScript(const FString& ScriptPath, FString& Error)
{
    if (!CheckGameThread(Error)) return false;
    const FString FullPath = FPaths::ConvertRelativePathToFull(FPaths::IsRelative(ScriptPath)
        ? FPaths::Combine(FPaths::ProjectContentDir(), TEXT("Lua"), ScriptPath) : ScriptPath);
    TArray<uint8> Bytes;
    if (!FFileHelper::LoadFileToArray(Bytes, *FullPath))
    {
        Error = TEXT("Cannot read Lua script: ") + FullPath;
        return false;
    }
    if (!Runtime) Runtime = new UE5Lua::ExecutorLua();
    const std::string Source = Bytes.Num() ? std::string(reinterpret_cast<const char*>(Bytes.GetData()), Bytes.Num()) : std::string();
    std::string NativeError;
    const bool Success = Runtime->LoadSource(Source, TCHAR_TO_UTF8(*(TEXT("@") + FullPath)),
        TCHAR_TO_UTF8(*FPaths::GetPath(FullPath)), NativeError);
    Error = UTF8_TO_TCHAR(NativeError.c_str());
    return Success;
}

bool UUE5LuaSubsystem::LoadSource(const FString& Source, FString& Error)
{
    if (!CheckGameThread(Error)) return false;
    if (!Runtime) Runtime = new UE5Lua::ExecutorLua();
    std::string NativeError;
    const FString ModulePath = FPaths::ConvertRelativePathToFull(FPaths::Combine(FPaths::ProjectContentDir(), TEXT("Lua")));
    const bool Success = Runtime->LoadSource(TCHAR_TO_UTF8(*Source), "@UE5Lua", TCHAR_TO_UTF8(*ModulePath), NativeError);
    Error = UTF8_TO_TCHAR(NativeError.c_str());
    return Success;
}

bool UUE5LuaSubsystem::Execute(const FString& Function, const TArray<FUE5LuaValue>& Arguments, FUE5LuaValue& Result, FString& Error)
{
    Result = FUE5LuaValue{};
    if (!CheckGameThread(Error)) return false;
    if (!Runtime) { Error = TEXT("Load a Lua script before calling a function."); return false; }
    std::vector<UE5Lua::Value> NativeArguments;
    NativeArguments.reserve(Arguments.Num());
    for (const FUE5LuaValue& Argument : Arguments)
    {
        switch (Argument.Type)
        {
        case EUE5LuaValueType::Nil: NativeArguments.emplace_back(); break;
        case EUE5LuaValueType::Integer: NativeArguments.push_back(UE5Lua::Value::FromInteger(Argument.IntegerValue)); break;
        case EUE5LuaValueType::Number: NativeArguments.push_back(UE5Lua::Value::FromNumber(Argument.NumberValue)); break;
        case EUE5LuaValueType::Boolean: NativeArguments.push_back(UE5Lua::Value::FromBoolean(Argument.BooleanValue)); break;
        case EUE5LuaValueType::String:
        {
            const FTCHARToUTF8 Text(*Argument.StringValue, Argument.StringValue.Len());
            NativeArguments.push_back(UE5Lua::Value::FromString(std::string(Text.Get(), Text.Length())));
            break;
        }
        default: Error = TEXT("Invalid Lua argument type."); return false;
        }
    }
    UE5Lua::Value NativeResult;
    std::string NativeError;
    if (!Runtime->Execute(TCHAR_TO_UTF8(*Function), NativeArguments, NativeResult, NativeError))
    {
        Error = UTF8_TO_TCHAR(NativeError.c_str());
        return false;
    }
    switch (NativeResult.Type)
    {
    case UE5Lua::ValueType::Nil: break;
    case UE5Lua::ValueType::Integer: Result.Type = EUE5LuaValueType::Integer; Result.IntegerValue = NativeResult.Integer; break;
    case UE5Lua::ValueType::Number: Result.Type = EUE5LuaValueType::Number; Result.NumberValue = NativeResult.Number; break;
    case UE5Lua::ValueType::Boolean: Result.Type = EUE5LuaValueType::Boolean; Result.BooleanValue = NativeResult.Boolean; break;
    case UE5Lua::ValueType::String:
    {
        if (NativeResult.String.size() > MAX_int32) { Error = TEXT("Lua string is too large for FString."); return false; }
        const FUTF8ToTCHAR Text(NativeResult.String.data(), static_cast<int32>(NativeResult.String.size()));
        Result.Type = EUE5LuaValueType::String;
        Result.StringValue = FString(Text.Length(), Text.Get());
        break;
    }
    }
    return true;
}

bool UUE5LuaSubsystem::ExecuteInt(const FString& Function, const TArray<int64>& Arguments, int64& Result, FString& Error)
{
    Result = 0;
    TArray<FUE5LuaValue> Values;
    Values.Reserve(Arguments.Num());
    for (int64 Argument : Arguments)
    {
        FUE5LuaValue Value;
        Value.Type = EUE5LuaValueType::Integer;
        Value.IntegerValue = Argument;
        Values.Add(Value);
    }
    FUE5LuaValue ReturnValue;
    if (!Execute(Function, Values, ReturnValue, Error)) return false;
    if (ReturnValue.Type != EUE5LuaValueType::Integer)
    {
        Error = TEXT("The Lua function did not return an integer.");
        return false;
    }
    Result = ReturnValue.IntegerValue;
    return true;
}

void UUE5LuaSubsystem::UnloadScript()
{
    if (!ensureMsgf(IsInGameThread(), TEXT("Lua cleanup must run on the game thread."))) return;
    delete Runtime;
    Runtime = nullptr;
}

void UUE5LuaSubsystem::Deinitialize()
{
    UnloadScript();
    Super::Deinitialize();
}
