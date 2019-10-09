using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{

	public sealed class Result
	{
		public Groups Groups { get; }

		internal Result()
		{
			this.Success = false;
			this.Groups = new Groups();
		}

		internal Result(Result result)
		{
			this.Index = new ParserIndex(result.Index);
			this.Success = result.Success;
			this.Groups = result.Groups;

		}

		internal Result(int startIndex)
		{
			this.Success = false;
			this.Index = new ParserIndex(startIndex, startIndex, 0, "");
			this.Groups = new Groups();
		}

		internal Result(int startIndex, int endIndex, string value, bool success = true)
		{
			this.Success = success;
			this.Index = new ParserIndex(startIndex, endIndex, endIndex - startIndex, value);
		}

		internal Result(ParserIndex result1, ParserIndex result2, bool success)
		{
			try
			{
				this.Index = result1 + result2;
				this.Success = success;
			}
			catch
			{
				this.Index = null;
				this.Success = false;
			}
		}

		public bool Success { get; internal set; }
		public ParserIndex Index { get; }

		internal void PushGroup(string groupName, ParserIndex value)
		{
			if (!groups.TryGetValue(groupName, out Group group))
			{
				group = new Group();
				groups.Add(groupName, group);
			}
			group.Push(value);
		}
		internal ParserIndex PopGroup(string groupName)
		{
			if (!groups.TryGetValue(groupName, out Group group)) return null;
			return group.Pop();
		}
		internal ParserIndex Peek(string groupName)
		{
			if (!groups.TryGetValue(groupName, out Group group)) return null;
			return group.Peek();
		}


		public static Result operator +(Result result1, Result result2)
		{
			if (result1 == null) return result2;
			return new Result(result1.Index, result2.Index, result1.Success && result2.Success);
		}
	}
}
