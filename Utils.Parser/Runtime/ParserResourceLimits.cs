namespace Utils.Parser.Runtime;

/// <summary>
/// Immutable resource limits applied by <see cref="LexerEngine"/>, <see cref="ParserEngine"/>,
/// <see cref="CompiledGrammar"/> and <see cref="ParseTreeCompiler{TContext, TResult}"/> to one operation
/// (one tokenization, one parse, or one parse-tree compilation).
/// </summary>
/// <remarks>
/// <para>
/// Every operation owns a private budget created when it starts and discarded when it ends, successfully or not.
/// Counters are never shared between operations, engines, threads or processes, and a failed operation never leaves
/// budget state behind. Limits are deterministic work and size bounds: none of them depends on wall-clock time.
/// </para>
/// <para>
/// Lexer limits (<see cref="MaxInputCharacters"/>, <see cref="MaxTokens"/>, <see cref="MaxLexerSteps"/>,
/// <see cref="MaxLexerDepth"/>) bound a tokenization, including the tokenization performed by
/// <see cref="CompiledGrammar.Parse(string)"/>. Parser limits (<see cref="MaxParseDepth"/>,
/// <see cref="MaxParserSteps"/>, <see cref="MaxParseNodes"/>, <see cref="MaxRetainedStates"/>) bound the parse of a
/// token stream. Exceeding any limit ends the operation with a <see cref="ParserResourceLimitException"/>; it is never
/// reported as a syntax error and never produces a truncated parse tree.
/// </para>
/// <para>
/// Independently of these limits, every recursive lexer, parser and parse-tree-compilation step checks the remaining
/// execution stack. When the stack is insufficient, the operation ends with a
/// <see cref="ParserResourceLimitException"/> of kind <see cref="ParserResourceLimitKind.Stack"/> instead of a
/// process-terminating stack overflow. This guard cannot be disabled, not even by <see cref="Unlimited"/>.
/// </para>
/// <para>
/// Limits count parser-managed work only. They cannot interrupt user code running inside a custom runtime policy
/// (semantic predicate evaluator, action executor, state manager, lifecycle executor, rule-call policy, observer, or
/// lexer extension); such code is charged only for the parser work it causes.
/// </para>
/// </remarks>
public sealed record ParserResourceLimits
{
    // Default rationale (S-06 measurements, docs/parser/ParserResourceSafety.md): the historical 20,000-term CSyntax sum
    // (168,887 characters, 39,999 tokens) needs 2.45 M lexer steps, 3.94 M parser steps, 260 k nodes and 3.0 M retained
    // states, so it stays accepted; the parser budgets bound one parse to roughly 2.5 times that work. The depth limits
    // sit above the deepest input the baseline parsed on a 1 MiB stack (where the stack guard now refuses first) and
    // below the physical limit of an 8 MiB stack, so they make large-stack refusals deterministic.

    /// <summary>The default maximum number of input characters read by one tokenization: 1,000,000.</summary>
    public const int DefaultMaxInputCharacters = 1_000_000;

    /// <summary>The default maximum number of tokens emitted by one tokenization, all channels included: 500,000.</summary>
    public const int DefaultMaxTokens = 500_000;

    /// <summary>The default maximum number of lexer matching steps of one tokenization: 50,000,000.</summary>
    public const long DefaultMaxLexerSteps = 50_000_000;

    /// <summary>The default maximum nesting depth of lexer rule matching: 4,000.</summary>
    public const int DefaultMaxLexerDepth = 4_000;

    /// <summary>The default maximum nesting depth of parser recursion (rule invocations and rule-content dispatches): 4,000.</summary>
    public const int DefaultMaxParseDepth = 4_000;

    /// <summary>The default maximum number of parser steps of one parse: 10,000,000.</summary>
    public const long DefaultMaxParserSteps = 10_000_000;

    /// <summary>The default maximum number of parse nodes constructed by one parse, failed branches included: 5,000,000.</summary>
    public const long DefaultMaxParseNodes = 5_000_000;

    /// <summary>The default maximum number of parser states retained by one parse (registry, memo and look-ahead entries): 5,000,000.</summary>
    public const long DefaultMaxRetainedStates = 5_000_000;

    /// <summary>Backing field of <see cref="MaxInputCharacters"/>.</summary>
    private readonly int _maxInputCharacters = DefaultMaxInputCharacters;

    /// <summary>Backing field of <see cref="MaxTokens"/>.</summary>
    private readonly int _maxTokens = DefaultMaxTokens;

    /// <summary>Backing field of <see cref="MaxLexerSteps"/>.</summary>
    private readonly long _maxLexerSteps = DefaultMaxLexerSteps;

    /// <summary>Backing field of <see cref="MaxLexerDepth"/>.</summary>
    private readonly int _maxLexerDepth = DefaultMaxLexerDepth;

    /// <summary>Backing field of <see cref="MaxParseDepth"/>.</summary>
    private readonly int _maxParseDepth = DefaultMaxParseDepth;

    /// <summary>Backing field of <see cref="MaxParserSteps"/>.</summary>
    private readonly long _maxParserSteps = DefaultMaxParserSteps;

    /// <summary>Backing field of <see cref="MaxParseNodes"/>.</summary>
    private readonly long _maxParseNodes = DefaultMaxParseNodes;

    /// <summary>Backing field of <see cref="MaxRetainedStates"/>.</summary>
    private readonly long _maxRetainedStates = DefaultMaxRetainedStates;

    /// <summary>Gets the default limits, applied by every engine constructor that does not take explicit limits.</summary>
    public static ParserResourceLimits Default { get; } = new();

    /// <summary>
    /// Gets limits that disable every size, depth and work check, for trusted inputs only. The execution-stack guard
    /// stays active.
    /// </summary>
    public static ParserResourceLimits Unlimited { get; } = new()
    {
        MaxInputCharacters = int.MaxValue,
        MaxTokens = int.MaxValue,
        MaxLexerSteps = long.MaxValue,
        MaxLexerDepth = int.MaxValue,
        MaxParseDepth = int.MaxValue,
        MaxParserSteps = long.MaxValue,
        MaxParseNodes = long.MaxValue,
        MaxRetainedStates = long.MaxValue,
    };

    /// <summary>Gets the maximum number of characters one tokenization reads from its input.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxInputCharacters
    {
        get => _maxInputCharacters;
        init => _maxInputCharacters = RequirePositive(value, nameof(MaxInputCharacters));
    }

    /// <summary>
    /// Gets the maximum number of tokens one tokenization emits, every channel and lexer-extension token included.
    /// Skipped tokens are not emitted and are charged as lexer steps only. The same limit bounds the tokens supplied
    /// directly to <see cref="ParserEngine.Parse"/>: every received token counts, whatever its channel; a list longer than
    /// the limit is refused before it is examined, and a lazy sequence is refused before the first token over the limit
    /// is examined or stored (kind <see cref="ParserResourceLimitKind.Tokens"/>, phase <see cref="ParserResourcePhase.Parser"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxTokens
    {
        get => _maxTokens;
        init => _maxTokens = RequirePositive(value, nameof(MaxTokens));
    }

    /// <summary>
    /// Gets the maximum number of lexer matching steps of one tokenization: one step per rule-content match attempt
    /// (literal, range, set, rule reference, sequence, alternation, quantifier iteration, negation, predicate) and one per
    /// candidate rule tried at a token start.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long MaxLexerSteps
    {
        get => _maxLexerSteps;
        init => _maxLexerSteps = RequirePositive(value, nameof(MaxLexerSteps));
    }

    /// <summary>Gets the maximum nesting depth of lexer rule-content matching (recursive lexer rules and fragments).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxLexerDepth
    {
        get => _maxLexerDepth;
        init => _maxLexerDepth = RequirePositive(value, nameof(MaxLexerDepth));
    }

    /// <summary>
    /// Gets the maximum nesting depth of parser recursion: every active rule invocation and every active rule-content
    /// dispatch (sequence, alternation, quantifier, negation, rule reference, predicate, action) counts one level,
    /// whatever the rule names and whether or not input was consumed between levels.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxParseDepth
    {
        get => _maxParseDepth;
        init => _maxParseDepth = RequirePositive(value, nameof(MaxParseDepth));
    }

    /// <summary>
    /// Gets the maximum number of parser steps of one parse: one step per rule invocation (memoized or not), rule-content
    /// dispatch, scheduled alternative attempt, look-ahead probe, quantifier iteration and left-recursive extension
    /// attempt. Steps spent in a branch that is later rejected or backtracked are never refunded.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long MaxParserSteps
    {
        get => _maxParserSteps;
        init => _maxParserSteps = RequirePositive(value, nameof(MaxParserSteps));
    }

    /// <summary>
    /// Gets the maximum number of parse nodes one parse constructs, counting every <see cref="ParserNode"/>,
    /// <see cref="QuantifierNode"/> and <see cref="LexerNode"/> the parser builds (empty predicate and action nodes
    /// included), including the nodes of branches that are later rejected. Scheduler-internal placeholders and the
    /// final <see cref="ErrorNode"/> of a failed parse are charged as part of their step instead. The accepted tree is
    /// never truncated to fit this limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long MaxParseNodes
    {
        get => _maxParseNodes;
        init => _maxParseNodes = RequirePositive(value, nameof(MaxParseNodes));
    }

    /// <summary>
    /// Gets the maximum number of parser states one parse retains until it ends: visited-state keys, continuation
    /// entries, completed (memoized) rule results and look-ahead cache entries. When it is exhausted the whole parse is
    /// refused; no entry is evicted or silently dropped.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long MaxRetainedStates
    {
        get => _maxRetainedStates;
        init => _maxRetainedStates = RequirePositive(value, nameof(MaxRetainedStates));
    }

    /// <summary>Validates a limit.</summary>
    /// <param name="value">The value.</param>
    /// <param name="name">The property name.</param>
    /// <returns><paramref name="value"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than 1.</exception>
    private static int RequirePositive(int value, string name)
        => value >= 1 ? value : throw new ArgumentOutOfRangeException(name, value, "A parser resource limit must be at least 1.");

    /// <summary>Validates a limit.</summary>
    /// <param name="value">The value.</param>
    /// <param name="name">The property name.</param>
    /// <returns><paramref name="value"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than 1.</exception>
    private static long RequirePositive(long value, string name)
        => value >= 1 ? value : throw new ArgumentOutOfRangeException(name, value, "A parser resource limit must be at least 1.");
}
