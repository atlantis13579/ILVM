#pragma once

#include <cmath>

namespace ILVM
{
	enum class ECode : int
	{
		Nop,
		Break,
		Ldarg,
		Ldloc,
		Stloc,
		Ldarga,
		Starg,
		Ldloca,
		Ldnull,
		Ldc_I4,
		Ldc_I8,
		Ldc_R4,
		Ldc_R8,
		Dup,
		Pop,
		Jmp,
		Call,
		CallExtern,
		//Calli,
		Ret,
		Br,
		Brfalse,
		Brtrue,
		Beq,
		Bge,
		Bgt,
		Ble,
		Blt,
		Bne_Un,
		Bge_Un,
		Bgt_Un,
		Ble_Un,
		Blt_Un,
		Switch,
		Ldind_I1,
		Ldind_U1,
		Ldind_I2,
		Ldind_U2,
		Ldind_I4,
		Ldind_U4,
		Ldind_I8,
		Ldind_I,
		Ldind_R4,
		Ldind_R8,
		Ldind_Ref,
		Stind_Ref,
		Stind_I1,
		Stind_I2,
		Stind_I4,
		Stind_I8,
		Stind_R4,
		Stind_R8,
		Add,
		Sub,
		Mul,
		Div,
		Div_Un,
		Rem,
		Rem_Un,
		And,
		Or,
		Xor,
		Shl,
		Shr,
		Shr_Un,
		Neg,
		Not,
		Conv_I1,
		Conv_I2,
		Conv_I4,
		Conv_I8,
		Conv_R4,
		Conv_R8,
		Conv_U4,
		Conv_U8,
		Callvirt,
		Callvirtvirt,
		Ldvirtftn2,
		Cpobj,
		Ldobj,
		Ldstr,
		Newobj,
		Castclass,
		Isinst,
		Conv_R_Un,
		Unbox,
		Throw,
		Ldfld,
		Ldflda,
		Stfld,
		Ldsfld,
		Ldsflda,
		Stsfld,
		Stobj,
		Conv_Ovf_I1_Un,
		Conv_Ovf_I2_Un,
		Conv_Ovf_I4_Un,
		Conv_Ovf_I8_Un,
		Conv_Ovf_U1_Un,
		Conv_Ovf_U2_Un,
		Conv_Ovf_U4_Un,
		Conv_Ovf_U8_Un,
		Conv_Ovf_I_Un,
		Conv_Ovf_U_Un,
		Box,
		Newarr,
		Ldlen,
		Ldelema,
		Ldelem_I1,
		Ldelem_U1,
		Ldelem_I2,
		Ldelem_U2,
		Ldelem_I4,
		Ldelem_U4,
		Ldelem_I8,
		Ldelem_I,
		Ldelem_R4,
		Ldelem_R8,
		Ldelem_Ref,
		Stelem_I,
		Stelem_I1,
		Stelem_I2,
		Stelem_I4,
		Stelem_I8,
		Stelem_R4,
		Stelem_R8,
		Stelem_Ref,
		Ldelem_Any,
		Stelem_Any,
		Unbox_Any,
		Conv_Ovf_I1,
		Conv_Ovf_U1,
		Conv_Ovf_I2,
		Conv_Ovf_U2,
		Conv_Ovf_I4,
		Conv_Ovf_U4,
		Conv_Ovf_I8,
		Conv_Ovf_U8,
		Refanyval,
		Ckfinite,
		Mkrefany,
		Ldtoken,
		Ldtype, // custom
		Conv_U2,
		Conv_U1,
		Conv_I,
		Conv_Ovf_I,
		Conv_Ovf_U,
		Add_Ovf,
		Add_Ovf_Un,
		Mul_Ovf,
		Mul_Ovf_Un,
		Sub_Ovf,
		Sub_Ovf_Un,
		Endfinally,
		Leave,
		Stind_I,
		Conv_U,
		Arglist,
		Ceq,
		Cgt,
		Cgt_Un,
		Clt,
		Clt_Un,
		Ldftn,
		Newanon,
		Ldvirtftn,
		Localloc,
		Endfilter,
		Unaligned,
		Volatile,
		Tail,
		Initobj,
		Constrained,
		Cpblk,
		Initblk,
		No,
		Rethrow,
		Sizeof,
		Refanytype,
		Readonly,

		//Pseudo instruction
		StackSpace,
	};

	enum class EVarType : int
	{
		Integer,
		Long,
		Float,
		Double,
		StackReference,//Value = pointer, 
		StaticFieldReference,
		FieldReference,//Value1 = objIdx, Value2 = fieldIdx
		ChainFieldReference,
		Object,        //Value1 = objIdx
		ValueType,     //Value1 = objIdx
		ArrayReference,//Value1 = objIdx, Value2 = elemIdx


		UnmanagedStruct = ValueType,
	};

	template<typename T>
	EVarType GetVarType() {
		return EVarType::Integer;
	}
	template<>
	EVarType GetVarType<char>() {
		return EVarType::Integer;
	}
	template<>
	EVarType GetVarType<short>() {
		return EVarType::Integer;
	}
	template<>
	EVarType GetVarType<int>() {
		return EVarType::Integer;
	}
	template<>
	EVarType GetVarType<long long>() {
		return EVarType::Long;
	}
	template<>
	EVarType GetVarType<unsigned char>() {
		return EVarType::Integer;
	}
	template<>
	EVarType GetVarType<unsigned short>() {
		return EVarType::Integer;
	}
	template<>
	EVarType GetVarType<unsigned int>() {
		return EVarType::Integer;
	}
	template<>
	EVarType GetVarType<unsigned long long>() {
		return EVarType::Long;
	}
	template<>
	EVarType GetVarType<float>() {
		return EVarType::Float;
	}
	template<>
	EVarType GetVarType<double>() {
		return EVarType::Double;
	}


	struct FStackVariable
	{
		FStackVariable()
		{
			Type = EVarType::Integer;
			I64Value = 0;
		}
		~FStackVariable()
		{
			Dispose();
		}
		void Dispose()
		{
			if (Type == EVarType::UnmanagedStruct)
			{
				FreeStruct(StructPointer);
				StructPointer = nullptr;
			}
			Type = EVarType::Integer;
		}
		EVarType Type;
		union
		{
			struct
			{
				int Value1;
				int Value2;
			};
			float F32Value;
			int I32Value;
			long long I64Value;			
			double F64Value;
			void* StructPointer;
			void* CSHandle;
		};
		template<typename T>
		void SetValueSmall(const T& v)
		{
			Dispose();
			Type = GetVarType<T>();
			*(T*)(&I64Value) = v;
		}
		template<typename T>
		void SetValueBig(const T& v)
		{
			Dispose();
			Type = EVarType::UnmanagedStruct;
			StructPointer = AllocStruct<T>();			
		}
		template<typename T>
		void* AllocStruct()
		{
			return new T();
		}
		void FreeStruct(void* p)
		{
			delete p;
		}
		static bool LessEqual(EVarType type, FStackVariable* lh, FStackVariable* rh)
		{
			switch (type)
			{
			case EVarType::Integer:
				return lh->I32Value <= rh->I32Value;
			case EVarType::Long:
				return lh->I64Value <= rh->I64Value;
			case EVarType::Float:
				return lh->F32Value <= rh->F32Value;
			case EVarType::Double:
				return lh->F64Value <= rh->F64Value;
			default:
				break;
			}
			return false;
		}
		static bool Less(EVarType type, FStackVariable* lh, FStackVariable* rh)
		{
			switch (type)
			{
			case EVarType::Integer:
				return lh->I32Value < rh->I32Value;
			case EVarType::Long:
				return lh->I64Value < rh->I64Value;
			case EVarType::Float:
				return lh->F32Value < rh->F32Value;
			case EVarType::Double:
				return lh->F64Value < rh->F64Value;
			default:
				break;
			}
			return false;
		}
		static void Rem(FStackVariable* lh, FStackVariable* rh)
		{
			switch (lh->Type)
			{
			case EVarType::Integer:
				lh->I32Value = lh->I32Value % rh->I32Value;
				break;
			case EVarType::Long:
				lh->I64Value = lh->I64Value % rh->I64Value;
				break;
			case EVarType::Float:
				lh->F32Value = std::fmod(lh->F32Value, rh->F32Value);
				break;
			case EVarType::Double:
				lh->F64Value = std::fmod(lh->F64Value, rh->F64Value);
				break;
			default:
				break;
			}
		}
		static void Add(FStackVariable* result, FStackVariable* lh, FStackVariable* rh)
		{
			switch (result->Type)
			{
				case EVarType::Integer:
					result->I32Value = lh->I32Value + rh->I32Value;
					break;
				case EVarType::Long:
					result->I64Value = lh->I64Value + rh->I64Value;
					break;
				case EVarType::Float:
					result->F32Value = lh->F32Value + rh->F32Value;
					break;
				case EVarType::Double:
					result->F64Value = lh->F64Value + rh->F64Value;
					break;
				default:
					break;
			}
		}
		static void Mul(FStackVariable* result, FStackVariable* lh, FStackVariable* rh)
		{
			switch (result->Type)
			{
			case EVarType::Integer:
				result->I32Value = lh->I32Value * rh->I32Value;
				break;
			case EVarType::Long:
				result->I64Value = lh->I64Value * rh->I64Value;
				break;
			case EVarType::Float:
				result->F32Value = lh->F32Value * rh->F32Value;
				break;
			case EVarType::Double:
				result->F64Value = lh->F64Value * rh->F64Value;
				break;
			default:
				break;
			}
		}
	};

	struct Instruction
	{
		ECode Code;
		int Operand;
	};

	typedef void* FManagedObject;

	class FILVirtualMachine
	{
	public:
		static FStackVariable* Execute(Instruction* pc, FStackVariable* argumentBase, FManagedObject* managedStack,
			FStackVariable* evaluationStackBase, int argsCount, Instruction **methodTable,
			int refCount = 0, FStackVariable** topWriteBack = nullptr);

		inline static FManagedObject CloneObject(FManagedObject obj)
		{
			return nullptr;
		}
		inline static void store(FStackVariable* stackBase, FStackVariable* dst, FStackVariable* src, FManagedObject* managedStack)
		{
			*dst = *src;
			if (dst->Type >= EVarType::Object)
			{
				auto obj = (dst->Type == EVarType::ValueType && managedStack[src->Value1] != nullptr) //Nullable box后可能为空
					? CloneObject(managedStack[src->Value1])
					: managedStack[src->Value1];
				auto dstPos = dst->Value1 = (int)(dst - stackBase);
				managedStack[dstPos] = obj;
			}
			else if (dst->Type == EVarType::ChainFieldReference)
			{
				managedStack[dst - stackBase] = managedStack[src - stackBase];
			}
		}
		inline static void copy(FStackVariable* stackBase, FStackVariable* dst, FStackVariable* src, FManagedObject* managedStack)
		{
			*dst = *src;
			if (dst->Type == EVarType::ValueType)
			{
				FManagedObject obj = nullptr;
				if (managedStack[src->Value1] != nullptr) //Nullable box后可能为空
				{
					obj = CloneObject(managedStack[src->Value1]);
				}
				auto dstPos = dst->Value1 = (int)(dst - stackBase);
				managedStack[dstPos] = obj;
			}
			else if (dst->Type == EVarType::ChainFieldReference)
			{
				managedStack[dst - stackBase] = managedStack[src - stackBase];
			}
		}
	};
}
