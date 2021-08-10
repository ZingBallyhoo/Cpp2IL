using System;
using Mono.Cecil;

namespace ReadExceptionInfo.Tracing
{
    public class StaticFieldValueTypeValue : IStructurePointer, IStaticFieldValue
    {
        public FieldDefinition m_field { get; }

        public StaticFieldValueTypeValue(FieldDefinition field)
        {
            m_field = field;
        }

        public IInternalValue GetValueAtOffset(uint offset)
        {
            throw new NotImplementedException();
        }
    }
}