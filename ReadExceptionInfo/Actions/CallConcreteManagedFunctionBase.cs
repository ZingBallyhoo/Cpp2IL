using System;
using System.Diagnostics;
using Iced.Intel;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public abstract class CallConcreteManagedFunctionBase : LiftedAction, IPreserveAction
    {
        protected MethodDefinition? m_method;
        private bool m_isVoid => GetMethod().ReturnType.FullName == "System.Void";
        
        public virtual Register[] GetReadRegisters() => new[] {Register.RCX/*, Register.RDX, Register.R8, Register.R9*/};
        
        public MethodDefinition GetMethod()
        {
            if (m_method == null) throw new NullReferenceException(nameof(m_method));
            return m_method;
        }

        public override string ToString()
        {
            return $"{m_method}";
        }

        public virtual Register[] GetWrittenRegisters()
        {
            if (m_isVoid) return Array.Empty<Register>();
            return new[] {Register.RAX};
        }

        public virtual ITypeSpec GetInitialTypeSpec()
        {
            Debug.Assert(!m_isVoid);
            return new KnownManagedTypeSpec(GetMethod().ReturnType.Resolve());
        }

        public abstract Mono.Cecil.Cil.Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec);
    }
}