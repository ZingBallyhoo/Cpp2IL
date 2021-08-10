using System;
using System.Diagnostics;
using Iced.Intel;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public class AddConstantAction : ImmediateToRegBase
    {
        private readonly ulong m_amount;
        
        public AddConstantAction(Register register, ulong toAdd) : base(register)
        {
            m_amount = toAdd;
        }

        public override Register[] GetReadRegisters() => new[] {m_register};
        public override Register[] GetWrittenRegisters() => new[] {m_register};
        
        public override Mono.Cecil.Cil.Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            Debug.Assert(typeSpec != null);
            if (typeSpec is not HintedImmediateTypeSpec hintedImmediateTypeSpec)
            {
                throw new NotImplementedException();
            }
            Debug.Assert(hintedImmediateTypeSpec.m_immediateType == ImmediateType.Int32); // todo
            
            return new[]
            {
                processor.Create(OpCodes.Ldc_I4, (int)m_amount),
                processor.Create(OpCodes.Add)
            };
        }

        public override string ToString()
        {
            return $"add {m_amount}";
        }
    }
}