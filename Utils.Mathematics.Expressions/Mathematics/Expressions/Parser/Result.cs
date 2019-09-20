using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{

	public sealed class Result
	{
		internal Result()
		{
			this.Success = false;
		}

		internal Result(int startIndex)
		{
			this.Success = false;
			this.Index = new ParserIndex(startIndex, startIndex, 0, "");
		}

		internal Result(int startIndex, int endIndex, string value, bool success = true)
		{
			this.Success = success;
			this.Index = new ParserIndex(startIndex, endIndex, endIndex - startIndex, value);
		}

		internal Result(ParserIndex result1, ParserIndex result2)
		{
			try
			{
				this.Index = result1 + result2;
				this.Success = true;
			}
			catch
			{
				this.Index = null;
				this.Success = false;
			}
		}

		public bool Success { get; internal set; }
		public ParserIndex Index { get; }
	}
}
