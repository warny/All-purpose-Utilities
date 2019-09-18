using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{
	public class Rule
	{

	}

	public sealed class Result
	{
		internal Result()
		{
			this.Success = false;
		}

		internal Result(int startIndex, int endIndex, string value)
		{
			this.Success = true;
			this.Index = new ParserIndex(startIndex, endIndex, endIndex - startIndex, value);
		}

		internal Result(ParserIndex result1, ParserIndex result2)
		{
			if (result1.EndIndex + 1 != result2.StartIndex) throw new InvalidOperationException("deux index fusionnés doivent être consécutifs");
			this.Success = true;
			this.Index = new ParserIndex(result1.StartIndex, result2.EndIndex, result2.EndIndex - result1.StartIndex, result1.Value + result2.Value);
		}

		public bool Success { get; }
		public ParserIndex Index { get; }
	}
}
