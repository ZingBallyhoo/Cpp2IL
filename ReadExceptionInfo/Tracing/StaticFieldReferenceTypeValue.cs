using Mono.Cecil;

namespace ReadExceptionInfo.Tracing
{
    public class StaticFieldReferenceTypeValue : ReferenceTypeValue, IStaticFieldValue
    {
        public FieldDefinition m_field { get; }

        public StaticFieldReferenceTypeValue(FieldDefinition field) : base(field.FieldType.Resolve())
        {
            m_field = field;
        }

        public override string ToString()
        {
            return m_field.ToString();
        }
    }
}