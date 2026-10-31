/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */

using System;

namespace IFix
{
    //Switch to interpreted execution.
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field)]
    public class InterpretAttribute : Attribute
    {
    }

    //Place the attribute directly on the method to be patched.
    [AttributeUsage(AttributeTargets.Method)]
    public class PatchAttribute : Attribute
    {
    }

    //Manually specify delegate bridges for closures and interface bridges for cases such as iterators.
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomBridgeAttribute : Attribute
    {

    }
}