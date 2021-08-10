using Iced.Intel;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public interface LiftedAction
    {
        Register[] GetReadRegisters();
        Register[] GetWrittenRegisters();
        
        ITypeSpec GetInitialTypeSpec();
        Mono.Cecil.Cil.Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec);
    }
}