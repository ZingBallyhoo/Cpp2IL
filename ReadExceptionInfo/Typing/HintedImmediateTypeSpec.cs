namespace ReadExceptionInfo.Typing
{
    public class HintedImmediateTypeSpec : ITypeSpec
    {
        public ImmediateType m_immediateType;

        public HintedImmediateTypeSpec(ImmediateType type)
        {
            m_immediateType = type;
        }
    }
}