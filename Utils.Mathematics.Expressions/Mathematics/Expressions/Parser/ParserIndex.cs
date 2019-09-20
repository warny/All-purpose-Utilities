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
			this.value = new StringBuilder(value) ?? throw new ArgumentNullException(nameof(value));
		}

		private ParserIndex(ParserIndex index1, ParserIndex index2)
		{
			if (index1.EndIndex != index2.StartIndex) throw new InvalidOperationException("deux index fusionnés doivent être consécutifs");
			StartIndex = index1.StartIndex;
			EndIndex = index2.EndIndex;
			Length = index1.Length + index2.Length;
			value = new StringBuilder(index1.Value + index2.Value);
		}

		public int StartIndex { get; }
		public int EndIndex { get; private set; }
		public int Length { get; }

		private readonly StringBuilder value;
		public string Value => value.ToString();

		public void NextChar(char c)
		{
			value.Append(c);
			EndIndex++;
		}

		public static ParserIndex operator +(ParserIndex index1, ParserIndex index2) => new ParserIndex(index1, index2);
	}
}
