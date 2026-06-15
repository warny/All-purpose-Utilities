using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Context object passed to lexer extensions.
/// </summary>
public sealed class LexerExtensionContext
{
    /// <summary>Token names declared in <c>tokens { ... }</c> blocks or backed by lexer rules, used by <see cref="IsTokenKnown"/>.</summary>
    private readonly HashSet<string> _knownTokenNames;

    /// <summary>
    /// Initializes a new context.
    /// </summary>
    public LexerExtensionContext(
        ParserDefinition definition,
        TextReaderLookahead input,
        IReadOnlyList<Token> emittedTokens,
        string currentMode)
    {
        Definition = definition;
        Input = input;
        EmittedTokens = emittedTokens;
        CurrentMode = currentMode;
        _knownTokenNames = definition.AllRules.Values
            .Where(static rule => rule.Kind == RuleKind.Lexer)
            .Select(static rule => rule.Name)
            .Concat(definition.DeclaredTokens)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Current parser definition.</summary>
    public ParserDefinition Definition { get; }

    /// <summary>Forward-only input lookahead.</summary>
    public TextReaderLookahead Input { get; }

    /// <summary>Already emitted tokens.</summary>
    public IReadOnlyList<Token> EmittedTokens { get; }

    /// <summary>Current absolute input position.</summary>
    public int Position => Input.Position;

    /// <summary>Current line.</summary>
    public int Line => Input.Line;

    /// <summary>Current column.</summary>
    public int Column => Input.Column;

    /// <summary>Current lexer mode.</summary>
    public string CurrentMode { get; }

    /// <summary>Returns <c>true</c> when token exists in <c>tokens { ... }</c>.</summary>
    public bool IsTokenDeclared(string tokenName) => Definition.DeclaredTokens.Contains(tokenName);

    /// <summary>Returns <c>true</c> when token is declared or backed by lexer rule.</summary>
    public bool IsTokenKnown(string tokenName) => _knownTokenNames.Contains(tokenName);
}
