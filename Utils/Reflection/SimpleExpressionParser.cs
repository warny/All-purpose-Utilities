
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Utils.Mathematics;

namespace Utils.Reflection
{
    /*
    public class SimpleExpressionParser
    {
        private readonly Parseroptions options;
        private readonly Regex tokenizer;
        private readonly Regex splitter;

        public SimpleExpressionParser(Parseroptions options) { 
            this.options = options;
            this.tokenizer = options.CreateParser();
            this.splitter = options.CreateSplitter();
        }

        public LambdaExpression ParseExpression (string expression, object defaultObject, params ArgumentException[] arguments) {
            Type defaultType = defaultObject?.GetType();

        }
    }

    public class Parseroptions
    {
        public IList<(string name, char start, char end)> Groups { get; } = new List<(string name, char start, char end)>() { 
            ("parenthesis", '(', ')'),
            ("brackets", '[', ']'),
            ("braces", '{', '}'),
        };

        public IList<(string name, char? prefix, char delimiter, char escape)> Delimiters { get; } = new List<(string name, char? prefix, char delimiter, char escape)>()
        {
            ("stringA", null, '\"', '\\'),
            ("stringB", '@', '\"', '\"'),
        };

        public IList<(string name, string op, int priority)> Operators = new List<(string name, string op, int priority)>()
        {
            ("equals", "==", 0),
            ("notEquals", "!=", 0),

            ("member", ".", 0),

            ("add", "+", 10),
            ("substract", "-", 10),

            ("multiply", "*", 20),
            ("divide", "*", 20),

            ("and", "&", 50),
            ("andAlso", "&&", 50),
            ("or", "&", 60),
            ("orElse", "||", 60),
            ("xor", "^", 60),
        };

        [StringSyntax(StringSyntaxAttribute.Regex)]
        public string Numbers { get; } = @"\d+(\.\d+)?";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        public string Names { get; } = @"\w+";

        public char ListSeparator { get; set; } = ',';

        public Regex CreateParser()
        {
            var regexParts = new List<string>();
            string delimitedPartsComplete = CreateDelimitedPartsRegEx(true);
            string delimitedPartsCompact = CreateDelimitedPartsRegEx(false);
            string groupsParts = GroupsParts(delimitedPartsCompact, true);

            regexParts.Add(groupsParts);
            regexParts.Add(delimitedPartsComplete);
            regexParts.Add(Numbers);
            regexParts.Add(Names);
            regexParts.Add(string.Join("|", Operators.Select(o => $"(?<{o.name}>{EscapeSpecialCharsForRegex(o.op)})")));
            regexParts.Add(@"\s*");

            var regex = new Regex(string.Join("|", regexParts), RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);
            return regex;
        }

        private string GroupsParts(string delimitedPartsCompact, bool withNames)
        {
            List<string> groupsPartsList = new List<string>();
            foreach (var item in Groups)
            {
                groupsPartsList.Add(
                    $@"""
                    ({(withNames ? $"?<{item.name}>" : "")}
                        \(
                        (
                            (?<d>{EscapeSpecialCharForRegex(item.start)})
                            |
                            (?<-d>{EscapeSpecialCharForRegex(item.end)})
                            |
                            ({delimitedPartsCompact}|[^{EscapeSpecialCharForCharsGroup(item.start)}{EscapeSpecialCharForCharsGroup(item.end)}])
                        )*
                        \)
                    )
                    """
                );
            }
            var groupsParts = string.Join("|", groupsPartsList);
            return groupsParts;
        }

        private string CreateDelimitedPartsRegEx(bool withNames)
        {
            var delimitedPartsList = new List<string>();

            foreach (var item in Delimiters)
            {
                var exp = $"{item.prefix}{EscapeSpecialCharForRegex(item.delimiter)}({EscapeSpecialCharForRegex(item.escape)}{EscapeSpecialCharForRegex(item.delimiter)}|[^{EscapeSpecialCharForRegex(item.delimiter)}])*{EscapeSpecialCharForRegex(item.delimiter)}";
                delimitedPartsList.Add($"({(withNames ? $"?<{item.name}>" : "")}{exp})");
            }
            var delimitedParts = string.Join("|", delimitedPartsList);
            return delimitedParts;
        }

        public Regex CreateSplitter() {
            string delimitedPartsCompact = CreateDelimitedPartsRegEx(false);
            string groupsParts = GroupsParts(delimitedPartsCompact, false);

            var regex = new Regex("(" + groupsParts + "|" + delimitedPartsCompact + "|[^" + EscapeSpecialCharForCharsGroup(ListSeparator) + "])*");
            return regex;
        }

        public string EscapeSpecialCharsForRegex(string str)
        {
            return new string(str.SelectMany(c => EscapeSpecialCharForRegex(c)).ToArray());
        }

        private static char[] EscapeSpecialCharForRegex(char c)
        {
            return (c.Between('a', 'z') || c.Between('A', 'Z')) ? new char[] { c } : new char[] { '\\', c };
        }

        private static char[] EscapeSpecialCharForCharsGroup(char c)
        {
            return c.In('[', ']') ? new char[] { '\\', c } : new char[] { c };
        }
    }
    */
}