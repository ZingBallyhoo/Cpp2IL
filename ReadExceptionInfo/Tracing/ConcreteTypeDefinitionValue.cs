using Mono.Cecil;

namespace ReadExceptionInfo.Tracing
{
    public class ConcreteTypeDefinitionValue : RuntimeTypeDefinitionValue
    {
        public ConcreteTypeDefinitionValue(TypeDefinition typeDefinition) : base(typeDefinition)
        {
        }
        
        public override IInternalValue GetValueAtOffset(uint offset)
        {
            if (offset == 0xB8) return new StaticFieldsPointerValue(this);
            return base.GetValueAtOffset(offset);
        }

        public override string ToString()
        {
            return $"runtimetype {m_base}";
        }
    }
}