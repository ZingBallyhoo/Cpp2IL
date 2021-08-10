using System;
using Iced.Intel;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public class BranchAlwaysAction : LiftedAction, IBranchInstruction
    {
        public Register[] GetReadRegisters() => Array.Empty<Register>();
        public Register[] GetWrittenRegisters() => Array.Empty<Register>();

        public ITypeSpec GetInitialTypeSpec()
        {
            throw new NotImplementedException();
        }

        public Mono.Cecil.Cil.Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            return new[]
            {
                processor.Create(OpCodes.Br, processor.Create(OpCodes.Nop))
            };
        }
    }
}