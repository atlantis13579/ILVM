#include "ExecutorLua.h"
#include <filesystem>
#include <fstream>
#include <iterator>
#include <limits>

extern "C"
{
#include "lua/lua.h"
#include "lua/lauxlib.h"
#include "lua/lualib.h"
}

namespace
{
    struct StackGuard
    {
        lua_State* State;
        int Top;
        explicit StackGuard(lua_State* Input) : State(Input), Top(lua_gettop(Input)) {}
        ~StackGuard() { lua_settop(State, Top); }
    };

    std::string ReadError(lua_State* State)
    {
        const char* Message = lua_tostring(State, -1);
        return Message ? Message : "Lua raised an error without a string message.";
    }

    int Traceback(lua_State* State)
    {
        const char* Message = lua_tostring(State, 1);
        luaL_traceback(State, State, Message ? Message : "Lua raised a non-string error.", 1);
        return 1;
    }

    int OpenLibraries(lua_State* State)
    {
        const char* ModulePath = static_cast<const char*>(lua_touserdata(State, 1));
        luaL_openlibs(State);
        if (*ModulePath)
        {
            lua_getglobal(State, "package");
            lua_getfield(State, -1, "path");
            const char* Existing = lua_tostring(State, -1);
            lua_pushfstring(State, "%s;%s", ModulePath, Existing ? Existing : "");
            lua_setfield(State, -3, "path");
        }
        return 0;
    }

    struct CallRequest
    {
        const char* Function;
        const std::vector<UE5Lua::Value>* Arguments;
    };

    int Invoke(lua_State* State)
    {
        const auto* Request = static_cast<const CallRequest*>(lua_touserdata(State, 1));
        if (!lua_checkstack(State, static_cast<int>(Request->Arguments->size()) + 3))
            return luaL_error(State, "Insufficient Lua stack space.");
        // Global-table metamethods execute inside the protected call as well.
        lua_getglobal(State, Request->Function);
        if (!lua_isfunction(State, -1))
            return luaL_error(State, "Global function '%s' was not found.", Request->Function);
        // Keep this C callback free of owning C++ objects and debug iterators across longjmp.
        for (size_t Index = 0; Index < Request->Arguments->size(); ++Index)
        {
            const auto& Argument = (*Request->Arguments)[Index];
            switch (Argument.Type)
            {
            case UE5Lua::ValueType::Nil: lua_pushnil(State); break;
            case UE5Lua::ValueType::Integer: lua_pushinteger(State, Argument.Integer); break;
            case UE5Lua::ValueType::Number: lua_pushnumber(State, Argument.Number); break;
            case UE5Lua::ValueType::Boolean: lua_pushboolean(State, Argument.Boolean); break;
            case UE5Lua::ValueType::String: lua_pushlstring(State, Argument.String.data(), Argument.String.size()); break;
            default: return luaL_error(State, "Invalid Lua argument type.");
            }
        }
        lua_call(State, static_cast<int>(Request->Arguments->size()), 1);
        return 1;
    }
}

UE5Lua::Value UE5Lua::Value::FromInteger(int64_t Input) { Value Result; Result.Type = ValueType::Integer; Result.Integer = Input; return Result; }
UE5Lua::Value UE5Lua::Value::FromNumber(double Input) { Value Result; Result.Type = ValueType::Number; Result.Number = Input; return Result; }
UE5Lua::Value UE5Lua::Value::FromBoolean(bool Input) { Value Result; Result.Type = ValueType::Boolean; Result.Boolean = Input; return Result; }
UE5Lua::Value UE5Lua::Value::FromString(const std::string& Input) { Value Result; Result.Type = ValueType::String; Result.String = Input; return Result; }

UE5Lua::ExecutorLua::~ExecutorLua() { Reset(); }

void UE5Lua::ExecutorLua::Reset()
{
    if (State) lua_close(State);
    State = nullptr;
}

int UE5Lua::ExecutorLua::GetStackSize() const { return State ? lua_gettop(State) : 0; }

bool UE5Lua::ExecutorLua::LoadFile(const std::string& File, std::string& Error)
{
    const auto Path = std::filesystem::absolute(std::filesystem::u8path(File));
    std::ifstream Stream(Path, std::ios::binary);
    if (!Stream) { Error = "Cannot open Lua script: " + File; return false; }
    const std::string Source((std::istreambuf_iterator<char>(Stream)), std::istreambuf_iterator<char>());
    if (Stream.bad()) { Error = "Cannot read Lua script: " + File; return false; }
    return LoadSource(Source, "@" + File, Path.parent_path().generic_u8string(), Error);
}

bool UE5Lua::ExecutorLua::LoadSource(const std::string& Source, const std::string& ChunkName,
    const std::string& ModuleDirectory, std::string& Error)
{
    Error.clear();
    lua_State* Candidate = luaL_newstate();
    if (!Candidate) { Error = "Cannot allocate a Lua state."; return false; }
    const std::string ModulePath = ModuleDirectory.empty() ? "" : ModuleDirectory + "/?.lua;" + ModuleDirectory + "/?/init.lua";
    lua_pushcfunction(Candidate, OpenLibraries);
    lua_pushlightuserdata(Candidate, const_cast<char*>(ModulePath.c_str()));
    int Status = lua_pcall(Candidate, 1, 0, 0);
    if (Status == LUA_OK)
    {
        lua_pushcfunction(Candidate, Traceback);
        size_t Offset = Source.compare(0, 3, "\xef\xbb\xbf") == 0 ? 3 : 0;
        Status = luaL_loadbufferx(Candidate, Source.data() + Offset, Source.size() - Offset, ChunkName.c_str(), "t");
        if (Status == LUA_OK) Status = lua_pcall(Candidate, 0, 0, 1);
    }
    if (Status != LUA_OK)
    {
        Error = ReadError(Candidate);
        lua_close(Candidate);
        return false;
    }
    lua_settop(Candidate, 0);
    Reset();
    State = Candidate;
    return true;
}

bool UE5Lua::ExecutorLua::Execute(const std::string& Function, const std::vector<Value>& Arguments,
    Value& Result, std::string& Error)
{
    Error.clear();
    Result = Value{};
    if (!State) { Error = "Load a Lua script before executing a function."; return false; }
    if (Function.empty() || Function.find('\0') != std::string::npos)
    { Error = "A nonempty Lua global function name is required."; return false; }
    if (Arguments.size() > static_cast<size_t>(std::numeric_limits<int>::max() - 3))
    { Error = "Too many Lua arguments."; return false; }
    StackGuard Guard(State);
    CallRequest Request{ Function.c_str(), &Arguments };
    lua_pushcfunction(State, Traceback);
    lua_pushcfunction(State, Invoke);
    lua_pushlightuserdata(State, &Request);
    if (lua_pcall(State, 1, 1, Guard.Top + 1) != LUA_OK)
    {
        Error = ReadError(State);
        return false;
    }
    switch (lua_type(State, -1))
    {
    case LUA_TNIL: break;
    case LUA_TBOOLEAN: Result = Value::FromBoolean(lua_toboolean(State, -1) != 0); break;
    case LUA_TNUMBER:
        Result = lua_isinteger(State, -1) ? Value::FromInteger(lua_tointeger(State, -1)) : Value::FromNumber(lua_tonumber(State, -1));
        break;
    case LUA_TSTRING:
    {
        size_t Length = 0;
        const char* Text = lua_tolstring(State, -1, &Length);
        Result = Value::FromString(std::string(Text, Length));
        break;
    }
    default:
        Error = "Unsupported Lua return type: " + std::string(luaL_typename(State, -1));
        return false;
    }
    return true;
}
