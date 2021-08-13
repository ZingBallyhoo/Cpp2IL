using System.Collections.Generic;
using Echo.Ast;
using Echo.Core.Code;

namespace ReadExceptionInfo.Ast
{
    public sealed class WrittenVariableWalker<TInstruction> : AstNodeWalkerBase<TInstruction>
    {
        public int Count => Variables.Count;
        
        public HashSet<IVariable> Variables
        {
            get;
        } = new HashSet<IVariable>();

        public void Clear() => Variables.Clear();

        protected override void ExitAssignmentStatement(AssignmentStatement<TInstruction> assignmentStatement)
        {
            foreach (var target in assignmentStatement.Variables)
                Variables.Add(target);
        }

        protected override void ExitPhiStatement(PhiStatement<TInstruction> phiStatement) =>
            Variables.Add(phiStatement.Target);
    }
}