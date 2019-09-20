using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	public class NotRule : Rule
	{
		public NotRule(Rule rule)
		{
			this.Rule = rule ?? throw new ArgumentNullException(nameof(rule));
		}

		public Rule Rule { get; }

		protected internal override bool Next(char c, int index)
		{
			if (!Result.Success) return false;

			Result.Index.NextChar(c);
			if (Rule.Result.Success)
			{
				Result.Success = false;
				Completed = false;
				return false;
			}
			Result.Success = true;
			Completed = true;
			return true;
		}
	}

	public class SequencedRule : Rule
	{
		public SequencedRule(params Rule[] rules)
		{

		}

		protected internal override bool Next(char c, int index)
		{
			throw new NotImplementedException();
		}
	}

}
