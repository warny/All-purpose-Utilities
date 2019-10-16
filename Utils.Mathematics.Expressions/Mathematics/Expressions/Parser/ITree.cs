using System.Collections.Generic;

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