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
			System.Diagnostics.Debug.WriteLine($"Group '{groupName}' push {value.Value} ({value.Start}=>{value.End})");

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
			var value = group.Pop();
			System.Diagnostics.Debug.WriteLine($"Group '{groupName}' pop {value.Value} ({value.Start}=>{value.End})");
			return value;
		}
		internal ParserIndex PeekGroup(string groupName)
		{
			if (!Groups.TryGetValue(groupName, out Group group)) return null;
			ParserIndex value = group.Peek();
			System.Diagnostics.Debug.WriteLine($"Group '{groupName}' peek {value.Value} ({value.Start}=>{value.End})");
			return value;
		}

		public Context Clone() => new Context(this);
	}
}
