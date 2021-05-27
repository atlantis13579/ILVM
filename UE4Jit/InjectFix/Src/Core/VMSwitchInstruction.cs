using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IFix
{
    public class VMExecute
    {
        public int demoAdd(int x, int y)
        {
            int Sum = x + y;
            return Sum;
        }
    }
}


namespace IFix.Core
{
    public unsafe struct VMExecuteContext
    {
        public Instruction** m_pc;
        public Value** m_argumentBase;
        public object[] managedStack;
        public Value** m_evaluationStackBase;
        public int* m_argsCount;
        public int* m_methodIndex;
        public int* m_refCount;
        public Value*** m_topWriteBack;

        public Value** m_evaluationStackPointer;
        public Value** m_localBase;
    }
    unsafe partial class VirtualMachine
    {
        const string ModuleNC = "ILVM.dll";
        [System.Runtime.InteropServices.DllImport(ModuleNC, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern unsafe Value* SDK_FILVirtualMachine_Execute(Instruction* pc, Value* argumentBase, void** managedStack,
                        Value* evaluationStackBase, int argsCount, Instruction **methodTable,
                        int refCount, Value** topWriteBack);

        public Value* ExecuteCpp(Instruction* pc, Value* argumentBase, object[] managedStack,
            Value* evaluationStackBase, int argsCount, Instruction** methodTable,
            int refCount = 0, Value** topWriteBack = null)
        {
            return SDK_FILVirtualMachine_Execute(pc, argumentBase, null, evaluationStackBase, argsCount, methodTable, refCount, topWriteBack);
        }
        public delegate bool FRunInstruction(ref VMExecuteContext context);
        FRunInstruction[] Executer;
        public bool UseExecuter = false;
        public void InitExecuter()
        {
            UseExecuter = true;

            if (Executer != null)
                return;
            Executer = new FRunInstruction[(int)Code.StackSpace];
            mInstruction_Ldarg = this.Instruction_Ldarg;
            Executer[(int)Code.Ldarg] = mInstruction_Ldarg;
            mInstruction_Ldloc = this.Instruction_Ldloc;
            Executer[(int)Code.Ldloc] = mInstruction_Ldloc;
            mInstruction_Ldc_I4 = this.Instruction_Ldc_I4;
            Executer[(int)Code.Ldc_I4] = mInstruction_Ldc_I4;
            mInstruction_Stloc = this.Instruction_Stloc;
            Executer[(int)Code.Stloc] = mInstruction_Stloc;
            mInstruction_Add = this.Instruction_Add;
            Executer[(int)Code.Add] = mInstruction_Add;
            mInstruction_Br = this.Instruction_Br;
            Executer[(int)Code.Br] = mInstruction_Br;
            mInstruction_Ble = this.Instruction_Ble;    
            Executer[(int)Code.Ble] = mInstruction_Ble;
            mInstruction_Rem = this.Instruction_Rem;
            Executer[(int)Code.Rem] = mInstruction_Rem;
            mInstruction_Mul = this.Instruction_Mul;
            Executer[(int)Code.Mul] = mInstruction_Mul;

            //mInstruction_NoOP = Instruction_NoOP;
            //for (int i = 0; i < Executer.Length-1; i++)
            //{
            //    Executer[i] = mInstruction_NoOP;
            //}
        }
        //FRunInstruction mInstruction_NoOP;
        //private unsafe bool Instruction_NoOP(ref VMExecuteContext context)
        //{
        //    return true;
        //}
        FRunInstruction mInstruction_Ldarg;
        private unsafe bool Instruction_Ldarg(ref VMExecuteContext context)
        {
            copy(*context.m_evaluationStackBase, *context.m_evaluationStackPointer, *context.m_argumentBase + (*context.m_pc)->Operand,
                                    context.managedStack);
            (*context.m_evaluationStackPointer)++;
            return true;
        }
        FRunInstruction mInstruction_Ldloc;
        private unsafe bool Instruction_Ldloc(ref VMExecuteContext context)
        {
            copy(*context.m_evaluationStackBase, *context.m_evaluationStackPointer, *context.m_localBase + (*context.m_pc)->Operand,
                                    context.managedStack);
            (*context.m_evaluationStackPointer)++;
            return true;
        }
        FRunInstruction mInstruction_Ldc_I4;
        private unsafe bool Instruction_Ldc_I4(ref VMExecuteContext context)
        {
            (*context.m_evaluationStackPointer)->Value1 = (*context.m_pc)->Operand; //高位不清除
            (*context.m_evaluationStackPointer)->Type = ValueType.Integer;
            (*context.m_evaluationStackPointer)++;
            return true;
        }
        FRunInstruction mInstruction_Stloc;
        private unsafe bool Instruction_Stloc(ref VMExecuteContext context)
        {
            (*context.m_evaluationStackPointer)--;
            //print("+++before stloc", locs + ins.Operand);
            store((*context.m_evaluationStackBase), (*context.m_localBase) + (*context.m_pc)->Operand, (*context.m_evaluationStackPointer),
                context.managedStack);
            //print("+++after stloc", locs + ins.Operand);
            context.managedStack[(*context.m_evaluationStackPointer) - (*context.m_evaluationStackBase)] = null;
            return true;
        }
        FRunInstruction mInstruction_Add;
        private unsafe bool Instruction_Add(ref VMExecuteContext context)
        {
            Value* b = (*context.m_evaluationStackPointer) - 1;
            //大于1的立即数和指针运算在il2cpp（unity 5.4）有bug，都会按1算
            Value* a = (*context.m_evaluationStackPointer) - 1 - 1;
            (*context.m_evaluationStackPointer) = a;
            switch (a->Type)//TODO: 通过修改指令优化掉
            {
                case ValueType.Long:
                    *((long*)&(*context.m_evaluationStackPointer)->Value1)
                        = *((long*)&a->Value1) + *((long*)&b->Value1);
                    break;
                case ValueType.Integer:
                    (*context.m_evaluationStackPointer)->Value1 = a->Value1 + b->Value1;
                    break;
                case ValueType.Float:
                    *((float*)&(*context.m_evaluationStackPointer)->Value1)
                        = *((float*)&a->Value1) + *((float*)&b->Value1);
                    break;
                case ValueType.Double:
                    *((double*)&(*context.m_evaluationStackPointer)->Value1)
                        = *((double*)&a->Value1) + *((double*)&b->Value1);
                    break;
                default:
                    throwRuntimeException(new NotImplementedException(), true);
                    break;
            }
            (*context.m_evaluationStackPointer)++;
            return true;
        }
        FRunInstruction mInstruction_Br;
        private unsafe bool Instruction_Br(ref VMExecuteContext context)
        {
            (*context.m_pc) += (*context.m_pc)->Operand;
            return false;
        }
        FRunInstruction mInstruction_Ble;
        private unsafe bool Instruction_Ble(ref VMExecuteContext context)
        {
            var b = (*context.m_evaluationStackPointer) - 1;
            var a = (*context.m_evaluationStackPointer) - 1 - 1;
            *context.m_evaluationStackPointer = a;
            bool transfer = false;
            switch ((*context.m_evaluationStackPointer)->Type)
            {
                case ValueType.Integer:
                    transfer = a->Value1 <= b->Value1;
                    break;
                case ValueType.Long:
                    transfer = *(long*)&a->Value1 <= *(long*)&b->Value1;
                    break;
                case ValueType.Float:
                    transfer = *(float*)&a->Value1 <= *(float*)&b->Value1;
                    break;
                case ValueType.Double:
                    transfer = *(double*)&a->Value1 <= *(double*)&b->Value1;
                    break;
                default:
                    throwRuntimeException(new NotImplementedException("Blt for "
                        + (*context.m_evaluationStackPointer)->Type), true);
                    break;
            }

            if (transfer)
            {
                (*context.m_pc) += (*context.m_pc)->Operand;
                return false;
            }
            return true;
        }
        FRunInstruction mInstruction_Rem;
        private unsafe bool Instruction_Rem(ref VMExecuteContext context)
        {
            Value* rhs = (*context.m_evaluationStackPointer) - 1;
            Value* lhs = rhs - 1;

            switch (lhs->Type)
            {
                case ValueType.Integer:
                    lhs->Value1 = lhs->Value1 % rhs->Value1;
                    break;
                case ValueType.Long:
                    *(long*)&lhs->Value1 = *(long*)&lhs->Value1 % *(long*)&rhs->Value1;
                    break;
                case ValueType.Float:
                    *(float*)&lhs->Value1 = *(float*)&lhs->Value1 % *(float*)&rhs->Value1;
                    break;
                case ValueType.Double:
                    *(double*)&lhs->Value1 = *(double*)&lhs->Value1 % *(double*)&rhs->Value1;
                    break;
            }

            (*context.m_evaluationStackPointer) = rhs;
            return true;
        }
        FRunInstruction mInstruction_Mul;
        private unsafe bool Instruction_Mul(ref VMExecuteContext context)
        {
            Value* b = (*context.m_evaluationStackPointer) - 1;
            Value* a = (*context.m_evaluationStackPointer) - 1 - 1;
            (*context.m_evaluationStackPointer) = a;
            switch (a->Type)
            {
                case ValueType.Long:
                    *((long*)&(*context.m_evaluationStackPointer)->Value1)
                        = (*((long*)&a->Value1)) * (*((long*)&b->Value1));
                    break;
                case ValueType.Integer:
                    (*context.m_evaluationStackPointer)->Value1 = a->Value1 * b->Value1;
                    break;
                case ValueType.Float:
                    *((float*)&(*context.m_evaluationStackPointer)->Value1)
                        = (*((float*)&a->Value1)) * (*((float*)&b->Value1));
                    break;
                case ValueType.Double:
                    *((double*)&(*context.m_evaluationStackPointer)->Value1)
                        = (*((double*)&a->Value1)) * (*((double*)&b->Value1));
                    break;
            }
            (*context.m_evaluationStackPointer)++;
            return true;
        }
    }
}
