using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class FunctionCall : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public IExpressionTree Left { get; set; }
		public string Name { get; set; }
		public List<IExpressionTree> Arguments { get; set; }

		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			var leftExpression = Left.CreateExpression(variables, labels, out var leftDeclaredVariables).ToExpression();
			var innerDeclaredVariables = new List<ParameterExpression>(leftDeclaredVariables);
			var innerVariables = variables.Union(innerDeclaredVariables).ToArray();

			var leftType = leftExpression.Type;

			List<Expression> argumentsExpressions = new List<Expression>();

			foreach (IExpressionTree argument in Arguments)
			{
				Expression argumentExpr = argument.CreateExpression(innerVariables, labels, out var subDeclaredVariables).ToExpression();
				if (!subDeclaredVariables.IsNullOrEmpty())
				{
					innerDeclaredVariables.AddRange(subDeclaredVariables);
					innerVariables = variables.Union(innerDeclaredVariables).ToArray();
				}
			}

			declaredVariables = innerDeclaredVariables.ToArray();

			var argumentTypes = argumentsExpressions.Select(a => a.Type).ToArray();

			var methodInfo = leftType.GetMethod(Name, argumentTypes);
			if (methodInfo == null)
			{
				return new[] { Expression.Call(leftExpression, methodInfo, argumentsExpressions) };
			}
			else
			{
				
				var propertyOrField = Expression.PropertyOrField(leftExpression, Name);
				Type propertyOrFieldType = propertyOrField.Type;
				var delegateType = typeof(Delegate);
				if (propertyOrFieldType.IsAssignableFrom(delegateType))
				{
					methodInfo = delegateType.GetMethod("DynamicInvoke");
				}
				else
				{
					throw new CompilerException("Aucune methode ou propriété de type expression trouvés", Name);
				}

				return new[] { Expression.Call(propertyOrField, methodInfo, argumentsExpressions) };
			}


		}
	}
}
