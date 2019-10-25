namespace Utils.Mathematics.Expressions.Parser
{
	public class Context
	{
		public Lexer Lexer { get; }
		public Groups Groups { get; }
		public Result Result { get; }

		public Context()
		{
			Lexer = null;
			Groups = new Groups();
			Result = new Result();
		}

		public Context(Lexer lexer)
		{
			Lexer = lexer;
			Groups = new Groups();
			Result = new Result();
		}

		public Context(Context context) : this()
		{
			Lexer = context.Lexer;
			Groups = context.Groups.Clone() ?? new Groups();
			Result = context.Result?.Clone();
		}

		public Context(Context context, Result result)
		{
			Lexer = context.Lexer;
			Groups = context.Groups.Clone() ?? new Groups();
			Result = result;
		}

		internal void PushGroup(string groupName, ParserIndex value) => Groups.Push(groupName, value);

		internal ParserIndex PopGroup(string groupName) => Groups.Pop(groupName);

		internal ParserIndex PeekGroup(string groupName) => Groups.Peek(groupName);

		public Context Clone() => new Context(this);
	}
}