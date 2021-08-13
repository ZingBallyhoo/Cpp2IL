using ReadExceptionInfo.Actions;

namespace ReadExceptionInfo.Tracing
{
    public class NewlyCreatedReferenceTypeValue : ReferenceTypeValue
    {
        public readonly ConcreteTypeDefinitionValue m_type;
        public readonly AllocateObjectAction m_allocateInstruction;

        public NewlyCreatedReferenceTypeValue(ConcreteTypeDefinitionValue type, AllocateObjectAction allocate) : base(type.m_base)
        {
            m_type = type;
            m_allocateInstruction = allocate;
        }
    }
}