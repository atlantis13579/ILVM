#include "DotNetHost.h"

#if ILVM_WITH_UNREAL
#include "Windows/WindowsHWrapper.h"
#else
#include <Windows.h>
#endif
#include <array>
#include <cwchar>
#include <vector>

namespace
{
    // Minimal declarations of the public hostfxr ABI from dotnet/runtime.
    struct HostParameters
    {
        size_t Size;
        const wchar_t* HostPath;
        const wchar_t* DotNetRoot;
    };
    using InitializeFunction = int(__cdecl*)(const wchar_t*, const HostParameters*, void**);
    using GetDelegateFunction = int(__cdecl*)(void*, int, void**);
    using CloseFunction = int(__cdecl*)(void*);
    using LoadAssemblyFunction = int(__stdcall*)(const wchar_t*, const wchar_t*, const wchar_t*, const wchar_t*, void*, void**);
    constexpr int LoadAssemblyAndGetFunctionPointer = 5;

    std::wstring GetEnvironment(const wchar_t* Name)
    {
        DWORD Size = GetEnvironmentVariableW(Name, nullptr, 0);
        if (Size == 0) return {};
        std::vector<wchar_t> Buffer(Size);
        if (GetEnvironmentVariableW(Name, Buffer.data(), Size) >= Size) return {};
        return Buffer.data();
    }

    std::wstring FindHostFxr(const std::wstring& Root)
    {
        const std::wstring Base = Root + L"\\host\\fxr\\";
        WIN32_FIND_DATAW Data{};
        HANDLE Search = FindFirstFileW((Base + L"*").c_str(), &Data);
        if (Search == INVALID_HANDLE_VALUE) return {};
        std::array<int, 3> BestVersion{};
        std::wstring Best;
        do
        {
            std::array<int, 3> Version{};
            wchar_t Suffix = 0;
            if (swscanf_s(Data.cFileName, L"%d.%d.%d%c", &Version[0], &Version[1], &Version[2], &Suffix, 1) == 3 && Version > BestVersion)
            {
                const std::wstring Candidate = Base + Data.cFileName + L"\\hostfxr.dll";
                if (GetFileAttributesW(Candidate.c_str()) != INVALID_FILE_ATTRIBUTES)
                {
                    BestVersion = Version;
                    Best = Candidate;
                }
            }
        } while (FindNextFileW(Search, &Data));
        FindClose(Search);
        return Best;
    }
}

bool ILVM::DotNetHost::Initialize(const std::wstring& ManagedDirectory, const std::wstring& DotNetRoot, std::string& Error)
{
    if (Dispatch) return true;
    std::vector<std::wstring> Roots{ DotNetRoot, GetEnvironment(L"ILVM_DOTNET_ROOT"), GetEnvironment(L"DOTNET_ROOT_X64"), GetEnvironment(L"DOTNET_ROOT"), GetEnvironment(L"ProgramFiles") + L"\\dotnet" };
    std::wstring RuntimeRoot;
    std::wstring HostPath;
    for (const auto& Root : Roots)
    {
        if (Root.empty()) continue;
        HostPath = FindHostFxr(Root);
        if (!HostPath.empty()) { RuntimeRoot = Root; break; }
    }
    if (HostPath.empty())
    {
        Error = "Cannot find hostfxr.dll. Install the x64 .NET 8 runtime or set ILVM_DOTNET_ROOT.";
        return false;
    }
    HMODULE Library = LoadLibraryW(HostPath.c_str());
    if (!Library) { Error = "Cannot load hostfxr.dll: " + std::to_string(GetLastError()); return false; }
    auto Initialize = reinterpret_cast<InitializeFunction>(GetProcAddress(Library, "hostfxr_initialize_for_runtime_config"));
    auto GetDelegate = reinterpret_cast<GetDelegateFunction>(GetProcAddress(Library, "hostfxr_get_runtime_delegate"));
    auto Close = reinterpret_cast<CloseFunction>(GetProcAddress(Library, "hostfxr_close"));
    if (!Initialize || !GetDelegate || !Close)
    {
        Error = "hostfxr.dll is missing required hosting exports.";
        FreeLibrary(Library);
        return false;
    }
    const std::wstring Config = ManagedDirectory + L"\\ILVM.Runtime.runtimeconfig.json";
    HostParameters Parameters{ sizeof(HostParameters), nullptr, RuntimeRoot.c_str() };
    void* Context = nullptr;
    int Status = Initialize(Config.c_str(), &Parameters, &Context);
    if (Status < 0 || !Context)
    {
        Error = "hostfxr initialization failed: " + std::to_string(Status) + ". Check ILVM.Runtime.runtimeconfig.json and the x64 .NET 8 runtime.";
        if (Context) Close(Context);
        FreeLibrary(Library);
        return false;
    }
    LoadAssemblyFunction LoadAssembly = nullptr;
    Status = GetDelegate(Context, LoadAssemblyAndGetFunctionPointer, reinterpret_cast<void**>(&LoadAssembly));
    Close(Context);
    if (Status != 0 || !LoadAssembly)
    {
        Error = "Cannot acquire the .NET assembly loader: " + std::to_string(Status);
        FreeLibrary(Library);
        return false;
    }
    const std::wstring Assembly = ManagedDirectory + L"\\ILVM.Runtime.dll";
    const wchar_t* Type = L"ILVM.Runtime.NativeBridge, ILVM.Runtime";
    const wchar_t* UnmanagedCallersOnly = reinterpret_cast<const wchar_t*>(static_cast<intptr_t>(-1));
    DispatchFunction NewDispatch = nullptr;
    FreeFunction NewFreeResponse = nullptr;
    Status = LoadAssembly(Assembly.c_str(), Type, L"Dispatch", UnmanagedCallersOnly, nullptr, reinterpret_cast<void**>(&NewDispatch));
    if (Status == 0)
        Status = LoadAssembly(Assembly.c_str(), Type, L"FreeResponse", UnmanagedCallersOnly, nullptr, reinterpret_cast<void**>(&NewFreeResponse));
    FreeLibrary(Library);
    if (Status != 0 || !NewDispatch || !NewFreeResponse)
    {
        Error = "Cannot bind ILVM.Runtime native entry points: " + std::to_string(Status);
        return false;
    }
    Dispatch = NewDispatch;
    FreeResponse = NewFreeResponse;
    return true;
}

bool ILVM::DotNetHost::Request(const std::string& Json, std::string& Response, std::string& Error) const
{
    if (!Dispatch) { Error = "ILVM host has not been initialized."; return false; }
    char* Buffer = nullptr;
    int Status = Dispatch(Json.c_str(), &Buffer);
    if (Status != 0 || !Buffer)
    {
        if (Buffer) FreeResponse(Buffer);
        Error = "ILVM native dispatch failed: " + std::to_string(Status);
        return false;
    }
    Response = Buffer;
    FreeResponse(Buffer);
    return true;
}
