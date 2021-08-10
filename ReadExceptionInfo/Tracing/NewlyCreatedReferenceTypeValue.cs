using ReadExceptionInfo.Actions;

namespace ReadExceptionInfo.Tracing
{
    public class NewlyCreatedReferenceTypeValue : ReferenceTypeValue
    {
        public readonly NewObjectAction m_createdFromAction;

        public NewlyCreatedReferenceTypeValue(NewObjectAction action) : base(action.m_type)
        {
            m_createdFromAction = action;
        }
    }
}