using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{
	public class Context : ICloneable
	{
		private readonly Dictionary<string, Stack<Result>> groups;
		public Result Result { get; }

		public Context()
		{
			groups = new Dictionary<string, Stack<Result>>();
			Result = new Result();
		}

		public Context(Context context) : this()
		{
			foreach (var group in context.Groups)
			{
				var targetResults = new Stack<Result>();
				foreach (var sourceResults in group.Value)
				{
					targetResults.Push(new Result(sourceResults));
				}
				this.groups.Add(group.Key, targetResults);
			}
			Result = new Result(context.Result);
		}

		public IDictionary<string, Stack<Result>> Groups => groups;
		public object Clone() => new Context(this);
	}
}
