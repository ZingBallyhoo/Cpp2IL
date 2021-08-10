namespace ReadExceptionInfo.Tracing
{
    public class StaticFieldAtOffsetValue : IInternalValue
    {
        public readonly ConcreteTypeDefinitionValue m_forType;
        public readonly uint m_offset;

        public StaticFieldAtOffsetValue(ConcreteTypeDefinitionValue forType, uint offset)
        {
            m_forType = forType;
            m_offset = offset;
        }
    }
}