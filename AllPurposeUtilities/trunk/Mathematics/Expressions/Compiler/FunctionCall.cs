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
	public class FunctionCall : ExpressionTreeWithLeft
	{
		public string Name { get; set; }
		public ExpressionTreeList Arguments { get; }
		public List<string> GenericTypesNames { get; }

		public FunctionCall()
		{
			Arguments = new ExpressionTreeList(this);
			GenericTypesNames = new List<string>();
		}

		public override Expression[] CreateExpression(Context context)
		{
			Type funcType = typeof(Func<>);
			Type actionType = typeof(Action<>);

			var leftExpression = Left?.CreateExpression(context).ToExpression();

			var argumentsExpressions = Arguments.ToExpressions(context);

			var argumentTypes = argumentsExpressions.Select(a => a.Type).ToArray();

			//Cas où il n'y a ni dépendance de nom ni dépendance d'objets
			if (Left == null) {
				var function = context.Variables[Name];
				if (function == null || !function.Type.IsAssignableFrom(funcType) || !function.Type.IsAssignableFrom(actionType)) {
					throw new NotImplementedException();
				}
			}

			//Cas où il y a une dépendance de nom mais pas d'objet
			if (leftExpression == null) {
				if (Left is Identifier leftIdentifier) {
					if (leftIdentifier.IdentifierType == IdentifierTypeEnum.Class) {
						var method = leftIdentifier.Type.GetMethod(
							Name, BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy, null,
							argumentTypes, null);
						if (method != null) {
							return new[] { Expression.Call(null, method, argumentsExpressions.ToArray()) };
						}

					}

					throw new CompilerException("Impossible de résoudre le nom", leftIdentifier.IdentifierFullName + "." + Name);
				}
				else {
					throw new CompilerException("Impossible de résoudre le nom", Name);
				}
			}

			// Cas où il y a dépendance d'objets
			var leftType = leftExpression.Type;

			var methodInfo = leftType.GetMethod(Name, argumentTypes);
			if (methodInfo != null) {
				return new[] { Expression.Call(leftExpression, methodInfo, argumentsExpressions) };
			}
			else {
				var propertyOrField = Expression.PropertyOrField(leftExpression, Name);
				Type propertyOrFieldType = propertyOrField.Type;
				var delegateType = typeof(Delegate);
				if (propertyOrFieldType.IsAssignableFrom(delegateType)) {
					methodInfo = delegateType.GetMethod("DynamicInvoke");
				}
				else {
					throw new CompilerException("Aucune methode ou propriété de type expression trouvés", Name);
				}

				return new[] { Expression.Call(propertyOrField, methodInfo, argumentsExpressions) };
			}


		}
	}
}
