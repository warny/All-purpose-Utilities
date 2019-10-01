using System;
using System.Collections.Generic;
using System.Text;
using Utils.Mathematics.Expressions.Parser.RulesImplementations;

namespace Utils.Mathematics.Expressions.Parser
{
	public abstract class Rule
	{
		public bool CanContinue { get; protected set; }
		public bool Completed { get; protected set; }
		public Result Result { get; protected set; }
		public int Start { get; private set;}

		protected internal abstract bool Next(char c, int index);
		protected internal virtual void Reset(int index)
		{
			Start = index;
			Completed = false;
			Result = new Result(index);
		}

		protected internal abstract Rule Clone();

		protected virtual Rule Then(Rule rule)	=> new SequencedRule(this, rule);
		protected virtual Rule Not() => new NotRule(this);

		public static Rule operator !(Rule rule) => rule.Not();
		public static Rule operator +(Rule rule1, Rule rule2) => rule1.Then(rule2);
		public static Rule operator |(Rule rule1, Rule rule2) => new ParallelRule(rule1, rule2);
		public static Rule operator *(Rule rule, int repetition) => Rules.Repeat(rule, repetition);
		public static Rule operator *(Rule rule, (int? minimum, int? maximum) repetitions) => Rules.Repeat(rule, repetitions.minimum ?? 0, repetitions.maximum ?? int.MaxValue);
	}

	public static class Rules
	{
		public static Rule Chars(params char[] chars) => new RulesImplementations.IncludeCharRule(chars);
		public static Rule Chars(string chars) => new RulesImplementations.IncludeCharRule(chars);
		public static Rule ExcludeChars(params char[] chars) => new RulesImplementations.ExcludeCharRule(chars);
		public static Rule ExcludeChars(string chars) => new RulesImplementations.ExcludeCharRule(chars);
		public static Rule String(string @string) => new RulesImplementations.StringRule(@string);
		public static Rule Sequence(IEnumerable<Rule> rules) => new RulesImplementations.SequencedRule(rules);
		public static Rule Sequence(params Rule[] rules) => new RulesImplementations.SequencedRule(rules);
		public static Rule Or(IEnumerable<Rule> rules) => new RulesImplementations.ParallelRule(rules);
		public static Rule Or(params Rule[] rules) => new RulesImplementations.ParallelRule(rules);
		public static Rule Repeat(this Rule rule, int repetition) => new RepetitionRule(rule, repetition);
		public static Rule Repeat(this Rule rule, int minimum = 0, int maximum = int.MaxValue) => new RepetitionRule(rule, minimum, maximum);
		public static Rule Not(Rule rule) => !rule;
	}
}
