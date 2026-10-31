#include "ILVMModule.h"
#include "DotNetHost.h"
#include "Interfaces/IPluginManager.h"
#include "Misc/Paths.h"
#include "Modules/ModuleManager.h"

class FILVMModule final : public IILVMModule
{
public:
    virtual bool SupportsDynamicReloading() override { return false; }

    virtual bool Request(const FString& Json, FString& Response, FString& Error) override
    {
        if (!IsInGameThread())
        {
            Error = TEXT("ILVM subsystem calls must run on the game thread.");
            return false;
        }
        const TSharedPtr<IPlugin> Plugin = IPluginManager::Get().FindPlugin(TEXT("ILVM"));
        if (!Plugin.IsValid()) { Error = TEXT("Cannot locate the ILVM plugin."); return false; }
        const FString Base = FPaths::ConvertRelativePathToFull(Plugin->GetBaseDir());
        std::string NativeError;
        if (!Host.Initialize(*FPaths::Combine(Base, TEXT("Binaries/Managed")), *FPaths::Combine(Base, TEXT("Binaries/DotNet")), NativeError))
        {
            Error = UTF8_TO_TCHAR(NativeError.c_str());
            return false;
        }
        std::string NativeResponse;
        if (!Host.Request(TCHAR_TO_UTF8(*Json), NativeResponse, NativeError))
        {
            Error = UTF8_TO_TCHAR(NativeError.c_str());
            return false;
        }
        Response = UTF8_TO_TCHAR(NativeResponse.c_str());
        return true;
    }

private:
    ILVM::DotNetHost Host;
};

IILVMModule& IILVMModule::Get()
{
    return FModuleManager::LoadModuleChecked<IILVMModule>(TEXT("ILVM"));
}

IMPLEMENT_MODULE(FILVMModule, ILVM)
