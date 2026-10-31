#include "Modules/ModuleManager.h"

class FUE5LuaModule final : public IModuleInterface
{
public:
    virtual bool SupportsDynamicReloading() override { return false; }
};

IMPLEMENT_MODULE(FUE5LuaModule, UE5Lua)
