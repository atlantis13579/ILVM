cmake_minimum_required(VERSION 3.20)
project(UE5Lua LANGUAGES C CXX)

file(GLOB LUA_SOURCES CONFIGURE_DEPENDS "lua/*.c")
list(REMOVE_ITEM LUA_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/lua/lua.c" "${CMAKE_CURRENT_SOURCE_DIR}/lua/luac.c")
add_library(UE5LuaRuntime STATIC ${LUA_SOURCES} ExecutorLua.cpp)
target_include_directories(UE5LuaRuntime PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}")
target_compile_features(UE5LuaRuntime PUBLIC cxx_std_17)
if(MSVC)
    target_compile_definitions(UE5LuaRuntime PRIVATE _CRT_SECURE_NO_WARNINGS)
    set_property(TARGET UE5LuaRuntime PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
endif()

add_executable(UE5Lua main.cpp)
target_link_libraries(UE5Lua PRIVATE UE5LuaRuntime)
add_custom_command(TARGET UE5Lua POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "${CMAKE_CURRENT_SOURCE_DIR}/test.lua" "$<TARGET_FILE_DIR:UE5Lua>/test.lua")

enable_testing()
add_executable(UE5LuaTests "../Tests/Lua/main.cpp")
target_link_libraries(UE5LuaTests PRIVATE UE5LuaRuntime)
add_test(NAME lua_demo COMMAND UE5Lua "${CMAKE_CURRENT_SOURCE_DIR}/test.lua")
add_test(NAME lua_regression COMMAND UE5LuaTests "${CMAKE_CURRENT_SOURCE_DIR}/test.lua")
