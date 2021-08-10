using Mono.Cecil;

namespace ReadExceptionInfo.Typing
{
    public class KnownManagedTypeSpec : ITypeSpec
    {
        public readonly TypeDefinition m_baseManagedType;

        public KnownManagedTypeSpec(TypeDefinition baseType)
        {
            m_baseManagedType = baseType;
        }

        public bool IsReferenceType()
        {
            return !m_baseManagedType.IsValueType;
        }
    }
}