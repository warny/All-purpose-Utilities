using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	public class GroupRules : Rule
	{
		private string Name { get; }
		private Rule Rule { get; }

		public GroupRules(string name, Rule rule)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.Rule = rule ?? throw new ArgumentNullException(nameof(rule));
		}

		protected internal override bool Next(char c, int index)
		{
			if (!Rule.Next(c, index)) return false;

			if (Rule.Result.Success)
			{
				this.Result = new Result(Rule.Result);
				this.Result.Success = true;
				this.CanContinue = Rule.CanContinue;
				this.Result.PushGroup(Name, this.Result.Index);
			}
			
			return true;
		}

		protected internal override Rule Clone() => new GroupRules(Name, Rule.Clone());

	}
}
