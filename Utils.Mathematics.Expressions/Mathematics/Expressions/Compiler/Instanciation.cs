using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Instanciation : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public string TypeName { get; set; }
		public ExpressionTreeList Arguments { get; }
		public List<string> GenericTypesNames { get; }

		public Instanciation()
		{
			Arguments = new ExpressionTreeList(this);
			GenericTypesNames = new List<string>();
		}

		public Expression[] CreateExpression(Context context)
		{
			Type type = Type.GetType(TypeName);

			var arguments = Arguments.ToExpressions(context);
			var argumentsTypes = arguments.Select(a => a.Type).ToArray();

			if (type.IsGenericType)
			{
				Type[] genericTypes;
				if (GenericTypesNames != null)
				{
					genericTypes = GenericTypesNames.Select(tn => Type.GetType(tn)).ToArray();
				}
			}

			var constructor = type.GetConstructor(argumentsTypes);

			return new[] { Expression.New(constructor, arguments) };
		}
	}
}