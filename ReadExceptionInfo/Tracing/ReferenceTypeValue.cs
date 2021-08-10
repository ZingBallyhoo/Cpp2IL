using System;
using Mono.Cecil;

namespace ReadExceptionInfo.Tracing
{
    public class ReferenceTypeValue : IStructurePointer
    {
        public readonly TypeDefinition m_type;

        public ReferenceTypeValue(TypeDefinition ofType)
        {
            m_type = ofType;
        }
        
        public IInternalValue GetValueAtOffset(uint offset)
        {
            if (offset == 0) return new RuntimeTypeDefinitionValue(m_type);
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return $"object of type {m_type}";
        }
    }
}