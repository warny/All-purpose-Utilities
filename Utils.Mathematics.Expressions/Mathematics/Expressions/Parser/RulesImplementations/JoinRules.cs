using System;
using System.Collections.Generic;
using System.Linq;
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
		private readonly List<Rule> rules;
		private IEnumerator<Rule> currentRule;

		public SequencedRule(params Rule[] rules) : this((IEnumerable<Rule>)rules) { }
		public SequencedRule(IEnumerable<Rule> rules)
		{
			this.rules = new List<Rule>();
			foreach (var rule in rules)
			{
				if (rule is SequencedRule sr) this.rules.AddRange(sr.rules);
				else this.rules.Add(rule);
			}
		}

		protected internal override void Reset(int index)
		{
			base.Reset(index);
			if (currentRule != null)
			{
				currentRule.Reset();
			}
			else
			{
				currentRule = rules.GetEnumerator();
			}
			currentRule.MoveNext();
			currentRule.Current.Reset(index);
		}

		protected internal override bool Next(char c, int index)
		{
			currentRule.Current.Next(c, index);
			if (currentRule.Current.Completed)
			{
				if (currentRule.Current.Result.Success)
				{
					this.Result = new Result(this.Result.Index, currentRule.Current.Result.Index);

					if (currentRule.MoveNext())
					{
						currentRule.Current.Reset(index);
					}
					else
					{
						this.Result.Success = true;
						this.Completed = true;
					}
					return true;
				}
				else
				{
					this.Result.Success = false;
					this.Completed = true;
					return false;
				}
			}
			return true;
		}
	}

	public class ParallelRule : Rule
	{
		private readonly List<Rule> rules;
		private readonly List<Rule> activeRules;
		public ParallelRule(params Rule[] rules) : this((IEnumerable<Rule>)rules) { }
		public ParallelRule(IEnumerable<Rule> rules)
		{
			this.rules = new List<Rule>();
			this.activeRules = new List<Rule>();
			foreach (var rule in rules)
			{
				if (rule is ParallelRule pr) this.rules.AddRange(pr.rules);
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
			foreach (var r in this.activeRules)
			{
				if (r.Completed && r.Result.Success)
				{
					this.Result = r.Result;
					this.Completed = true;
					this.CanContinue = r.CanContinue;
					return true;
				}
			}
			return false;
		}
	}

	public class RepetitionRule : Rule
	{
		internal RepetitionRule(Rule rule, int repetition) : this(rule, repetition, repetition) { }

		internal RepetitionRule(Rule rule, int minimum = 0, int maximum = int.MaxValue)
		{
			this.Rule = rule ?? throw new ArgumentNullException(nameof(rule));
			this.Minimum = minimum;
			this.Maximum = maximum;
		}

		private Rule Rule { get; }
		private int Minimum { get; }
		private int Maximum { get; }

		protected internal override void Reset(int index)
		{
			base.Reset(index);
			Rule.Reset();
		}
		protected internal override bool Next(char c, int index)
		{
			throw new NotImplementedException();
		}
	}

}
