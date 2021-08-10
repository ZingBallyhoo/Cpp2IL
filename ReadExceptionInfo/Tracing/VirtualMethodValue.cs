using Mono.Cecil;

namespace ReadExceptionInfo.Tracing
{
    public class VirtualMethodValue : IInternalValue
    {
        public readonly MethodDefinition m_method;

        public VirtualMethodValue(MethodDefinition methodDefinition)
        {
            m_method = methodDefinition;
        }
    }
}