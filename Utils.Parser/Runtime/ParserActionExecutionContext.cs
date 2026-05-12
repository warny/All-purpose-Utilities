using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Immutable action execution data passed to <see cref="IParserActionExecutor"/>.
/// </summary>
/// <param name="Rule">Current parser rule.</param>
/// <param name="Action">Action model node.</param>
/// <param name="ActionCode">Raw action code.</param>
/// <param name="InputPosition">Current parser input position.</param>
/// <param name="AlternativeIndex">Current alternative index.</param>
/// <param name="ElementIndex">Current element index within the alternative.</param>
public sealed record ParserActionExecutionContext(
    Rule Rule,
    RuleContent Action,
    string ActionCode,
    int InputPosition,
    int AlternativeIndex,
    int ElementIndex);

