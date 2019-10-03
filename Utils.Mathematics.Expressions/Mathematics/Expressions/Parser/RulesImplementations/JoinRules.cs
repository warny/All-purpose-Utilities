using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	public class Cursor
	{
		public Result Result { get; set; }
		public Rule Rule { get; set; }

		internal Cursor(Result result, Rule rule)
		{
			this.Result = result ?? throw new ArgumentNullException(nameof(result));
			this.Rule = rule ?? throw new ArgumentNullException(nameof(rule));
		}
	}

	public abstract class JoinRule<T> : Rule
		where T: Cursor
	{

		protected List<T> Cursors { get; } = new List<T>();

		protected abstract bool UseCursor(T cursor);
		protected abstract T Copy(T cursor);
		protected abstract bool Next(T cursor);
		protected abstract (bool success, bool canContinue) Test(T cursor);

		protected sealed internal override bool Next(char c, int index)
		{
			var toAdd = new List<T>();
			var toRemove = new List<T>();
			bool canContinue = false;
			foreach (var cursor in Cursors.Where(UseCursor))
			{
				bool valid = cursor.Rule.Next(c, index);
				if (cursor.Rule.Result.Success)
				{
					var test = Test(cursor);
					cursor.Result.Success = test.success;
					canContinue |= test.canContinue;
					if (cursor.Rule.CanContinue)
					{
						var newCursor = Copy(cursor);
						newCursor.Result += cursor.Rule.Result;
						if (newCursor.Rule != null)
						{
							newCursor.Rule.Reset(index);
							toAdd.Add(newCursor);
						}
					}
					else
					{
						cursor.Result += cursor.Rule.Result;
						if (Next(cursor))
						{
							cursor.Rule.Reset(index);
						}
						else
						{
							toRemove.Add(cursor);
						}
					}
				}
				else
				{
					cursor.Result.Success = false;
					if (!valid) toRemove.Add(cursor);
				}
			}

			if (!Cursors.Any())
			{
				Result = new Result();
				Cursors.RemoveRange(toRemove);
				Cursors.AddRange(toAdd);
				return false;
			}
			if (Cursors.Count == 1)
			{
				Result = Cursors.First().Result;
				CanContinue = canContinue;
				Cursors.RemoveRange(toRemove);
				Cursors.AddRange(toAdd);
				return true;
			}

			CanContinue = canContinue || Cursors.Any(cur => cur.Rule.CanContinue);
			Result = Cursors.FirstOrDefault(cur => cur.Result.Success)?.Result;
			Result = Result ?? Cursors.First().Result;
			Cursors.RemoveRange(toRemove);
			Cursors.AddRange(toAdd);

			return true;
		}

	}

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

	public class SequencedRule : JoinRule<SequencedRule.SequenceCursor>
	{
		private List<Rule> Rules { get; }

		public class SequenceCursor : Cursor
		{
			public SequenceCursor(Result result, Rule rule, Queue<Rule> rules) : base(result, rule)
			{
				this.Rules = rules ?? throw new ArgumentNullException(nameof(rules));
			}

			public Queue<Rule> Rules { get; }
			public override string ToString() => $"Sequence : {Rules.Count}";
		}

		public SequencedRule(params Rule[] rules) : this((IEnumerable<Rule>)rules) { }
		public SequencedRule(IEnumerable<Rule> rules)
		{
			this.Rules = new List<Rule>();
			foreach (var rule in rules)
			{
				if (rule is SequencedRule sr) this.Rules.AddRange(sr.Rules);
				else this.Rules.Add(rule);
			}
		}

		protected internal override void Reset(int index)
		{
			base.Reset(index);
			var rules = Rules.ToQueue();
			Rule rule = rules.Dequeue();
			rule.Reset(index);
			Cursors.Clear();
			Cursors.Add(new SequenceCursor(this.Result, rule, rules));
		}

		protected internal override Rule Clone()
		{
			var copiedRules = new List<Rule>();
			foreach (var rule in this.Rules)
			{
				copiedRules.Add(rule.Clone());
			}
			return new SequencedRule(copiedRules);
		}

		protected override Rule Then(Rule rule) => new SequencedRule(this.Rules.Union(new[] { rule }));

		protected override bool UseCursor(SequenceCursor cursor) => true;
		protected override SequenceCursor Copy(SequenceCursor cursor) {
			Queue<Rule> queue = cursor.Rules.Copy(r=>r.Clone());
			var rule = queue.Dequeue();
			return new SequenceCursor(cursor.Result, rule, queue);
		}
		protected override bool Next(SequenceCursor cursor) {
			if (!cursor.Rules.Any()) return false;
			cursor.Rule = cursor.Rules.Dequeue();
			return true;
		}
		protected override (bool success, bool canContinue) Test(SequenceCursor cursor) => (!cursor.Rules.Any(), false);
		public override string ToString() => string.Join("", Rules);
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
			var rulesToRemove = new List<Rule>();
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

	public class RepetitionRule : JoinRule<RepetitionRule.RepetitionCursor>
	{
		public class RepetitionCursor : Cursor {
			public int Repetition { get; set; }

			internal RepetitionCursor(Result result, int repetition, Rule rule) : base(result, rule)
			{
				Repetition = repetition;
			}

			public override string ToString() => $"Repetition : {Repetition}";
		}

		internal RepetitionRule(Rule rule, int repetition) : this(rule, repetition, repetition) { }

		internal RepetitionRule(Rule rule, int minimum = 0, int maximum = int.MaxValue)
		{
			this.Rule = rule ?? throw new ArgumentNullException(nameof(rule));
			this.Minimum = minimum;
			this.Maximum = maximum;
		}

		protected override bool UseCursor(RepetitionCursor cursor) => cursor.Repetition <= Maximum;
		protected override RepetitionCursor Copy(RepetitionCursor cursor) => new RepetitionCursor(cursor.Result + cursor.Rule.Result, cursor.Repetition + 1, cursor.Rule.Clone());
		protected override bool Next(RepetitionCursor cursor)
		{
			cursor.Repetition++;
			return cursor.Repetition <= Maximum;
		}
		protected override (bool success, bool canContinue) Test(RepetitionCursor cursor)
		{
			return (
				cursor.Repetition.Between(Minimum, Maximum),
				cursor.Repetition >= Minimum && cursor.Repetition < Maximum
			);
		}

		private Rule Rule { get; }
		private int Minimum { get; }
		private int Maximum { get; }

		protected internal override void Reset(int index)
		{
			base.Reset(index);
			Rule.Reset(index);
			Cursors.Clear();
			Cursors.Add(new RepetitionCursor(Result, 1, Rule));
		}

		protected internal override Rule Clone() => new RepetitionRule(Rule, Minimum, Maximum);

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
