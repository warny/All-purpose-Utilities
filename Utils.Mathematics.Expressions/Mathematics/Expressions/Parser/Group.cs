using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{
	public class Group : IReadOnlyList<ParserIndex>
	{
		private readonly Stack<ParserIndex> indexes = new Stack<ParserIndex>();

		public ParserIndex this[int index] => indexes.Skip(index).First();

		public int Count => indexes.Count;

		public IEnumerator<ParserIndex> GetEnumerator() => indexes.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => indexes.GetEnumerator();

		public string Value => indexes.Peek().Value;

		internal void Push(ParserIndex value) => indexes.Push(value);
		internal ParserIndex Pop()
		{
			if (indexes.TryPop(out ParserIndex result)) return result;
			return null;
		}

		internal ParserIndex Peek()
		{
			if (indexes.TryPeek(out ParserIndex result)) return result;
			return null;
		}
	}
}
