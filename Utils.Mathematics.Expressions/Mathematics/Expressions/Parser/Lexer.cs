using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Mathematics.Expressions.Parser
{
	public class Lexer
	{
		public string DefaultContext { get; set; }
		public IList<LexerEntry> Rules { get; private set; } = new List<LexerEntry>();

		public IEnumerable<LexerEntry> FindRulesByName(string name) => Rules.Where(r => r.Name.Equals(name));
	}

	public sealed class LexerEntry : IEquatable<string>
	{
		public string Name { get; }
		public Rule Rule { get; }

		public bool Equals(string other) => this.Name == other;
	}
}