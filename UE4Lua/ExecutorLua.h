#pragma once

extern "C" {
#include "lua\lua.h"
#include "lua\lauxlib.h"
#include "lua\lualib.h"
}

class ExecutorLua
{
public:
	ExecutorLua(const char *file)
	{
		L = luaL_newstate();
		luaL_openlibs(L);
		luaL_dofile(L, file);
	}

	~ExecutorLua()
	{
		lua_close(L);
	}

	void UnpackArgs()
	{
	}

	template<typename ... Ts>
	void UnpackArgs(int arg0, Ts... args)
	{
		lua_pushinteger(L, arg0);
		UnpackArgs(args...);
	}

	template<typename ... Ts>
	void UnpackArgs(float arg0, Ts... args)
	{
		lua_pushnumber(L, arg0);
		UnpackArgs(args...);
	}

	template<typename R, typename ... Ts>
	R Execute(const char *func, Ts... args)
	{
		lua_getglobal(L, func);

		UnpackArgs(args...);

		if (lua_pcall(L, sizeof...(args), 1, 0) != 0)
		{
			printf("error running function %s: %s\n", func, lua_tostring(L, -1));
			return 0;
		}

		if (!lua_isnumber(L, -1))
		{
			printf("function %s must return a number\n", func);
			return 0;
		}

		if (std::is_same<R, int>::value)
		{
			R z = (R)lua_tointeger(L, -1);
			lua_pop(L, 1);
			return z;
		}
		else if (std::is_same<R, float>::value)
		{
			R z = (R)lua_tonumber(L, -1);
			lua_pop(L, 1);
			return z;
		}

		return 0;
	}

private:
	lua_State *L;
};