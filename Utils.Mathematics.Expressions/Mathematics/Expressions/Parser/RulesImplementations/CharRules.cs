using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	public class IncludeCharRule : Rule
	{
		private readonly char[] chars;

		public IncludeCharRule(params char[] chars) => this.chars = chars;
		public IncludeCharRule(string chars) => this.chars = chars.ToCharArray();

		protected internal override bool Next(char c, int index)
		{
			if (c.In(chars))
			{
				Completed = true;
				Result = new Result(index, index, c.ToString());
				return true;
			}
			else
			{
				Completed = false;
				Result = new Result();
				return false;
			}
		}

		public override string ToString() => "[" + new string(chars) + "]";
	}

	public class ExcludeCharRule : Rule
	{
		private readonly char[] chars;

		public ExcludeCharRule(params char[] chars) => this.chars = chars;
		public ExcludeCharRule(string chars) => this.chars = chars.ToCharArray();

		protected internal override bool Next(char c, int index)
		{
			if (c.NotIn(chars))
			{
				Completed = true;
				Result = new Result(index, index, c.ToString());
				return true;
			}
			else
			{
				Completed = false;
				Result = new Result();
				return false;
			}
		}

		public override string ToString() => "[^" + new string(chars) + "]";
	}
}
