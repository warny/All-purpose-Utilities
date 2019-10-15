using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	/// <summary>
	/// Curseur d'exécution des séquences
	/// </summary>
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

	/// <summary>
	/// Système de rêgles devant être exécutées en séquence
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class SequencedRule<T> : Rule
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
					System.Diagnostics.Debug.WriteLine($"{this.GetType().Name}.{cursor.Rule.GetType().Name} success {cursor.Result.Index.Value} ({cursor.Result.Index.Start}=>{cursor.Result.Index.End})");
					System.Diagnostics.Debug.WriteLine($"		Context :");
					foreach (var group in (IEnumerable<Group>)cursor.Rule.Context.Groups)
					{
						System.Diagnostics.Debug.WriteLine($"			{group.Name} : {group.Value}");
					}
					var test = Test(cursor);
					cursor.Result.Success = test.success;
					canContinue |= test.canContinue;
					if (cursor.Rule.CanContinue)
					{
						var newCursor = Copy(cursor);
						newCursor.Result += cursor.Rule.Result;
						if (newCursor.Rule != null)
						{
							newCursor.Rule.Reset(index + 1, cursor.Rule.Context.Clone());
							toAdd.Add(newCursor);
							System.Diagnostics.Debug.WriteLine($"	{this.GetType().Name}.{cursor.Rule.GetType().Name} clonerule {newCursor.Rule.GetType().Name}, {cursor.Rule.Result.Index.Value} ({cursor.Rule.Result.Index.Start}=>{cursor.Rule.Result.Index.End})");
							foreach (var group in (IEnumerable<Group>)newCursor.Rule.Context.Groups)
							{
								System.Diagnostics.Debug.WriteLine($"			{group.Name} : {group.Value}");
							}
						}
					}
					else
					{
						var oldRule = cursor.Rule;
						cursor.Result += cursor.Rule.Result;
						if (Next(cursor))
						{
							cursor.Rule.Reset(index + 1, cursor.Rule.Context ?? new Context(oldRule.Context, cursor.Result));
							System.Diagnostics.Debug.WriteLine($"	{this.GetType().Name}.{cursor.Rule.GetType().Name} resetrule {cursor.Rule.Result.Index.Value} ({cursor.Rule.Result.Index.Start}=>{cursor.Rule.Result.Index.End})");
						}
						else
						{
							toRemove.Add(cursor);
							System.Diagnostics.Debug.WriteLine($"	{this.GetType().Name}.{cursor.Rule.GetType().Name} removerule {cursor.Rule.Result.Index.Value} ({cursor.Rule.Result.Index.Start}=>{cursor.Rule.Result.Index.End})");
						}
					}
				}
				else
				{
					cursor.Result.Success = false;
					if (!valid) toRemove.Add(cursor);
				}
			}

			try
			{
				if (!Cursors.Any())
				{
					Result = new Result();
					Context = null;
					return false;
				}

				CanContinue = canContinue || Cursors.Any(cur => cur.Rule.CanContinue);
				var cursor = Cursors.FirstOrDefault(cur => cur.Result.Success) ?? Cursors.First();
				Result = cursor.Result;
				Context = cursor.Rule.Context;
				return true;
			}
			finally
			{
				Cursors.RemoveRange(toRemove);
				Cursors.AddRange(toAdd);
			}
		}

		protected internal override void OnReset(int index, Context context) {
			Result = new Result(index);
		}

	}

	/// <summary>
	/// Ensemble de rêgles devant être exécutées en séquence ordonnée
	/// </summary>
	public class SequenceRule : SequencedRule<SequenceRule.SequenceCursor>
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

		public SequenceRule(params Rule[] rules) : this((IEnumerable<Rule>)rules) { }
		public SequenceRule(IEnumerable<Rule> rules)
		{
			this.Rules = new List<Rule>();
			foreach (var rule in rules)
			{
				if (rule is SequenceRule sr) this.Rules.AddRange(sr.Rules);
				else this.Rules.Add(rule);
			}
		}

		protected internal override void OnReset(int index, Context context)
		{
			base.OnReset(index, context);
			var rules = Rules.ToQueue();
			Rule rule = rules.Dequeue();
			rule.Reset(index, context);
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
			return new SequenceRule(copiedRules);
		}

		protected override Rule Then(Rule rule) => new SequenceRule(this.Rules.Union(new[] { rule }));

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

	/// <summary>
	/// Répéteur de rêgles
	/// </summary>
	public class RepetitionRule : SequencedRule<RepetitionRule.RepetitionCursor>
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

		protected internal override void OnReset(int index, Context context)
		{
			base.OnReset(index, context);
			Rule.Reset(index, context);
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
