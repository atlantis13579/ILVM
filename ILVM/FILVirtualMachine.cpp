#include "pch.h"
#include "assert.h"
#include <set>
#include "FILVirtualMachine.h"

/*
#using "D:/src/CRBProject/UnrealBinder/JitDemo/bin/Debug/JitDemo.exe"
using namespace IFix;
*/

namespace ILVM
{
	void code_not_implement(ECode code)
	{
		static std::set<ECode> s_cache;
		if (s_cache.count(code) == 0)
		{
			s_cache.insert(code);
			printf("Code(%d) not implement\n", (int)code);
		}
	}

	const int MAX_EVALUATION_STACK_SIZE = 1024 * 10;
	FStackVariable* FILVirtualMachine::Execute(Instruction* pc, FStackVariable* argumentBase, void** managedStack,
		FStackVariable* evaluationStackBase, int argsCount, Instruction **methodTable,
		int refCount, FStackVariable** topWriteBack)
	{
		if (pc->Code != ECode::StackSpace) //TODO:删了pc会慢，但手机可能会快
		{
			return nullptr;
		}
		int leavePoint = 0; //由于首指令是插入的StackSpace，所以leavePoint不可能等于0
		int localsCount = (pc->Operand >> 16);
		int maxStack = (pc->Operand & 0xFFFF);
		
		int argumentPos = (int)(argumentBase - evaluationStackBase);
		if (argumentPos + argsCount + localsCount + maxStack > MAX_EVALUATION_STACK_SIZE)
		{
			return nullptr;
		}

		FStackVariable* localBase = argumentBase + argsCount;
		FStackVariable* evaluationStackPointer = localBase + localsCount;
		
		pc++;

		// VMExecute ^a = gcnew VMExecute();
		// int sum = a->demoAdd(2, 3);

		while (true) //TODO: 常用指令应该放前面
		{
			ECode code = pc->Code;
			switch (code)
			{
			case ECode::Ldarg:
			{
				copy(evaluationStackBase, evaluationStackPointer, argumentBase + pc->Operand,
					managedStack);
				evaluationStackPointer++;
			}
			break;
			case ECode::Call:// Call: 8.1233%
			{
				int narg = pc->Operand >> 16;
				int methodIndexToCall = pc->Operand & 0xFFFF;
				evaluationStackPointer = Execute(methodTable[methodIndexToCall],
					evaluationStackPointer - narg, managedStack, evaluationStackBase, narg,
					methodTable);
			}
			break;
			case ECode::CallExtern://部分来自Call部分来自Callvirt
			case ECode::Newobj: // 2.334642%
				assert(false);
				/*
				int methodId = pc->Operand & 0xFFFF;
				if (code == ECode::Newobj)
				{
					var method = externMethods[methodId];
					if (method.DeclaringType.BaseType == typeof(MulticastDelegate)) // create delegate
					{
						var pm = evaluationStackPointer - 1;
						var po = pm - 1;
						var o = managedStack[po->Value1];
						managedStack[po - evaluationStackBas7e] = null;
						Delegate del = null;
						if (pm->Type == ValueType.Integer)
						{
							//_Info("new closure!");
							del = wrappersManager.CreateDelegate(method.DeclaringType, pm->Value1, o);
							if (del == null)
							{
								del = GenericDelegateFactory.Create(method.DeclaringType, this,
									pm->Value1, o);
							}
							if (del == null)
							{
								throwRuntimeException(
									new InvalidProgramException("no closure wrapper for "
										+ method.DeclaringType), true);
							}
						}
						//else if (pm->Type == ValueType.Float) // 
						//{
						//    del = GetGlobalWrappersManager().CreateDelegate(method.DeclaringType,
						//        pm->Value1, null);
						//    if (del == null)
						//    {
						//        throwRuntimeException(new InvalidProgramException(
						//            "no closure wrapper for " + method.DeclaringType), true);
						//    }
						//}
						else
						{
							var mi = managedStack[pm->Value1] as MethodInfo;
							managedStack[pm - evaluationStackBase] = null;
							del = Delegate.CreateDelegate(method.DeclaringType, o, mi);
						}
						po->Value1 = (int)(po - evaluationStackBase);
						managedStack[po->Value1] = del;
						evaluationStackPointer = pm;
						break;
					}
				}
				int paramCount = pc->Operand >> 16;
				var externInvokeFunc = externInvokers[methodId];
				if (externInvokeFunc == null)
				{
					externInvokers[methodId] = externInvokeFunc
						= (new ReflectionMethodInvoker(externMethods[methodId])).Invoke;
				}
				//Info("call extern: " + externMethods[methodId]);
				var top = evaluationStackPointer - paramCount;
				//for(int kk = 0; kk < paramCount; kk++)
				//{
				//    string info = "arg " + kk + " " + (top + kk)->Type.ToString() + ": ";
				//    if ((top + kk)->Type >= ValueType.Object)
				//    {
				//        var o = managedStack[(top + kk)->Value1];
				//        info += "obj(" + (o == null ? "null" : o.GetHashCode().ToString()) + ")";
				//    }
				//    else
				//    {
				//        info += (top + kk)->Value1;
				//    }
				//    Info(info);
				//}
				Call call = new Call()
				{
					argumentBase = top,
					currentTop = top,
					managedStack = managedStack,
					evaluationStackBase = evaluationStackBase
				};
				//调用外部前，需要保存当前top，以免外部从新进入内部时覆盖栈
				ThreadStackInfo.Stack.UnmanagedStack->Top = evaluationStackPointer;
				externInvokeFunc(this, ref call, code == Code.Newobj);
				evaluationStackPointer = call.currentTop;
				*/
				break;
			case ECode::Ldloc:
			{
				copy(evaluationStackBase, evaluationStackPointer, localBase + pc->Operand,
					managedStack);
				evaluationStackPointer++;
			}
			break;
			case ECode::Stloc:
			{
				evaluationStackPointer--;
				//print("+++before stloc", locs + ins.Operand);
				store(evaluationStackBase, localBase + pc->Operand, evaluationStackPointer,
					managedStack);
				//print("+++after stloc", locs + ins.Operand);
				if (managedStack != nullptr)
					managedStack[evaluationStackPointer - evaluationStackBase] = nullptr;
			}
			break;
			case ECode::Ldc_I4:
			{
				evaluationStackPointer->Value1 = pc->Operand; //高位不清除
				evaluationStackPointer->Type = EVarType::Integer;
				evaluationStackPointer++;
			}
			break;
			case ECode::Blt: //Blt_S:0.4835447% Blt:0.04465406% 
			{
				FStackVariable* b = evaluationStackPointer - 1;
				FStackVariable* a = evaluationStackPointer - 1 - 1;
				evaluationStackPointer = a;
				bool transfer = FStackVariable::Less(evaluationStackPointer->Type, a, b);
				if (transfer)
				{
					pc += pc->Operand;
					continue;
				}
			}
			break;
			case ECode::Add:
			{
				FStackVariable* b = evaluationStackPointer - 1;
				FStackVariable* a = evaluationStackPointer - 1 - 1;
				evaluationStackPointer = a;
				FStackVariable::Add(evaluationStackPointer, a, b);
				evaluationStackPointer++;
			}
			break;
			case ECode::Br://Br_S:1.162784% Br:0.2334108%
			{
				pc += pc->Operand;
			}
			continue;
			case ECode::Ble://Ble_S:0.2581396%  Ble:0.0152998%
			{
				FStackVariable* b = evaluationStackPointer - 1;
				FStackVariable* a = evaluationStackPointer - 1 - 1;
				evaluationStackPointer = a;
				bool transfer = FStackVariable::LessEqual(evaluationStackPointer->Type, a, b);				
				if (transfer)
				{
					pc += pc->Operand;
					continue;
				}
			}
			break;
			case ECode::Rem: //0.04714472%
			{
				FStackVariable* rhs = evaluationStackPointer - 1;
				FStackVariable* lhs = rhs - 1;

				FStackVariable::Rem(lhs, rhs);

				evaluationStackPointer = rhs;
			}
			break;
			case ECode::Mul://0.2389259%
			{
				FStackVariable* b = evaluationStackPointer - 1;
				FStackVariable* a = evaluationStackPointer - 1 - 1;
				evaluationStackPointer = a;
				FStackVariable::Mul(evaluationStackPointer, a, b);
				evaluationStackPointer++;
			}
			break;
			case ECode::Starg://Starg_S:0.1551328 %
			{
				evaluationStackPointer--;
				store(evaluationStackBase, argumentBase + pc->Operand, evaluationStackPointer,
					managedStack);
				if (managedStack)
				{
					managedStack[evaluationStackPointer - evaluationStackBase] = nullptr;
				}
			}
			break;
			case ECode::Ret:// 5.5% TODO: 分为带返回值和不带返回值
			{
				//TODO: 可优化? 检查到没都是基本类型后改指令

				if (topWriteBack != nullptr)
				{
					*topWriteBack = argumentBase - refCount;
				}
				
				if (pc->Operand != 0)
				{
					*argumentBase = *(evaluationStackPointer - 1);
					if (argumentBase->Type == EVarType::Object
						|| argumentBase->Type == EVarType::ValueType)
					{
						int resultPos = argumentBase->Value1;
						if (resultPos != argumentPos)
						{
							if (managedStack != nullptr)
								managedStack[argumentPos] = managedStack[resultPos];
							//managedStack[resultPos] = null;
						}
						argumentBase->Value1 = argumentPos;
					}
					for (int i = 0; i < evaluationStackPointer - evaluationStackBase - 1; i++)
					{
						if (managedStack != nullptr)
							managedStack[i + argumentPos + 1] = nullptr;
					}

					return argumentBase + 1;
				}
				else
				{
					for (int i = 0; i < evaluationStackPointer - evaluationStackBase; i++)
					{
						if (managedStack != nullptr)
							managedStack[i + argumentPos] = nullptr;
					}
					return argumentBase;
				}
			}
			break;
			default:
				code_not_implement(code);
				break;
			}
			pc++;
		}

		return nullptr;
	}
}

using namespace ILVM;

#define EXPORT			__declspec(dllexport)

extern "C" EXPORT FStackVariable* SDK_FILVirtualMachine_Execute(Instruction * pc, FStackVariable * argumentBase, FManagedObject * managedStack,
	FStackVariable * evaluationStackBase, int argsCount, Instruction **methodTable,
	int refCount = 0, FStackVariable * *topWriteBack = nullptr)
{
	return FILVirtualMachine::Execute(pc, argumentBase, managedStack,
		evaluationStackBase, argsCount, methodTable,
		refCount, topWriteBack);
}