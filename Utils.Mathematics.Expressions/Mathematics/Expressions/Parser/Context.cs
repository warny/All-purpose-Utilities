using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{

	public class Context
	{
		public Groups Groups { get; }
		public Result Result { get; }

		public Context()
		{
			Groups = new Groups();
			Result = new Result();
		}

		public Context(Context context) : this()
		{
			Groups = context.Groups.Clone();
			Result = new Result(context.Result);
		}

		public Context(Context context, Result result)
		{
			Groups = result.Groups;
			Result = result;
		}

		public Context Clone() => new Context(this);
	}
}
