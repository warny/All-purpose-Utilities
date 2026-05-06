namespace Utils.Parser.Runtime;

internal readonly record struct RuleInvocationKey(
    string RuleName,
    int OriginPosition,
    int MinimumPrecedence);

internal readonly record struct ContinuationKey(
    string CallerRuleName,
    int CallerAlternativeIndex,
    int CallerElementIndex,
    int ResumePosition,
    int MinimumPrecedence);

internal readonly record struct ParserStateKey(
    string RuleName,
    int InputPosition,
    int AlternativeIndex,
    int ElementIndex,
    int MinimumPrecedence);
