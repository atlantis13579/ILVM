/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */

using System;

namespace IFix.Core
{
    //The injector implements this interface automatically.
    public interface WrappersManager
    {
        //Create a delegate; a non-null anon parameter indicates a closure.
        Delegate CreateDelegate(Type type, int id, object anon);
        //Create an interface bridge.
        AnonymousStorey CreateBridge(int fieldNum, int[] fieldTypes, int typeIndex, int[] vTable, int[] slots, VirtualMachine virtualMachine);
        //Create a wrapper object and store it in the wrapper array when loading a patch.
        object CreateWrapper(int id);
        //Initialize the wrapper array.
        object InitWrapperArray(int len);
    }
}
