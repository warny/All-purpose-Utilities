using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Lists;

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

		protected internal override Rule Clone() => new NotRule(Rule.Clone());
		protected override Rule Not() => this.Rule;
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
					this.Result = new Result(this.Result.Index, currentRule.Current.Result.Index, currentRule.Current.Result.Success);

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

		protected internal override Rule Clone()
		{
			var copiedRules = new List<Rule>();
			foreach (var rule in this.rules)
			{
				copiedRules.Add(rule.Clone());
			}
			return new SequencedRule(copiedRules);
		}

		protected override Rule Then(Rule rule) => new SequencedRule(this.rules.Union(new[] { rule }));
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
			bool result = false;
			List<Rule> rulesToRemove = new List<Rule>();
			foreach (var rule in this.activeRules)
			{
				rule.Next(c, index);
				if (rule.Completed && rule.Result.Success)
				{
					this.Result = rule.Result;
					this.Completed = true;
					this.CanContinue = activeRules.Count > 1 || rule.CanContinue;
					result = true;
					continue;
				}
				rulesToRemove.Add(rule);
			}
			this.activeRules.RemoveRange(rulesToRemove);

			return result;
		}

		protected internal override Rule Clone()
		{
			var copiedRules = this.rules.Copy(r => r.Clone());
			return new ParallelRule(copiedRules);
		}

		public override string ToString() => "(" + string.Join("|", rules.Select(r => r.ToString())) + ")";
	}

	public class RepetitionRule : Rule
	{
		private class Cursor
		{
			public Result Result;
			public int Repetition;
			public Rule Rule;

			public Cursor(Result result, int repetition, Rule rule)
			{
				this.Result = result ?? throw new ArgumentNullException(nameof(result));
				this.Repetition = repetition;
				this.Rule = rule ?? throw new ArgumentNullException(nameof(rule));
			}
		}

		private readonly List<Cursor> cursors = new List<Cursor>();
		
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
			Rule.Reset(index);
			cursors.Clear();
			cursors.Add(new Cursor(Result, 0, Rule));
		}

		protected internal override Rule Clone() => new RepetitionRule(Rule, Minimum, Maximum);

		protected internal override bool Next(char c, int index)
		{
			var toAdd = new List<Cursor>();
			var toRemove = new List<Cursor>();
			foreach (var cursor in cursors.Where(cur=>cur.Repetition < Maximum))
			{
				bool valid = cursor.Rule.Next(c, index);
				if (cursor.Rule.Result.Success)
				{
					if (cursor.Rule.CanContinue)
					{
						var newCursor = new Cursor(cursor.Result + cursor.Rule.Result, cursor.Repetition + 1, Rule.Clone());
						toAdd.Add(newCursor);
					}
					else
					{
						cursor.Result += Rule.Result;
						cursor.Repetition++;
						cursor.Rule.Reset(index);
					}
				}
				else
				{
					if (!valid)	toRemove.Add(cursor);
				}
			}
			cursors.RemoveRange(toRemove);
			cursors.AddRange(toAdd);

			if (!cursors.Any())
			{
				return false;
			}
			if (cursors.Count == 1)
			{
				Result = cursors.First().Result;
				return true;
			}

			CanContinue = cursors.Any(cur => cur.Rule.CanContinue);
			Result = cursors.FirstOrDefault(cur => cur.Result.Success).Result;
			Result = Result ?? cursors.First().Result;

			return true;
		}

		public override string ToString()
		{
			if (Minimum == Maximum) return $"{Rule}{{{Minimum}}}";
			if (Maximum == int.MaxValue)
			{
				if (Minimum == 0) return $"{Rule}*";
				if (Minimum == 1) return $"{Rule}+";
				return $"{Rule}{{{Minimum},}}";
			}
			return $"{Rule}{{{Minimum},{Maximum}}}";
		}
	}

}
