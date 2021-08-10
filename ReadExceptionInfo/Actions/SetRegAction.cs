using System;
using System.Diagnostics;
using Iced.Intel;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public class SetRegAction : ImmediateToRegBase
    {
        private readonly ulong m_value;
        
        public SetRegAction(Register register, ulong value) : base(register)
        {
            m_value = value;
        }

        public override Mono.Cecil.Cil.Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            Debug.Assert(typeSpec != null);
            
            if (typeSpec is not HintedImmediateTypeSpec hintedImmediateType)
            {
                throw new NotImplementedException(typeSpec.ToString());
            }
            
            if (hintedImmediateType.m_immediateType == ImmediateType.Int32)
            {
                return new[]
                {
                    processor.Create(OpCodes.Ldc_I4, (int)m_value)
                };
            } else
            {
                throw new NotImplementedException(m_guessedImmediate.ToString());
            }
        }

        public override string ToString()
        {
            return $"{m_value}";
        }
    }
}