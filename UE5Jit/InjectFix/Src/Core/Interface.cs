using System;
using System.Collections.Generic;
using System.Text;
using IFix.Core;

namespace IFix
{
	public class ILFixInterfaceBridge : AnonymousStorey
    {
		bool running
		{
			get
			{
				Call call = Call.Begin();
				call.PushObject(this);
				this.virtualMachine.Execute(this.methodId_0, ref call, 1, 0);
				return call.GetBoolean(0);
			}
		}

		void Destroy()
		{
			Call call = Call.Begin();
			call.PushObject(this);
			this.virtualMachine.Execute(this.methodId_1, ref call, 1, 0);
		}

		void Start()
		{
			Call call = Call.Begin();
			call.PushObject(this);
			this.virtualMachine.Execute(this.methodId_2, ref call, 1, 0);
		}

		void Stop()
		{
			Call call = Call.Begin();
			call.PushObject(this);
			this.virtualMachine.Execute(this.methodId_3, ref call, 1, 0);
		}

		void Update()
		{
			Call call = Call.Begin();
			call.PushObject(this);
			this.virtualMachine.Execute(this.methodId_4, ref call, 1, 0);
		}

        public ILFixInterfaceBridge(int fieldNum, int[] fieldTypes, int typeIndex, int[] vTable, int[] methodIdArray, VirtualMachine virtualMachine) : base(fieldNum, fieldTypes, typeIndex, vTable, virtualMachine)
        {
            if (methodIdArray.Length != 6)
            {
                throw new Exception("invalid length of methodId array");
            }
            this.methodId_0 = methodIdArray[0];
            this.methodId_1 = methodIdArray[1];
            this.methodId_2 = methodIdArray[2];
            this.methodId_3 = methodIdArray[3];
            this.methodId_4 = methodIdArray[4];
        }

        private int methodId_0 = 0;
		private int methodId_1 = 0;
		private int methodId_2 = 0;
		private int methodId_3 = 0;
		private int methodId_4 = 0;
    }
}