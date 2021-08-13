using System.Collections.Generic;
using Echo.Ast;
using Echo.Core.Code;
using ReadExceptionInfo.Actions;

namespace ReadExceptionInfo.Ast
{
    public class VariableRemapWalker : AstNodeWalkerBase<LiftedAction>
    {
        private readonly Dictionary<IVariable, IVariable> m_mapping;

        public VariableRemapWalker(Dictionary<IVariable, IVariable> mapping)
        {
            m_mapping = mapping;
        }

        protected override void VisitVariableExpression(VariableExpression<LiftedAction> variableExpression)
        {
            if (!m_mapping.TryGetValue(variableExpression.Variable, out var newVariable))
            {
                return;
            }
            variableExpression.WithVariable(newVariable);
        }
    }
}