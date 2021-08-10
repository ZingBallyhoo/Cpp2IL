using Echo.Ast;
using Echo.Core.Code;

namespace ReadExceptionInfo
{
    public static class ExpressionExtensions
    {
        public static IVariable GetVariable<T>(this ExpressionStatement<T> expression)
        {
            return GetVariable(expression.Expression);
        }
        
        public static IVariable GetVariable<T>(this Expression<T> expression)
        {
            return ((VariableExpression<T>)expression).Variable;
        }
    }
}