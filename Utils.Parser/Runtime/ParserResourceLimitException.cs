namespace Utils.Parser.Runtime;

/// <summary>The resource dimension whose limit a lexer, parser or parse-tree compilation operation exceeded.</summary>
public enum ParserResourceLimitKind
{
    /// <summary>The input has more characters than <see cref="ParserResourceLimits.MaxInputCharacters"/>.</summary>
    InputCharacters,

    /// <summary>The tokenization emitted more tokens than <see cref="ParserResourceLimits.MaxTokens"/>.</summary>
    Tokens,

    /// <summary>The tokenization performed more matching steps than <see cref="ParserResourceLimits.MaxLexerSteps"/>.</summary>
    LexerSteps,

    /// <summary>Lexer rule matching nested deeper than <see cref="ParserResourceLimits.MaxLexerDepth"/>.</summary>
    LexerDepth,

    /// <summary>Parser recursion nested deeper than <see cref="ParserResourceLimits.MaxParseDepth"/>.</summary>
    ParseDepth,

    /// <summary>The parse performed more steps than <see cref="ParserResourceLimits.MaxParserSteps"/>.</summary>
    ParserSteps,

    /// <summary>The parse constructed more nodes than <see cref="ParserResourceLimits.MaxParseNodes"/>.</summary>
    ParseNodes,

    /// <summary>The parse retained more states than <see cref="ParserResourceLimits.MaxRetainedStates"/>.</summary>
    RetainedStates,

    /// <summary>The remaining execution stack was insufficient to continue the recursion safely.</summary>
    Stack,
}

/// <summary>The operation phase in which a parser resource limit was exceeded.</summary>
public enum ParserResourcePhase
{
    /// <summary>Tokenization by <see cref="LexerEngine"/>.</summary>
    Lexer,

    /// <summary>Parsing of a token stream by <see cref="ParserEngine"/>.</summary>
    Parser,

    /// <summary>Traversal of a parse tree by <see cref="ParseTreeCompiler{TContext, TResult}"/>.</summary>
    TreeCompilation,
}

/// <summary>
/// Thrown when a tokenization, parse or parse-tree compilation exceeds one of its <see cref="ParserResourceLimits"/>, or
/// when the remaining execution stack is insufficient to continue it safely.
/// </summary>
/// <remarks>
/// <para>
/// This is a refusal, never a result and never a syntax error: syntax errors still produce an <see cref="ErrorNode"/>
/// and diagnostics, while this exception means the operation was stopped before it could decide anything. No partial
/// or truncated parse tree is returned.
/// </para>
/// <para>
/// The message and properties never contain input text, only the limit kind, the phase, the configured limit, the
/// attempted value and, when known, the input character offset. A <see cref="ParserResourceLimitKind.Stack"/> refusal
/// keeps an <see cref="InsufficientExecutionStackException"/> as <see cref="Exception.InnerException"/>.
/// </para>
/// <para>
/// The engines release every parser-managed per-operation state (active rule frames, registry, memo and look-ahead
/// entries, budget counters) when they throw this exception, and run the exit hooks of every rule frame and rule call
/// they had entered, so the same engine can run further operations. Exceptions thrown by injected runtime policies are
/// never converted into this type.
/// </para>
/// </remarks>
public sealed class ParserResourceLimitException : InvalidOperationException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="kind">The exceeded dimension.</param>
    /// <param name="phase">The phase that exceeded it.</param>
    /// <param name="limit">The configured limit (0 for <see cref="ParserResourceLimitKind.Stack"/>).</param>
    /// <param name="observed">The attempted value that crossed the limit (the current depth for <see cref="ParserResourceLimitKind.Stack"/>).</param>
    /// <param name="offset">The input character offset where the operation stopped, when known.</param>
    public ParserResourceLimitException(ParserResourceLimitKind kind, ParserResourcePhase phase, long limit, long observed, int? offset)
        : this(kind, phase, limit, observed, offset, null)
    {
    }

    /// <summary>Initializes a new instance with an inner exception.</summary>
    /// <param name="kind">The exceeded dimension.</param>
    /// <param name="phase">The phase that exceeded it.</param>
    /// <param name="limit">The configured limit (0 for <see cref="ParserResourceLimitKind.Stack"/>).</param>
    /// <param name="observed">The attempted value that crossed the limit (the current depth for <see cref="ParserResourceLimitKind.Stack"/>).</param>
    /// <param name="offset">The input character offset where the operation stopped, when known.</param>
    /// <param name="innerException">The exception that caused this one, such as an <see cref="InsufficientExecutionStackException"/>.</param>
    public ParserResourceLimitException(ParserResourceLimitKind kind, ParserResourcePhase phase, long limit, long observed, int? offset, Exception? innerException)
        : base(BuildMessage(kind, phase, limit, observed, offset), innerException)
    {
        Kind = kind;
        Phase = phase;
        Limit = limit;
        Observed = observed;
        Offset = offset;
    }

    /// <summary>Gets the exceeded dimension.</summary>
    public ParserResourceLimitKind Kind { get; }

    /// <summary>Gets the phase that exceeded the limit.</summary>
    public ParserResourcePhase Phase { get; }

    /// <summary>Gets the configured limit (0 for <see cref="ParserResourceLimitKind.Stack"/>).</summary>
    public long Limit { get; }

    /// <summary>
    /// Gets the attempted value that crossed the limit: the first value over it, since the operation stops as soon as the
    /// limit is crossed (the current recursion depth for <see cref="ParserResourceLimitKind.Stack"/>).
    /// </summary>
    public long Observed { get; }

    /// <summary>
    /// Gets the zero-based input character offset where the operation stopped (the current character for the lexer, the
    /// start of the current token for the parser), or <see langword="null"/> when it is not known.
    /// </summary>
    public int? Offset { get; }

    /// <summary>Builds the exception message without any input text.</summary>
    /// <param name="kind">The exceeded dimension.</param>
    /// <param name="phase">The phase.</param>
    /// <param name="limit">The configured limit.</param>
    /// <param name="observed">The attempted value.</param>
    /// <param name="offset">The input offset, when known.</param>
    /// <returns>The message.</returns>
    private static string BuildMessage(ParserResourceLimitKind kind, ParserResourcePhase phase, long limit, long observed, int? offset)
    {
        string where = offset is null ? string.Empty : $" at input offset {offset.Value}";
        return kind == ParserResourceLimitKind.Stack
            ? $"The {phase} operation stopped{where} because the remaining execution stack was insufficient (recursion depth {observed})."
            : $"The {phase} operation exceeded its {kind} limit{where} ({observed} > {limit}).";
    }
}
