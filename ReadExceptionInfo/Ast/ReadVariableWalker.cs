using System.Collections.Generic;
using Echo.Ast;
using Echo.Core.Code;

namespace ReadExceptionInfo.Ast
{
    internal sealed class ReadVariableWalker<TInstruction> : AstNodeWalkerBase<TInstruction>
    {
        internal int Count => Variables.Count;

        internal HashSet<IVariable> Variables
        {
            get;
        } = new HashSet<IVariable>();

        public void Clear() => Variables.Clear();

        protected override void VisitVariableExpression(VariableExpression<TInstruction> variableExpression) =>
            Variables.Add(variableExpression.Variable);
    }
}