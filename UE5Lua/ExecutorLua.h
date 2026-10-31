#pragma once

#include <cstdint>
#include <string>
#include <vector>

struct lua_State;

namespace UE5Lua
{
    enum class ValueType { Nil, Integer, Number, Boolean, String };

    struct Value
    {
        ValueType Type = ValueType::Nil;
        int64_t Integer = 0;
        double Number = 0;
        bool Boolean = false;
        std::string String;

        static Value FromInteger(int64_t Input);
        static Value FromNumber(double Input);
        static Value FromBoolean(bool Input);
        static Value FromString(const std::string& Input);
    };

    class ExecutorLua
    {
    public:
        ExecutorLua() = default;
        ~ExecutorLua();
        ExecutorLua(const ExecutorLua&) = delete;
        ExecutorLua& operator=(const ExecutorLua&) = delete;

        // A successful load replaces the state; a failed load preserves the previous state.
        bool LoadFile(const std::string& File, std::string& Error);
        bool LoadSource(const std::string& Source, const std::string& ChunkName,
            const std::string& ModuleDirectory, std::string& Error);
        bool Execute(const std::string& Function, const std::vector<Value>& Arguments,
            Value& Result, std::string& Error);
        void Reset();
        int GetStackSize() const;

    private:
        lua_State* State = nullptr;
    };
}
