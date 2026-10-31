/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */

using System;

//Configure how IL2CPP processes this code.
namespace Unity.IL2CPP.CompilerServices
{
    internal enum Option
    {
        //Enable null checks.
        NullChecks = 1,
        //Enable array-bounds checks.
        ArrayBoundsChecks = 2,
        //Enable divide-by-zero checks.
        DivideByZeroChecks = 3,
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method
        | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal class Il2CppSetOptionAttribute : Attribute
    {
        internal Option Option { get; private set; }
        internal object Value { get; private set; }

        internal Il2CppSetOptionAttribute(Option option, object value)
        {
            Option = option;
            Value = value;
        }
    }
}
