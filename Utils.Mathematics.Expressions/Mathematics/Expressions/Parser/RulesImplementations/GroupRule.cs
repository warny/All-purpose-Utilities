using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	public class GroupRule : Rule
	{
		private readonly Rule rule;
		public string Name { get; }
		private Context contextCache = null;

		public GroupRule(string name, Rule rule)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.rule = rule ?? throw new ArgumentNullException(nameof(rule));
		}

		protected internal override void OnReset(int index, Context context)
		{
			rule.Reset(index, context);
		}

		protected internal override bool Next(char c, int index)
		{
			if (this.contextCache != null)
			{
				this.Context = contextCache;
				contextCache = null;
			}
			var res = rule.Next(c, index);
			this.Result = rule.Result;
			if (res && rule.Result.Success)
			{
				this.contextCache = this.Context.Clone();
				this.Context.PushGroup(Name, this.Result.Index);
				this.CanContinue = rule.CanContinue;
			}
			return res;
		}

		protected internal override Rule Clone()
		{
			return new GroupRule(Name, rule.Clone());
		}

		public override string ToString() => $"(?<{Name}>{rule})";
	}

	public class GroupReference : StringRuleBase
	{
		public string Name { get; }

		public GroupReference(string name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		protected internal override void OnReset(int index, Context context)
		{
			base.OnReset(index, context);
			@string = context.Groups[Name].Value; 
		}

		protected internal override Rule Clone() => new GroupReference(Name);
		public override string ToString() => $"\\k<{Name}>";
	}
}
