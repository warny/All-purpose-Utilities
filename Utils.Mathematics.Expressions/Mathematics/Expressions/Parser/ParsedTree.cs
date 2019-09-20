using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{
	public class ParsedTree : ITree
	{
		public ParserIndex Index { get; }

		public Context Context { get; }
		public Rule Rule { get; }

		private readonly List<ITree> subTrees = new List<ITree>();
		public IReadOnlyList<ITree> SubTrees { get => subTrees; } 
	}
}
