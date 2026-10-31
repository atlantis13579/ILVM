#include "ExecutorLua.h"
#include <chrono>
#include <filesystem>
#include <iostream>

int main(int Count, char** Arguments)
{
    std::string File = Count > 1 ? Arguments[1] : (std::filesystem::absolute(Arguments[0]).parent_path() / "test.lua").string();
    UE5Lua::ExecutorLua VM;
    UE5Lua::Value Result;
    std::string Error;
    if (!VM.LoadFile(File, Error) || !VM.Execute("Add", {UE5Lua::Value::FromInteger(1), UE5Lua::Value::FromInteger(2)}, Result, Error))
    { std::cerr << Error << '\n'; return 1; }
    if (Result.Type != UE5Lua::ValueType::Integer || Result.Integer != 3)
    { std::cerr << "Add returned an unexpected result.\n"; return 1; }
    std::cout << "Add(1, 2) = " << Result.Integer << '\n';
    if (Count > 2 && std::string(Arguments[2]) == "--benchmark")
    {
        const auto Start = std::chrono::steady_clock::now();
        if (!VM.Execute("Benchmark", {}, Result, Error)) { std::cerr << Error << '\n'; return 1; }
        std::cout << "Benchmark = " << Result.Integer << ", elapsed = "
            << std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - Start).count() << " ms\n";
    }
    return 0;
}
