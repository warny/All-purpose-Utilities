using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{
	public interface ITree
	{
		int StartIndex { get; }
		int EndIndex { get; }
		int Length { get; }
		string Value { get; }
		Context Context { get; }
		Rule Rule { get; }
		IReadOnlyList<ITree> SubTrees { get; }
	}
}
