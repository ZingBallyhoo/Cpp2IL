using System.Diagnostics;
using Cpp2IL.Core;
using Iced.Intel;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Tracing;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public class NewObjectAction : CallConcreteManagedFunctionBase
    {
        public readonly TypeDefinition m_type;
        
        public NewObjectAction(ConcreteTypeDefinitionValue ofType)
        {
            m_type = ofType.m_base;
        }

        public override Register[] GetReadRegisters() => new Register[] { };
        public override Register[] GetWrittenRegisters() => new[] {Register.RAX};

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

        public void SetConstructor(MethodDefinition? methodInfo)
        {
            Debug.Assert(m_method == null);
            m_method = methodInfo;
        }

        public override string ToString()
        {
            return $"newobj {m_type} {m_method}";
        }
    }
}