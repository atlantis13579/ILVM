using System;
using System.Collections.Generic;
using System.Text;
using IFix.Core;

namespace IFix
{
    // Token: 0x0200000C RID: 12
    public class ILFixDynamicMethodWrapper
    {
        // Token: 0x06000029 RID: 41 RVA: 0x00002668 File Offset: 0x00000868
        public ILFixDynamicMethodWrapper(VirtualMachine virtualMachine, int methodId, object anonObj)
        {
            this.virtualMachine = virtualMachine;
            this.methodId = methodId;
            this.anonObj = anonObj;
        }

        // Token: 0x0600002A RID: 42 RVA: 0x00002688 File Offset: 0x00000888
        public int __Gen_Wrap_0(object P0, int P1, int P2)
        {
            Call call = Call.Begin();
            if (this.anonObj != null)
            {
                call.PushObject(this.anonObj);
            }
            call.PushObject(P0);
            call.PushInt32(P1);
            call.PushInt32(P2);
            this.virtualMachine.Execute(this.methodId, ref call, (this.anonObj != null) ? 4 : 3, 0);
            return call.GetInt32(0);
        }

        // Token: 0x0600002B RID: 43 RVA: 0x000026F4 File Offset: 0x000008F4
        public void __Gen_Wrap_1(string P0)
        {
            Call call = Call.Begin();
            if (this.anonObj != null)
            {
                call.PushObject(this.anonObj);
            }
            call.PushObject(P0);
            this.virtualMachine.Execute(this.methodId, ref call, (this.anonObj != null) ? 2 : 1, 0);
        }

        // Token: 0x0600002C RID: 44 RVA: 0x00002748 File Offset: 0x00000948
        public int __Gen_Wrap_2(int P0)
        {
            Call call = Call.Begin();
            if (this.anonObj != null)
            {
                call.PushObject(this.anonObj);
            }
            call.PushInt32(P0);
            this.virtualMachine.Execute(this.methodId, ref call, (this.anonObj != null) ? 2 : 1, 0);
            return call.GetInt32(0);
        }

        // Token: 0x0600002D RID: 45 RVA: 0x000027A4 File Offset: 0x000009A4
        public void __Gen_Wrap_3(object P0)
        {
            Call call = Call.Begin();
            if (this.anonObj != null)
            {
                call.PushObject(this.anonObj);
            }
            call.PushObject(P0);
            this.virtualMachine.Execute(this.methodId, ref call, (this.anonObj != null) ? 2 : 1, 0);
        }

        // Token: 0x04000007 RID: 7
        private VirtualMachine virtualMachine;

        // Token: 0x04000008 RID: 8
        private int methodId;

        // Token: 0x04000009 RID: 9
        private object anonObj;

        // Token: 0x0400000A RID: 10
        public static ILFixDynamicMethodWrapper[] wrapperArray = new ILFixDynamicMethodWrapper[0];
    }
}