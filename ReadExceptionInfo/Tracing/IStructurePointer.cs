namespace ReadExceptionInfo.Tracing
{
    public interface IStructurePointer : IInternalValue
    {
        IInternalValue GetValueAtOffset(uint offset);
    }
}