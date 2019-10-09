using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Parser
{
	public class Group : IReadOnlyList<ParserIndex>
	{
		public string Name { get; }

		private readonly Stack<ParserIndex> indexes = new Stack<ParserIndex>();

		public Group(string name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}

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

		public Group Clone()
		{
			var result = new Group(Name);
			foreach (var index in indexes) {
				result.indexes.Push(new ParserIndex(index));
			}
			return result;
		}
	}

	public class Groups : IndexedList<string, Group>
	{
		public Groups() : base(g => g.Name) { }

		public Groups(IEnumerable<Group> groups) : this()
		{
			foreach (var group in groups)
			{
				this.Add(group.Clone());
			}
		}

		public Groups Clone() => new Groups(this);
	}
}
