using System;
using System.Collections.Generic;
using Cpp2IL.Core;
using Iced.Intel;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Tracing;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public class ConstructObjectAction : CallConcreteManagedFunctionBase
    {
        public readonly TypeDefinition m_type;
        
        public ConstructObjectAction(ConcreteTypeDefinitionValue ofType, MethodDefinition method)
        {
            m_type = ofType.m_base;
            m_method = method;
        }

        public override Register[] GetReadRegisters()
        {
            // todo: basic impl. todo: what regs are used for int and float interlaced
            
            var parameters = GetMethod().Parameters;

            var list = new List<Register>();
            for (var i = 0; i < parameters.Count; i++)
            {
                if (i == 0) list.Add(Register.RDX);
                else if (i == 1) list.Add(Register.R8);
                else if (i == 2) list.Add(Register.R9);
                else throw new NotImplementedException();
            }
            return list.ToArray();
        }
        public override Register[] GetWrittenRegisters() => new [] {Register.RAX}; // todo: managed only, c++ is void

        public override Mono.Cecil.Cil.Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            return new[]
            {
                processor.Create(OpCodes.Newobj, processor.ImportReference(GetMethod()))
            };
        }
        
        public override ITypeSpec GetInitialTypeSpec()
        {
            return new KnownManagedTypeSpec(m_type);
        }
        
        public override string ToString()
        {
            return $"newobj {m_type} {m_method}";
        }
    }
}