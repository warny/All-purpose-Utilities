using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Context
	{
		private Stack<Variables> variablesStack = new Stack<Variables>();
		private Stack<Labels> labelsStack = new Stack<Labels>();

		public Variables Variables { get; private set; }

		public Labels Labels { get; private set; }

		public void Push()
		{
			variablesStack.Push(Variables);
			labelsStack.Push(Labels);

			Variables = new Variables(Variables);
			Labels = new Labels(Labels);
		}

		public void Pop()
		{

		}
	}

	public class Variables : IndexedList<string, ParameterExpression>
	{
		public Variables() : base(p => p.Name) { }
		public Variables(Variables variables) : base(variables, p => p.Name) { }
	}

	public class Labels : IndexedList<string, LabelTarget>
	{
		public Labels() : base(l => l.Name) { }
		public Labels(Labels labels) : base (labels, l => l.Name) { }
	}
}
