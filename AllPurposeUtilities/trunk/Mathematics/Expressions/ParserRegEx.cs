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
		private static RegexOptions regexOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture;

		public static Regex InstructionBlock { get; } = new Regex(ParserRegExResources.InstructionBlock, regexOptions);
	}
}
