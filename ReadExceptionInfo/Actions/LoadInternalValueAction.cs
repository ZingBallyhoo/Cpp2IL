using System;
using System.Diagnostics;
using Cpp2IL.Core;
using Iced.Intel;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Tracing;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public class LoadInternalValueAction : LiftedAction
    {
        private readonly Register m_register;
        public readonly IInternalValue m_value;
        
        public LoadInternalValueAction(Register output, IInternalValue value)
        {
            m_register = output;
            m_value = value;
        }

        public Register[] GetReadRegisters() => new Register[0]; // we manage everything here, doesn't matter
        public Register[] GetWrittenRegisters() => new[] {m_register};
        
        public ITypeSpec GetInitialTypeSpec()
        {
            if (m_value is ConcreteTypeDefinitionValue)
            {
                var runtimeTypeHandle = Utils.TryLookupTypeDefKnownNotGeneric("System.RuntimeTypeHandle");
                Debug.Assert(runtimeTypeHandle != null);
                return new KnownManagedTypeSpec(runtimeTypeHandle);
            } else if (m_value is IStaticFieldValue staticField)
            {
                return new KnownManagedTypeSpec(staticField.m_field.FieldType.Resolve());
            } else if (m_value is ConstantComparandValue)
            {
                return new HintedImmediateTypeSpec(ImmediateType.Int32);
            }
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return $"Load Internal {m_value}";
        }
        
        public Mono.Cecil.Cil.Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            if (m_value is ConcreteTypeDefinitionValue concreteTypeDefinitionValue)
            {
                return new[]
                {
                    processor.Create(OpCodes.Ldtoken, processor.ImportReference(concreteTypeDefinitionValue.m_base))
                };
            } else if (m_value is IStaticFieldValue staticField)
            {
                return new[]
                {
                    processor.Create(OpCodes.Ldsfld, processor.ImportReference(staticField.m_field))
                };
            } else if (m_value is ConstantComparandValue constantComparand)
            {
                return new[]
                {
                    processor.Create(OpCodes.Ldc_I4, (int)constantComparand.m_value)
                };
            }
            throw new NotImplementedException(); // todo:
        }
        
        //public LocalVariable MakeLocal()
        //{
        //    if (m_value is ConcreteTypeDefinitionValue)
        //    {
        //        return new LocalVariable(TypeCompatibilitySet.FromSingle(Utils.TryLookupTypeDefKnownNotGeneric("System.RuntimeTypeHandle")));
        //    }
        //    throw new System.NotImplementedException();
        //}
    }
}