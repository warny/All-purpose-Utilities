using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions.Compiler
{
	public interface IExpressionTree
	{
		IExpressionTree Parent { get; set; }

		Expression[] CreateExpression(Context context);
	}

	public interface IExpressionTreeWithLeft : IExpressionTree
	{
		IExpressionTree Left { get; set; }
	}

	public interface IExpressionTreeWithRight : IExpressionTree
	{
		IExpressionTree Right { get; set; }
	}

	public abstract class ExpressionTreeWithUnary : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public abstract Expression[] CreateExpression(Context context);

		private IExpressionTree expression;

		public IExpressionTree Expression
		{
			get => expression;
			set {
				expression = value;
				expression.Parent = this;
			}
		}
	}

	public abstract class ExpressionTreeWithLeft : IExpressionTreeWithLeft
	{
		public IExpressionTree Parent { get; set; }

		public abstract Expression[] CreateExpression(Context context);

		private IExpressionTree left;

		public IExpressionTree Left
		{
			get => left;
			set {
				left = value;
				left.Parent = this;
			}
		}
	}

	public abstract class ExpressionTreeWithRight : IExpressionTreeWithRight
	{
		public IExpressionTree Parent { get; set; }

		public abstract Expression[] CreateExpression(Context context);

		private IExpressionTree right;

		public IExpressionTree Right
		{
			get => right;
			set {
				right = value;
				right.Parent = this;
			}
		}
	}

	public abstract class ExpressionTreeWithLeftAndRight : IExpressionTreeWithRight, IExpressionTreeWithLeft
	{
		public IExpressionTree Parent { get; set; }

		public abstract Expression[] CreateExpression(Context context);

		private IExpressionTree left;

		public IExpressionTree Left
		{
			get => left;
			set {
				left = value;
				left.Parent = this;
			}
		}

		private IExpressionTree right;

		public IExpressionTree Right
		{
			get => right;
			set {
				right = value;
				right.Parent = this;
			}
		}
	}

	public interface IBreakableContinuableTree : IExpressionTree
	{
		LabelTarget ContinueLabel { get; }
		LabelTarget BreakLabel { get; }
	}

	public class CompilerException : Exception
	{
		public CompilerException(string message, string objectName) :
			base(message + " : " + objectName)
		{
		}
	}

	public class ExpressionTreeList : IList<IExpressionTree>
	{
		private List<IExpressionTree> expressionsTrees = new List<IExpressionTree>();
		private IExpressionTree parent;

		internal IExpressionTree Parent
		{
			get => parent;
			set {
				parent = value;
				expressionsTrees.ForEach(et => et.Parent = value);
			}
		}

		public ExpressionTreeList(IExpressionTree parent)
		{
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
		}

		public IExpressionTree this[int index]
		{
			get => expressionsTrees[index];
			set {
				expressionsTrees[index].Parent = null;
				expressionsTrees[index] = value;
				value.Parent = this.parent;
			}
		}

		public int Count { get; }
		public bool IsReadOnly { get; }

		public void Add(IExpressionTree item)
		{
			expressionsTrees.Add(item);
			item.Parent = this.parent;
		}

		public void Clear()
		{
			expressionsTrees.ForEach(et => et.Parent = null);
			expressionsTrees.Clear();
		}

		public bool Contains(IExpressionTree item)
			=> expressionsTrees.Contains(item);

		public void CopyTo(IExpressionTree[] array, int arrayIndex)
			=> expressionsTrees.CopyTo(array, arrayIndex);

		public IEnumerator<IExpressionTree> GetEnumerator()
			=> expressionsTrees.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> expressionsTrees.GetEnumerator();

		public int IndexOf(IExpressionTree item)
			=> expressionsTrees.IndexOf(item);

		public void Insert(int index, IExpressionTree item)
		{
			expressionsTrees.Insert(index, item);
			item.Parent = this.parent;
		}

		public bool Remove(IExpressionTree item)
		{
			item.Parent = null;
			return expressionsTrees.Remove(item);
		}

		public void RemoveAt(int index)
		{
			expressionsTrees[index].Parent = null;
			expressionsTrees.RemoveAt(index);
		}

		public Expression[] ToExpressions(Context context)
		{
			List<Expression> expressions = new List<Expression>();

			foreach (IExpressionTree expressionTree in this)
			{
				Expression expression = expressionTree.CreateExpression(context).ToExpression();
				expressions.Add(expression);
			}
			var argumentTypes = expressions.Select(a => a.Type).ToArray();
			return expressions.ToArray();
		}
	}
}