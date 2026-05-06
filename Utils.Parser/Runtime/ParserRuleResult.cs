using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

internal readonly record struct ParserRuleResult(ParseNode? Node, int EndPosition, bool IsFailure);
