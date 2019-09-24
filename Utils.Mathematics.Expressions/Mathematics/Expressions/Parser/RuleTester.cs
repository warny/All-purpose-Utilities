using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{
	public static class RuleTester
	{
		public static Result Test(Rule rule, string testString)
		{
			rule.Reset(0);

			for (int i = 0; i < testString.Length; i++)
			{
				char c = testString[i];
				if (!rule.Next(c, i)) break;
			}

			return rule.Result;
		}
	}
}
