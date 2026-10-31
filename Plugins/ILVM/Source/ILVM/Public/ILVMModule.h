#pragma once

#include "Modules/ModuleInterface.h"
#include "CoreMinimal.h"

class ILVM_API IILVMModule : public IModuleInterface
{
public:
    static IILVMModule& Get();
    virtual bool Request(const FString& Json, FString& Response, FString& Error) = 0;
};
