using System;
using Iced.Intel;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public abstract class ImmediateToRegBase : LiftedAction
    {
        protected readonly Register m_register;
        protected readonly ImmediateType m_guessedImmediate;

        public ImmediateToRegBase(Register register)
        {
            m_register = RegisterExtensions.GetFullRegister(register);
            m_guessedImmediate = ImmediateTypeFromRegister(register);
        }
        
        public virtual Register[] GetReadRegisters() => new Register[0];
        public virtual Register[] GetWrittenRegisters() => new[] {m_register};
        public ITypeSpec GetInitialTypeSpec() => new HintedImmediateTypeSpec(m_guessedImmediate);

        public static ImmediateType ImmediateTypeFromRegister(Register register)
        {
            if (RegisterExtensions.IsGPR32(register))
            {
                // could also be int64, we will find out later 
                return ImmediateType.Int32;
            } else if (RegisterExtensions.IsGPR64(register))
            {
                return ImmediateType.Int64;
            } else if (RegisterExtensions.IsGPR16(register))
            {
                return ImmediateType.Int16;
            } else if (RegisterExtensions.IsGPR8(register))
            {
                return ImmediateType.Int8;
            } else
            {
                throw new NotImplementedException(register.ToString());
            }
        } 
        
        public abstract Mono.Cecil.Cil.Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec);
    }
}