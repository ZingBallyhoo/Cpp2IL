using System;
using System.Diagnostics;
using System.Linq;
using Cpp2IL.Core;
using LibCpp2IL;
using Mono.Cecil;

namespace ReadExceptionInfo.Tracing
{
    public class StaticFieldsPointerValue : IStructurePointer
    {
        public readonly ConcreteTypeDefinitionValue m_forType;

        public StaticFieldsPointerValue(ConcreteTypeDefinitionValue forType)
        {
            m_forType = forType;
        }

        public IInternalValue GetValueAtOffset(uint offset)
        {
            var field = GetStaticFieldByOffset(offset);
            Debug.Assert(field != null);

            if (field.FieldType.IsValueType)
            {
                return new StaticFieldValueTypeValue(field);
            }
            return new StaticFieldReferenceTypeValue(field);
        }
        
        private FieldDefinition? GetStaticFieldByOffset(uint fieldOffset)
        {
            var type = m_forType.m_base;

            var theFields = SharedState.FieldsByType[type];
            string fieldName;
            try
            {
                fieldName = theFields.SingleOrDefault(f => f.Static && f.Constant == null && f.Offset == fieldOffset).Name;
            }
            catch (InvalidOperationException)
            {
                var matchingFields = theFields.Where(f => f.Static && f.Constant == null && f.Offset == fieldOffset).ToList();
                Logger.ErrorNewline($"FieldUtils#GetStaticFieldByOffset: More than one static field at offset 0x{fieldOffset:X} in type {type}! Matches: " + matchingFields.Select(f => f.Name).ToStringEnumerable());
                return null;
            }

            if (string.IsNullOrEmpty(fieldName)) return null;

            return type.Fields.FirstOrDefault(f => f.IsStatic && f.Name == fieldName);
        }
    }
}