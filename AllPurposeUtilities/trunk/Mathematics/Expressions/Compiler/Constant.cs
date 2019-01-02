using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;
using Utils.Objects;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Constant : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public string Value { get; set; }

		public string TypeName { get; set; }

		public Expression[] CreateExpression(Context context)
		{
			Type type = Type.GetType(TypeName);
			return new Expression[] { Expression.Constant(Convert.ChangeType(Value, type, System.Globalization.CultureInfo.InvariantCulture), type) };
		}
	}
}
