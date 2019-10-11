using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	/// <summary>
	/// Négation d'une rêgle
	/// </summary>
	public class NotRule : Rule
	{
		public NotRule(Rule rule)
		{
			this.Rule = rule ?? throw new ArgumentNullException(nameof(rule));
		}

		public Rule Rule { get; }

		protected internal override bool Next(char c, int index)
		{
			CanContinue = true;
			Result.Index.NextChar(c);
			if (!Result.Success)
			{
				return false;
			}

			if (Rule.Result.Success)
			{
				CanContinue = false;
				Result.Success = false;
				return false;
			}
			Result.Success = true;
			return true;
		}

		protected internal override void OnReset(int index, Context context) { }
		protected internal override Rule Clone() => new NotRule(Rule.Clone());
		protected override Rule Not() => this.Rule;
	}
}
