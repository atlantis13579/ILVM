# Third-party components

The repository's MIT license is in `LICENSE`.

- InjectFix sources under `UE5Jit/InjectFix` retain their original copyright headers:
  Copyright (C) 2019 THL A29 Limited, a Tencent company. Licensed under MIT.
- Lua 5.4.3 sources under `UE5Lua/lua`: Copyright (C) 1994-2021 Lua.org,
  PUC-Rio. Licensed under MIT; full license in `UE5Lua/LICENSE`.
- Mono.Cecil 0.11.6 is restored through NuGet. Copyright Jb Evain and contributors,
  licensed under MIT. Source: https://github.com/jbevain/cecil
- The hostfxr ABI declarations in `DotNetHost.cpp` follow the public .NET hosting
  interface: https://github.com/dotnet/runtime/tree/main/src/native/corehost
- Optional bundled .NET runtime files retain `LICENSE.txt` and
  `ThirdPartyNotices.txt` from the installed distribution.

MIT permission text for InjectFix and Mono.Cecil:

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
