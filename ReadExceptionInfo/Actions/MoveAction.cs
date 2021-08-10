using System;
using Iced.Intel;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;
using Instruction = Mono.Cecil.Cil.Instruction;

namespace ReadExceptionInfo.Actions
{
    public class MoveAction : LiftedAction
    {
        private Register m_from;
        private Register m_to;
        
        public MoveAction(Register from, Register to)
        {
            m_from = RegisterExtensions.GetFullRegister(from);
            m_to = RegisterExtensions.GetFullRegister(to);
        }

        public Register[] GetReadRegisters() => new[] {m_from};
        public Register[] GetWrittenRegisters() => new[] {m_to};
        
        public ITypeSpec GetInitialTypeSpec()
        {
            throw new NotImplementedException();
        }

        public Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            return Array.Empty<Instruction>();
        }

        public override string ToString()
        {
            return "";
        }
    }
}