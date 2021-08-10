using System;
using Cpp2IL.Core;
using Cpp2IL.Core.Analysis;
using Mono.Cecil;

namespace ReadExceptionInfo.Tracing
{
    public class RuntimeTypeDefinitionValue : IStructurePointer
    {
        public readonly TypeDefinition m_base;
        
        public RuntimeTypeDefinitionValue(TypeDefinition typeDefinition)
        {
            m_base = typeDefinition;
        }
        
        public virtual IInternalValue GetValueAtOffset(uint offset)
        {
            var slotNum = Utils.GetSlotNum((int)offset);
            var methodPointerRead = MethodUtils.GetMethodFromVtableSlot(SharedState.ManagedToUnmanagedTypes[m_base], slotNum);

            if (methodPointerRead == null)
            {
                throw new NotImplementedException(offset.ToString());
            }

            return new VirtualMethodValue(methodPointerRead);
        }
        
        public override string ToString()
        {
            return $"runtimetype from variable with base of {m_base}";
        }
    }
}