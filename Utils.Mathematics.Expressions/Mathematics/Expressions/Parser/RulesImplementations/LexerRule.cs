using System;
using System.Collections.Generic;
using System.Text;
using Utils.Mathematics.Expressions.Parser.RulesImplementations;

namespace Utils.Mathematics.Expressions.Parser.RulesImplementations
{
	public class LexerRule : SequencedRule<LexerRuleCursor>
	{
		public string Name { get; }
		
	    public LexerRule(string name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		protected internal override void OnReset(int index, Context context)
		{
			if (context.Lexer == null) throw new NullReferenceException(nameof(context.Lexer));
			base.OnReset(index, context);
			var rules = context.Lexer.FindRulesByName(Name);
			foreach (var rule in rules)
			{
				
				if (rule is LexerRule lexerRule)
				{

				}
			}
		}

		protected override LexerRuleCursor Copy(LexerRuleCursor cursor)
		{
			throw new NotImplementedException();
		}

		protected override bool Next(LexerRuleCursor cursor)
		{
			throw new NotImplementedException();
		}

		protected override (bool success, bool canContinue) Test(LexerRuleCursor cursor)
		{
			throw new NotImplementedException();
		}

		protected override bool UseCursor(LexerRuleCursor cursor)
		{
			throw new NotImplementedException();
		}

		protected internal override Rule Clone()
		{
			throw new NotImplementedException();
		}
	}

	public class LexerRuleCursor : Cursor {
	}
}
