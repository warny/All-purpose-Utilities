using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utils.Mathematics.Expressions
{
	static class ParserRegEx
	{
		private static string[][] operators = new[] {
			new [] { "+" , "-" },
			new [] { "*" , "/", "%" },
			new [] { "^" },
			new [] { "+" , "-" },
			new [] { ">>", "<<" },
			new [] { "==", "!=", "<", ">", "<=", ">=" },
			new [] { "&" },
			new [] { "#" },
			new [] { "|" },
			new [] { "&&" },
			new [] { "||" },
			new [] { "??" },
			new [] { "?" }
		};

		public static readonly char[] TrimCharacters = new char [] { ' ', '\r', '\n', '\t', '\0' };

		private static RegexOptions regexOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture;

		public static Regex InstructionBlockSplitter { get; } = new Regex(ParserRegExResources.InstructionBlockSplitter, regexOptions);
		public static Regex InstructionStart { get; } = new Regex(ParserRegExResources.InstructionStart, regexOptions);
		public static Regex InstructionTokenizer { get; } = new Regex(ParserRegExResources.InstructionTokenizer.Replace(
			"{{operators}}", 
			string.Join("|", operators.SelectMany(o=>o.Select(o1=>Regex.Replace(o1,@"(.)", @"\\1"))))), 
			regexOptions);
	}
}
