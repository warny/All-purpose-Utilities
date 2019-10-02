using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{
	public class ParserIndex
	{
		internal ParserIndex(int startIndex, int endIndex, int length, string value)
		{
			this.Start = startIndex;
			this.End = endIndex;
			this.Length = length;
			this.value = new StringBuilder(value) ?? throw new ArgumentNullException(nameof(value));
		}

		internal ParserIndex(ParserIndex index)
		{
			Start = index.Start;
			End = index.End;
			Length = index.Length;
			value = new StringBuilder(index.Value);
		}

		private ParserIndex(ParserIndex index1, ParserIndex index2)
		{
			if (index1.End != index2.Start) throw new InvalidOperationException("deux index fusionnés doivent être consécutifs");
			Start = index1.Start;
			End = index2.End;
			Length = index1.Length + index2.Length;
			value = new StringBuilder(index1.Value + index2.Value);
		}

		public int Start { get; }
		public int End { get; private set; }
		public int Length { get; }

		private readonly StringBuilder value;
		public string Value => value.ToString();

		public void NextChar(char c)
		{
			value.Append(c);
			End++;
		}

		public static ParserIndex operator +(ParserIndex index1, ParserIndex index2) => new ParserIndex(index1, index2);
	}
}
