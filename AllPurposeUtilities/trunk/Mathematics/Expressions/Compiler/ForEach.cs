using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class ForEach : IExpressionTree, IBreakableContinuableTree
	{
		private IExpressionTree body;

		public IExpressionTree Parent { get; set; }
		public LabelTarget ContinueLabel { get; }
		public LabelTarget BreakLabel { get; }

		public string TypeName { get; set; }
		public string VariableName { get; set; }

		public IExpressionTree EnumerableVariable { get; set; }

		private Type GenericEnumerableInterface = typeof(IEnumerable<>);
		private Type EnumerableInterface = typeof(IEnumerable);

		public IExpressionTree Body
		{
			get => body;
			set {
				body = value;
				body.Parent = this;
			}
		}

		public ForEach()
		{
			ContinueLabel = Expression.Label();
			BreakLabel = Expression.Label();
		}

		public Expression[] CreateExpression(Context context)
		{
			var enumerable = EnumerableVariable.CreateExpression(context).ToExpression();
			var varEnumerableType = enumerable.Type;

			var type = varEnumerableType;
			Type enumerableType, enumeratorType, elementType;

			if ((type.IsGenericType && type.GetGenericTypeDefinition() == GenericEnumerableInterface) || type == EnumerableInterface) {
				SetTypes(out enumerableType, out enumeratorType, out elementType, type);
			}
			else {
				while (true) {
					var interfaces = type.GetInterfaces();
					var @interface = interfaces.FirstOrDefault(i => (i.IsGenericType && i.GetGenericTypeDefinition() == GenericEnumerableInterface) || i == EnumerableInterface);
					if (@interface == null) {
						type = type.BaseType;
						continue;
					}
					SetTypes(out enumerableType, out enumeratorType, out elementType, @interface);
					break;
				}
			}

			bool innerVariable;
			ParameterExpression loopVariable;
			Type variableType;
			if (string.IsNullOrWhiteSpace(TypeName)) {
				innerVariable = false;
				loopVariable = context.Variables[VariableName];
			}
			else {
				innerVariable = true;
				if (TypeName == "var") {
					variableType = elementType;
				}
				else {
					variableType = Type.GetType(TypeName);
				}
				loopVariable = Expression.Variable(variableType, VariableName);
				context.Variables.Add(loopVariable);
			}

			var enumeratorVar = Expression.Variable(enumeratorType);
			var getEnumeratorCall = Expression.Call(enumerable, enumerableType.GetMethod("GetEnumerator"));
			var enumeratorAssign = Expression.Assign(enumeratorVar, getEnumeratorCall);
			var enumeratorDispose = Expression.Call(enumeratorVar, typeof(IDisposable).GetMethod("Dispose"));

			// The MoveNext method's actually on IEnumerator, not IEnumerator<T>
			var moveNextCall = Expression.Call(enumeratorVar, typeof(IEnumerator).GetMethod("MoveNext"));

			var trueConstant = Expression.Constant(true);

			var loop =
				Expression.Loop(
					Expression.IfThenElse(
						Expression.Equal(moveNextCall, trueConstant),
						Expression.Block(
							new[] { loopVariable },
							Expression.Assign(loopVariable, Expression.Property(enumeratorVar, "Current")),
							Body.CreateExpression(context).ToExpression()),
						Expression.Break(BreakLabel)),
					BreakLabel, 
					ContinueLabel);

			var tryFinally =
				Expression.TryFinally(
					loop,
					enumeratorDispose);

			var body =
				Expression.Block(
					innerVariable 
						? new ParameterExpression[] { enumeratorVar, loopVariable } 
						: new ParameterExpression[] { enumeratorVar },
					enumeratorAssign,
					tryFinally);

			return new Expression[] { body };
		}

		private void SetTypes(out Type enumerableType, out Type enumeratorType, out Type elementType, Type @interface)
		{
			enumerableType = @interface;
			if (@interface == EnumerableInterface) {
				elementType = typeof(object);
				enumeratorType = typeof(IEnumerator);
			}
			else {
				elementType = enumerableType.GetGenericArguments()[0];
				enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);
			}
		}
	}
}
