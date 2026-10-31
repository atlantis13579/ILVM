# ILVM

UE5 的 C# 热更新插件，保留原有 InjectFix/ILVM 软件解释器。当前接入目标为 **UE5 / Windows x64**，UE 接口按本机 UE **5.4.4** 源码核对。托管部分使用 **.NET 8**，Mono.Cecil 依赖由 NuGet 恢复。

调用链：`UE5 GameInstanceSubsystem → C++ hostfxr 宿主 → ILVM.Runtime → IL 翻译器 → C# 软件解释器`。

已完成独立托管测试和原生宿主测试；按项目约定，没有编译 UE5 工程，也没有进行编辑器 PIE 或打包运行验证。

## 构建与测试

环境：Windows x64、Visual Studio 2022（包含 C++ 工具链）、CMake、.NET SDK 8/9 和 .NET 8 x64 Runtime。`ILVM.sln` 是 VS2022 解决方案，包含 C# 工程和 `UE5Lua` 原生工程；原生工程使用 v143 工具集。

在仓库根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

脚本通过 VS2022 MSBuild 构建 `ILVM.sln`，再构建 `UE5Lua` 静态库并执行 C#、Lua 和原生宿主回归。托管依赖发布到 `Plugins/ILVM/Binaries/Managed`，Lua 静态库发布到 `Plugins/UE5Lua/Source/ThirdParty`。脚本**不会调用 UnrealBuildTool 或编译 UE5**。使用 `-Configuration Debug` 可验证 Debug；只准备插件依赖可使用 `-SkipTests`。

可把当前机器上的 .NET 8 运行时一起放入插件，避免目标机器单独安装：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -BundleRuntime
```

运行时存放于 `Plugins/ILVM/Binaries/DotNet`。`-DotNetRoot` 可指定 x64 dotnet 安装路径；`-CMakeGenerator` 可指定已安装的 Visual Studio CMake 生成器。生成的二进制和测试产物不加入 Git。

单独验证托管解释器：

```powershell
dotnet run --project Tests/ILVM.Tests -c Release
dotnet run --project JitDemo -c Release
```

默认演示输出 `Game.Hotfix.Entry::Add = 5`，无需原来的 Mono 安装、`ILVM.dll`、调试符号或固定工作目录。指定程序集和方法：

```powershell
dotnet run --project JitDemo -c Release -- Samples/Hotfix/bin/Release/net8.0/Game.Hotfix.dll Game.Hotfix.Entry::Add '[3,2]'
```

## 接入 UE5

1. 执行上述构建脚本，将完整的 `Plugins/ILVM` 目录复制到游戏工程的 `Plugins/ILVM`，包括生成的 `Binaries/Managed` 和可选的 `Binaries/DotNet`。
2. 在 `.uproject` 的 `Plugins` 数组中启用 `{ "Name": "ILVM", "Enabled": true }`。如果从 C++ 调用，在游戏模块的 `Build.cs` 依赖中添加 `ILVM`。
3. 构建 `Samples/Hotfix/Hotfix.csproj`，将生成的 `Game.Hotfix.dll` 放入游戏的 `Content/Hotfix`。
4. 蓝图中获取 `ILVMSubsystem` Game Instance Subsystem，调用 `LoadHotfix("Game.Hotfix.dll")`，再调用 `ExecuteInt("Game.Hotfix.Entry::Add", [3, 2])`。预期结果为 `5`。节点返回 `false` 时读取 `Error`。

C++ 调用示例：

```cpp
#include "ILVMSubsystem.h"
#include "Engine/GameInstance.h"

UILVMSubsystem* VM = GameInstance->GetSubsystem<UILVMSubsystem>();
FString Error;
int32 Result = 0;
if (VM->LoadHotfix(TEXT("Game.Hotfix.dll"), Error))
{
    VM->ExecuteInt(TEXT("Game.Hotfix.Entry::Add"), {3, 2}, Result, Error);
}
```

插件的托管依赖和可选运行时通过 `RuntimeDependencies` 作为 NonUFS 文件打包。游戏的热更新 DLL 也必须是磁盘上的普通文件，在 `Config/DefaultGame.ini` 中加入：

```ini
[/Script/UnrealEd.ProjectPackagingSettings]
+DirectoriesToAlwaysStageAsNonUFS=(Path="Hotfix")
```

更新时将新 DLL 写入可访问位置，再次调用 `LoadHotfix` 即可。路径可以是绝对路径，或相对 `Content/Hotfix` 的路径。同名同版本程序集也可以替换；加载失败保留当前 VM，加载成功创建全新的脚本状态。**旧对象、旧委托和静态字段不会迁移到新版本**。停止使用旧回调后，旧程序集可被 GC 回收。

宿主依次查找：插件自带 `Binaries/DotNet`、`ILVM_DOTNET_ROOT`、`DOTNET_ROOT_X64`、`DOTNET_ROOT`、`Program Files/dotnet`。所有 UE 调用在游戏线程执行；每个 GameInstance 有独立会话，结束时自动销毁。

## 参数与返回值

`ExecuteInt` 适用于 int32 参数和返回值。通用蓝图节点 `Execute` 接受 JSON 参数数组，返回完整 JSON 响应，如 `{"ok":true,"result":5}`。

| 参数 | JSON 示例 |
| --- | --- |
| int32、字符串、bool、null | `[3,"hello",true,null]` |
| float / double | `[{"type":"single","value":1.5},{"type":"double","value":2.0}]` |
| 64 位整数 | `[{"type":"int64","value":"9007199254740993"}]` |
| uint32 / uint64 | `[{"type":"uint32","value":4294967295}]` |
| int32 数组 | `[{"type":"int32[]","value":[1,2,3]}]` |

int64/uint64 返回值使用 `{"type":"int64","value":"..."}`，避免 UE JSON 的 double 数字表示损失精度。其他返回值需可被 JSON 序列化。跨原生边界暂不传递任意托管对象、指针、ref/out 参数；脚本内部的 ref/out 已纳入回归测试。

存在重载时使用 Cecil 完整签名，例如 `System.Int32 Game.Hotfix.Entry::Overload(System.Int32)`；仅用短名称会报歧义错误。托管 `ExecutorVM.MethodNames` 或原生协议的 `describe` 操作可以查看可调用签名。

## 解释执行范围

- 热更新入口及可翻译的方法体由软件解释器执行；递归、虚调用、反射入口和委托通过生成的包装方法回到解释器。
- CLR 提供对象、GC、基础库和互操作。构造函数、开放泛型方法及外部库仍通过 CLR 执行，因此本插件不是完整的独立 CLR，也不提供无 JIT / AOT 平台支持。
- 示例固定 C# 7.3 语言版本，避免新编译器语法引入未经验证的 IL。异步状态机、任意泛型热更新和全部 C# 语言特性不在当前验证范围。
- 本仓库原先没有 UE 模块或 UObject 绑定生成器；新增插件提供蓝图/C++ 调用入口，尚未提供自动 UObject/UFUNCTION 的 C# 绑定。
- `ILVM/` 中的 C++ IL 解释器仍是历史未完成原型，不参与新构建。C# 翻译器与软件解释器源码、命名空间已统一为 `UE5Jit`；Lua 源码与演示工程已统一为 `UE5Lua`。

## 验证内容

- 原有 41 项测试全部通过：算术、64 位整数、浮点/NaN、数组、引用、结构体、接口、虚调用、异常/finally、闭包等；断言在 Release 下同样生效。
- 新增验证覆盖不同参数/返回值、递归和回调确实进入解释器、重载选择、缺失方法、错误参数、连续异常后的栈恢复、坏 DLL 加载回滚、同名程序集反复替换和旧程序集回收。
- 独立 C++ 测试复用插件的 `DotNetHost.cpp`，验证 C++ → hostfxr → 托管桥 → 解释器，以及 v1 → v2 热重载和会话销毁；可从仓库以外的工作目录执行。
- UE API 静态检查参考 UE 5.4.4 源码。UE 模块编译、PIE、Shipping 打包和其他 UE5 版本仍需在目标工程验证。

原生托管接口依据 [.NET 原生宿主文档](https://learn.microsoft.com/en-us/dotnet/core/tutorials/netcore-hosting)。第三方许可见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## UE5Lua

`Plugins/UE5Lua` 是独立的 UE5 Win64 插件，不依赖 C# 插件或 .NET。复用仓库内的 Lua 5.4.3 源码和 `UE5Lua/ExecutorLua.cpp`，通过静态库接入。单独构建与验证：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-lua.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-lua.ps1 -Configuration Debug
```

需要 CMake、Visual Studio 2022 C++ 工具链。脚本生成 `artifacts/lua/<配置>/UE5Lua.exe` 与静态库，并复制头文件、库和许可证到插件的 `Source/ThirdParty/UE5LuaRuntime`。UE 默认使用 Release 库；仅在 Debug 且 `bDebugBuildsActuallyUseDebugCRT=true` 时使用 Debug 库。旧 Visual Studio 工程也已改名为 `UE5Lua/UE5Lua.vcxproj`，更新为 v143 / C++17，可独立构建演示。

1. 构建 Release 后，将完整的 `Plugins/UE5Lua` 复制到游戏工程，包含生成的 `Source/ThirdParty`。在 `.uproject` 中启用 `{ "Name": "UE5Lua", "Enabled": true }`。
2. 将 `UE5Lua/test.lua` 放入游戏的 `Content/Lua/test.lua`。
3. 蓝图获取 `UE5LuaSubsystem` Game Instance Subsystem，调用 `LoadScript("test.lua")`，然后 `ExecuteInt("Add", [1, 2])`，预期返回 `3`。检查 bool 返回值和 `Error`。

从 C++ 调用时，在游戏模块依赖中添加 `UE5Lua`：

```cpp
#include "UE5LuaSubsystem.h"
#include "Engine/GameInstance.h"

UUE5LuaSubsystem* Lua = GameInstance->GetSubsystem<UUE5LuaSubsystem>();
FString Error;
int64 Result = 0;
if (Lua->LoadScript(TEXT("test.lua"), Error))
{
    Lua->ExecuteInt(TEXT("Add"), {1, 2}, Result, Error);
}
```

通用 `Execute` 节点通过 `FUE5LuaValue` 传递 nil、int64、double、bool 和字符串，返回第一个结果；表、函数和 userdata 暂不跨 UE 边界传递。`LoadSource` 可以直接执行源码字符串；`LoadScript` 支持绝对路径或相对 `Content/Lua` 的路径。`require` 优先搜索脚本目录的 `?.lua` 和 `?/init.lua`；Windows 下 Lua 自带文件接口使用系统窄字符路径，建议模块搜索目录使用 ASCII 路径。

重复加载会创建新的 Lua state，清空原来的全局变量和模块缓存；语法错误或初始化错误不会替换已有 state。每个 GameInstance 独立，调用限制在游戏线程；`UnloadScript` 或 GameInstance 结束时释放状态。不包含自动 UObject 绑定。

在 `Config/DefaultGame.ini` 中添加下面的打包项，以便运行时直接读取和替换脚本：

```ini
[/Script/UnrealEd.ProjectPackagingSettings]
+DirectoriesToAlwaysStageAsNonUFS=(Path="Lua")
```

Lua 回归包括 16 组检查及 10,000 次错误恢复循环，Release/Debug 均运行；原有基准可通过 `artifacts/lua/Release/UE5Lua.exe UE5Lua/test.lua --benchmark` 执行。独立测试链接的静态库与 UE5 插件一致；UE5 模块只做源码接口核对，尚未编译、进行 PIE 或打包运行验证。Lua C API 依据 [Lua 5.4 官方手册](https://www.lua.org/manual/5.4/manual.html)。
