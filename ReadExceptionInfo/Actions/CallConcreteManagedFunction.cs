using Cpp2IL.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ReadExceptionInfo.Typing;

namespace ReadExceptionInfo.Actions
{
    public class CallConcreteManagedFunction : CallConcreteManagedFunctionBase
    {
        public CallConcreteManagedFunction(MethodDefinition method)
        {
            m_method = method;

            //var returnTypes = TypeCompatibilitySet.FromOptions(possibleFunctions.Select(x => x.ReturnType.Resolve()).ToHashSet());

            //m_varID = TempVars.Next(SharedState.MethodsByAddress[address].ReturnType.Resolve());
        }
        
        public override Instruction[] EmitIL(ILProcessor processor, ITypeSpec? typeSpec)
        {
            return new[]
            {
                processor.Create(OpCodes.Call, processor.ImportReference(GetMethod()))
            };
        }
    }
}