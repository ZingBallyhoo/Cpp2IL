using System;
using System.Diagnostics;
using Iced.Intel;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;
using Instruction = Mono.Cecil.Cil.Instruction;

namespace ReadExceptionInfo.Actions
{
    public class AllocateObjectAction : LiftedAction, IDeferVariableRead
    {
        private LiftedAction? m_targetAction;

        public Register[] GetReadRegisters() => Array.Empty<Register>();
        public Register[] GetWrittenRegisters() => new[] { Register.RAX };

        public ITypeSpec GetInitialTypeSpec()
        {
            throw new NotImplementedException();
        }

        public Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            throw new NotImplementedException();
        }

        public void SetTargetAction(LiftedAction action)
        {
            Debug.Assert(m_targetAction == null);
            m_targetAction = action;
        }

        public LiftedAction ToAction()
        {
            Debug.Assert(m_targetAction != null);
            return m_targetAction;
        }
    }
}