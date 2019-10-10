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
			Groups = context.Groups.Clone() ?? new Groups();
			Result = context.Result?.Clone();
		}

		public Context(Context context, Result result)
		{
			Groups = context.Groups.Clone() ?? new Groups();
			Result = result;
		}

		internal void PushGroup(string groupName, ParserIndex value)
		{
			if (!Groups.TryGetValue(groupName, out Group group))
			{
				group = new Group(groupName);
				Groups.Add(group);
			}
			group.Push(value);
		}
		internal ParserIndex PopGroup(string groupName)
		{
			if (!Groups.TryGetValue(groupName, out Group group)) return null;
			return group.Pop();
		}
		internal ParserIndex PeekGroup(string groupName)
		{
			if (!Groups.TryGetValue(groupName, out Group group)) return null;
			return group.Peek();
		}

		public Context Clone() => new Context(this);
	}
}
