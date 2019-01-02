using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

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

		}
	}
}
