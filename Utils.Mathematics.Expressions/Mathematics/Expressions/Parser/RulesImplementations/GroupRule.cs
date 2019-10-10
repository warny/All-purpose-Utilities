using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	public class GroupRule : Rule
	{
		private readonly Rule rule;
		public string Name { get; }

		public GroupRule(string name, Rule rule)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.rule = rule ?? throw new ArgumentNullException(nameof(rule));
		}

		protected internal override void Reset(int index, Context context)
		{
			base.Reset(index, context);
			rule.Reset(index, context);
		}

		protected internal override bool Next(char c, int index)
		{
			var res = rule.Next(c, index);
			this.Result = rule.Result;
			if (res && rule.Result.Success)
			{
				this.Context.PushGroup(Name, this.Result.Index);
				this.CanContinue = rule.CanContinue;
			}
			return res;
		}

		protected internal override Rule Clone()
		{
			return new GroupRule(Name, rule.Clone());
		}
	}

	public class GroupReference : StringRuleBase
	{
		public string Name { get; }

		public GroupReference(string name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		protected internal override void Reset(int index, Context context)
		{
			base.Reset(index, context);
			@string = context.Groups[Name].Value; 
		}

		protected internal override Rule Clone() => new GroupReference(Name);

	}
}
