#pragma once

#include <string>

namespace ILVM
{
    class DotNetHost
    {
    public:
        bool Initialize(const std::wstring& ManagedDirectory, const std::wstring& DotNetRoot, std::string& Error);
        bool Request(const std::string& Json, std::string& Response, std::string& Error) const;

    private:
        using DispatchFunction = int(__cdecl*)(const char*, char**);
        using FreeFunction = void(__cdecl*)(void*);
        DispatchFunction Dispatch = nullptr;
        FreeFunction FreeResponse = nullptr;
    };
}
