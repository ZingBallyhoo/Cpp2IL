namespace ReadExceptionInfo.Tracing
{
    public class ConstantStringValue : IInternalValue
    {
        public readonly string m_string;

        public ConstantStringValue(string str)
        {
            m_string = str;
        }
    }
}