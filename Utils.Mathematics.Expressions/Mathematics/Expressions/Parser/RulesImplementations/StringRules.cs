using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	public class StringRule : Rule
	{
		private readonly string @string;
		private int stringIndex = 0;

		public StringRule(string @string)
		{
			if (string.IsNullOrEmpty(@string)) throw new ArgumentNullException(nameof(@string));
			this.@string = @string;
		}

		protected internal override bool Next(char c, int index)
		{
			if (c == @string[stringIndex])
			{
				if (stringIndex == 0)
				{
					Result = new Result(index);
				}
				Result.Index.NextChar(c);
				stringIndex++;
				if (stringIndex == @string.Length)
				{
					Completed = true;
					Result.Success = true;
				}
				return true;
			}
			else
			{
				Completed = true;
				return false;
			}
		}
	}
}
