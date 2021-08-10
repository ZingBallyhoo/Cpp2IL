using Iced.Intel;

namespace ReadExceptionInfo.Tracing
{
    public class ConstantComparandValue : IInternalValue
    {
        public readonly ulong m_value;
        
        public ConstantComparandValue(ulong value)
        {
            m_value = value;
        }
    }

    public record RegisterComparandValue(Register m_register) : IInternalValue;
}