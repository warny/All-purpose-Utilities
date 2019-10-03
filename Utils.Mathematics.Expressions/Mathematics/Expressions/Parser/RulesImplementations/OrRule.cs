using System.Collections.Generic;
using System.Linq;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	public class OrRule : Rule
	{
		private readonly List<Rule> rules;
		private readonly List<Rule> activeRules;
		public OrRule(params Rule[] rules) : this((IEnumerable<Rule>)rules) { }
		public OrRule(IEnumerable<Rule> rules)
		{
			this.rules = new List<Rule>();
			this.activeRules = new List<Rule>();
			foreach (var rule in rules)
			{
				if (rule is OrRule pr) this.rules.AddRange(pr.rules);
				else this.rules.Add(rule);
			}
		}

		protected internal override void Reset(int index)
		{
			base.Reset(index);
			activeRules.Clear();
			foreach (var r in this.rules)
			{
				r.Reset(index);
				activeRules.Add(r);
			}
		}

		protected internal override bool Next(char c, int index)
		{
			var rulesToRemove = new List<Rule>();
			this.CanContinue = false;
			foreach (var rule in this.activeRules)
			{
				if (!rule.Next(c, index))
				{
					rulesToRemove.Add(rule);
					continue;
				}
				if (rule.Result.Success)
				{
					this.CanContinue = rule.CanContinue;
					continue;
				}
			}
			this.activeRules.RemoveRange(rulesToRemove);
			this.CanContinue = CanContinue || !activeRules.All(r=> !r.Result.Success);
			this.Result = this.activeRules.FirstOrDefault(r=>r.Result.Success)?.Result;
			this.Result = this.Result ?? this.activeRules.FirstOrDefault()?.Result ?? new Result();
			return this.activeRules.Any();
		}

		protected internal override Rule Clone()
		{
			var copiedRules = this.rules.Copy(r => r.Clone());
			return new OrRule(copiedRules);
		}

		public override string ToString() => "(" + string.Join("|", rules.Select(r => r.ToString())) + ")";
	}

}
