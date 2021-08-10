using Mono.Cecil;

namespace ReadExceptionInfo.Tracing
{
    public interface IStaticFieldValue
    {
        public FieldDefinition m_field { get; }
    }
}