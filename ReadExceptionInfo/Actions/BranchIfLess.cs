using System;
using Iced.Intel;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public class BranchIfLess : LiftedAction, IBranchInstruction
    {
        public Register[] GetReadRegisters() => new[] {Register.DontUse0, Register.DontUseFA};
        public Register[] GetWrittenRegisters() => new Register[0];
        
        public ITypeSpec GetInitialTypeSpec()
        {
            throw new NotImplementedException();
        }

        public Mono.Cecil.Cil.Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            return new[]
            {
                processor.Create(OpCodes.Blt, processor.Create(OpCodes.Nop))
            };
        }
    }
}