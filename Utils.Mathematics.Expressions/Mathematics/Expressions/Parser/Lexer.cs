using System.Collections.Generic;

namespace Utils.Mathematics.Expressions.Parser
{
	public class Lexer
	{
		public string DefaultContext { get; set; }
		public IDictionary<string, Context> Contexts { get; private set; } = new Dictionary<string, Context>();
	}
}