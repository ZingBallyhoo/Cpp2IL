using System;
using System.Diagnostics;
using Iced.Intel;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public class ClearRegAction : ImmediateToRegBase
    {
        public ClearRegAction(Register register) : base(register)
        {
        }

        public override Mono.Cecil.Cil.Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            Debug.Assert(typeSpec != null);

            if (typeSpec is KnownManagedTypeSpec managedType && managedType.IsReferenceType())
            {
                return new[]
                {
                    processor.Create(OpCodes.Ldnull)
                };
            }
            
            if (typeSpec is not HintedImmediateTypeSpec hintedImmediateType)
            {
                throw new NotImplementedException(typeSpec.ToString());
            }
            
            if (hintedImmediateType.m_immediateType == ImmediateType.Int32)
            {
                return new[]
                {
                    processor.Create(OpCodes.Ldc_I4_0)
                };
            } /*else if (hintedImmediateType.m_immediateType == ImmediateType.Int64)
            {
                return new[]
                {
                    processor.Create(OpCodes.Ldc_I8, 0L)
                };
            }*/ else
            {
                throw new NotImplementedException(m_guessedImmediate.ToString());
            }
        }

        public override string ToString()
        {
            return "0";
        }
    }
}