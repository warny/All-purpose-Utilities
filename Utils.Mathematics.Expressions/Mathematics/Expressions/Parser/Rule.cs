using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{
	public abstract class Rule
	{
		public bool CanContinue { get; protected set; }
		public bool Completed { get; protected set; }
		public Result Result { get; protected set; }

		protected internal abstract bool Next(char c, int index);
		protected internal virtual void Reset(int index)
		{
			Completed = false;
			Result = new Result(index);
		}

		public static Rule operator !(Rule rule)
		{
			if (rule is RulesImplementations.NotRule notRule)
				return notRule.Rule;
			else
				return new RulesImplementations.NotRule(rule);
		}

	}

	public static class Rules
	{
		public static Rule Chars(params char[] chars) => new RulesImplementations.IncludeCharRule(chars);
		public static Rule Chars(string chars) => new RulesImplementations.IncludeCharRule(chars);
		public static Rule ExcludeChars(params char[] chars) => new RulesImplementations.ExcludeCharRule(chars);
		public static Rule ExcludeChars(string chars) => new RulesImplementations.ExcludeCharRule(chars);
		public static Rule String(string @string) => new RulesImplementations.StringRule(@string);
	}
}
