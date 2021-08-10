using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cpp2IL.Core;
using Iced.Intel;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Tracing;
using ReadExceptionInfo.Typing;
using Instruction = Mono.Cecil.Cil.Instruction;

namespace ReadExceptionInfo.Actions
{
    public class CallManagedVirtualFunctionAction : LiftedAction, IPreserveAction
    {
        private readonly MethodDefinition m_method;
        private bool m_isVoid => m_method.ReturnType.FullName == "System.Void";
        
        public CallManagedVirtualFunctionAction(VirtualMethodValue virtualMethod)
        {
            m_method = virtualMethod.m_method;
        }
        
        public Register[] GetReadRegisters() => new[] {Register.RCX/*, Register.RDX, Register.R8*/};
        public virtual Register[] GetWrittenRegisters()
        {
            if (m_isVoid) return Array.Empty<Register>();
            return new[] {Register.RAX};
        }
        
        public virtual ITypeSpec GetInitialTypeSpec()
        {
            Debug.Assert(!m_isVoid);
            return new KnownManagedTypeSpec(m_method.ReturnType.Resolve());
        }

        public Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            return new[]
            {
                processor.Create(OpCodes.Callvirt, processor.ImportReference(m_method))
            };
        }

        public IEnumerable<Instruction> PushResults()
        {
            throw new NotImplementedException();
        }
    }
}