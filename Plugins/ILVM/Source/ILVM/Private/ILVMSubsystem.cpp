#include "ILVMSubsystem.h"
#include "ILVMModule.h"
#include "Dom/JsonObject.h"
#include "Misc/Paths.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"
#include "Serialization/JsonWriter.h"

bool UILVMSubsystem::Send(const TSharedRef<FJsonObject>& Request, TSharedPtr<FJsonObject>& Response, FString& Error)
{
    Error.Reset();
    Request->SetNumberField(TEXT("sessionId"), static_cast<double>(SessionId));
    FString RequestText;
    FJsonSerializer::Serialize(Request, TJsonWriterFactory<>::Create(&RequestText));
    FString ResponseText;
    if (!IILVMModule::Get().Request(RequestText, ResponseText, Error)) return false;
    if (!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(ResponseText), Response) || !Response.IsValid())
    {
        Error = TEXT("ILVM returned an invalid JSON response.");
        return false;
    }
    bool Ok = false;
    if (!Response->TryGetBoolField(TEXT("ok"), Ok) || !Ok)
    {
        if (!Response->TryGetStringField(TEXT("error"), Error)) Error = TEXT("ILVM request failed.");
        return false;
    }
    return true;
}

bool UILVMSubsystem::LoadHotfix(const FString& AssemblyPath, FString& Error)
{
    TSharedPtr<FJsonObject> Response;
    if (SessionId == 0)
    {
        const TSharedRef<FJsonObject> Create = MakeShared<FJsonObject>();
        Create->SetStringField(TEXT("operation"), TEXT("create"));
        if (!Send(Create, Response, Error)) return false;
        SessionId = static_cast<int64>(Response->GetNumberField(TEXT("result")));
    }
    const FString FullPath = FPaths::IsRelative(AssemblyPath)
        ? FPaths::ConvertRelativePathToFull(FPaths::Combine(FPaths::ProjectContentDir(), TEXT("Hotfix"), AssemblyPath))
        : AssemblyPath;
    const TSharedRef<FJsonObject> Request = MakeShared<FJsonObject>();
    Request->SetStringField(TEXT("operation"), TEXT("load"));
    Request->SetStringField(TEXT("assemblyPath"), FullPath);
    return Send(Request, Response, Error);
}

bool UILVMSubsystem::Execute(const FString& Method, const FString& ArgumentsJson, FString& ResultJson, FString& Error)
{
    ResultJson.Reset();
    Error.Reset();
    if (SessionId == 0) { Error = TEXT("Load a hotfix assembly before executing a method."); return false; }
    TArray<TSharedPtr<FJsonValue>> Arguments;
    if (!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(ArgumentsJson), Arguments))
    {
        Error = TEXT("ArgumentsJson must be a JSON array.");
        return false;
    }
    const TSharedRef<FJsonObject> Request = MakeShared<FJsonObject>();
    Request->SetStringField(TEXT("operation"), TEXT("invoke"));
    Request->SetStringField(TEXT("method"), Method);
    Request->SetArrayField(TEXT("arguments"), Arguments);
    TSharedPtr<FJsonObject> Response;
    if (!Send(Request, Response, Error)) return false;
    FJsonSerializer::Serialize(Response.ToSharedRef(), TJsonWriterFactory<>::Create(&ResultJson));
    return true;
}

bool UILVMSubsystem::ExecuteInt(const FString& Method, const TArray<int32>& Arguments, int32& Result, FString& Error)
{
    Result = 0;
    TArray<TSharedPtr<FJsonValue>> JsonArguments;
    for (int32 Value : Arguments) JsonArguments.Add(MakeShared<FJsonValueNumber>(Value));
    FString ArgumentsJson;
    FJsonSerializer::Serialize(JsonArguments, TJsonWriterFactory<>::Create(&ArgumentsJson));
    FString ResultJson;
    if (!Execute(Method, ArgumentsJson, ResultJson, Error)) return false;
    TSharedPtr<FJsonObject> Response;
    FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(ResultJson), Response);
    double Number = 0;
    if (!Response.IsValid() || !Response->TryGetNumberField(TEXT("result"), Number) ||
        Number < MIN_int32 || Number > MAX_int32 || Number != static_cast<int32>(Number))
    {
        Error = TEXT("The interpreted method did not return an int32 value.");
        return false;
    }
    Result = static_cast<int32>(Number);
    return true;
}

void UILVMSubsystem::UnloadHotfix()
{
    if (SessionId == 0) return;
    const TSharedRef<FJsonObject> Request = MakeShared<FJsonObject>();
    Request->SetStringField(TEXT("operation"), TEXT("destroy"));
    TSharedPtr<FJsonObject> Response;
    FString Error;
    if (!Send(Request, Response, Error)) UE_LOG(LogTemp, Warning, TEXT("ILVM cleanup: %s"), *Error);
    SessionId = 0;
}

void UILVMSubsystem::Deinitialize()
{
    UnloadHotfix();
    Super::Deinitialize();
}
