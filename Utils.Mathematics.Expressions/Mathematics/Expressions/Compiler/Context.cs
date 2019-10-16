using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Context
	{
		private Stack<Variables> variablesStack = new Stack<Variables>();
		private Stack<Labels> labelsStack = new Stack<Labels>();

		public Variables Variables { get; private set; } = new Variables();

		public Labels Labels { get; private set; } = new Labels();

		public INameResolver NameResolver { get; private set; }

		public Context()
		{
			NameResolver = new DefaultNameResolver();
		}

		public Context(INameResolver nameResolver)
		{
			NameResolver = nameResolver;
		}

		public void Push()
		{
			variablesStack.Push(Variables);
			labelsStack.Push(Labels);

			Variables = new Variables(Variables);
			Labels = new Labels(Labels);
		}

		public void Pop()
		{
			Variables = variablesStack.Pop();
			Labels = labelsStack.Pop();
		}

		public ParameterExpression[] PeekVariables()
		{
			return Variables.Values.Except(variablesStack.Peek().Values).ToArray();
		}

		public LabelTarget[] PeekLabels()
		{
			return Labels.Values.Except(labelsStack.Peek().Values).ToArray();
		}
	}

	public class Variables : IndexedList<string, ParameterExpression>
	{
		public Variables() : base(p => p.Name)
		{
		}

		public Variables(Variables variables) : base(p => p.Name)
		{
		}
	}

	public class Labels : IndexedList<string, LabelTarget>
	{
		public Labels() : base(l => l.Name)
		{
		}

		public Labels(Labels labels) : base(l => l.Name)
		{
		}
	}
}