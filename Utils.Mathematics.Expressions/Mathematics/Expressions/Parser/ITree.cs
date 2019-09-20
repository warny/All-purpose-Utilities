using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{
	public interface ITree
	{
		ParserIndex Index { get; }
		Context Context { get; }
		Rule Rule { get; }
		IReadOnlyList<ITree> SubTrees { get; }
	}
}
