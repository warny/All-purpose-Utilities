using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Mathematics.Expressions.Parser
{

	public class Lexer
	{
		public string DefaultContext { get; set; }
		public IDictionary<string, Context> Contexts { get; private set; } = new Dictionary<string, Context>();
	}
}
