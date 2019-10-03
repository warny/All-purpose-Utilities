using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	public class StringRule : Rule
	{
		private readonly string @string;
		private int stringIndex = 0;

		protected internal override void Reset(int index)
		{
			base.Reset(index);
			stringIndex = 0;
		}

		public StringRule(string @string)
		{
			if (string.IsNullOrEmpty(@string)) throw new ArgumentNullException(nameof(@string));
			this.@string = @string;
		}

		public StringRule(StringRule sr1, StringRule sr2) : this(sr1.@string + sr2.@string) { }

		protected internal override bool Next(char c, int index)
		{
			if (stringIndex >= @string.Length)
			{
				return false;
			}
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
					CanContinue = false;
					Result.Success = true;
				}
				return true;
			}
			else
			{
				Result.Success = false;
				return false;
			}
		}

		protected override Rule Then(Rule rule)
		{
			if (rule is StringRule stringRule)
			{
				return new StringRule(this.@string + stringRule.@string);
			}
			return base.Then(rule);
		}

		protected internal override Rule Clone() => new StringRule(@string);

		public override string ToString() => @string;
	}
}
