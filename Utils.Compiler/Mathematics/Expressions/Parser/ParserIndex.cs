using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{
	public class ParserIndex
	{
		internal ParserIndex(int startIndex, int endIndex, int length, string value)
		{
			this.StartIndex = startIndex;
			this.EndIndex = endIndex;
			this.Length = length;
			this.Value = value ?? throw new ArgumentNullException(nameof(value));
		}

		public int StartIndex { get; }
		public int EndIndex { get; }
		public int Length { get; }
		public string Value { get; }
	}
}
