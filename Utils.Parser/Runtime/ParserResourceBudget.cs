using System.Runtime.CompilerServices;

namespace Utils.Parser.Runtime;

/// <summary>
/// The private resource budget of one tokenization, parse or parse-tree compilation, created when the operation starts
/// and discarded when it ends. It enforces <see cref="ParserResourceLimits"/> and the execution-stack guard.
/// </summary>
/// <remarks>
/// Counters only grow, except the current depth which is restored by <see cref="ExitDepth"/> in <c>finally</c> blocks:
/// backtracking restores token positions and semantic state, never consumed budget. Every check runs before the counter
/// crosses its limit, so no counter can overflow. An instance is never shared between operations or threads.
/// </remarks>
internal sealed class ParserResourceBudget
{
    /// <summary>The limits in force.</summary>
    private readonly ParserResourceLimits _limits;

    /// <summary>The phase reported by refusals.</summary>
    private readonly ParserResourcePhase _phase;

    /// <summary>Resolves the current input character offset, only when a refusal is built.</summary>
    private readonly Func<int?> _offset;

    /// <summary>Current recursion depth.</summary>
    private int _depth;

    /// <summary>Initializes a budget.</summary>
    /// <param name="limits">The limits in force.</param>
    /// <param name="phase">The phase reported by refusals.</param>
    /// <param name="offset">Resolves the current input character offset when a refusal is built.</param>
    public ParserResourceBudget(ParserResourceLimits limits, ParserResourcePhase phase, Func<int?> offset)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _phase = phase;
        _offset = offset ?? throw new ArgumentNullException(nameof(offset));
    }

    /// <summary>Gets the limits in force.</summary>
    public ParserResourceLimits Limits => _limits;

    /// <summary>Gets the current recursion depth.</summary>
    public int Depth => _depth;

    /// <summary>Gets the maximum recursion depth reached.</summary>
    public int MaxDepthReached { get; private set; }

    /// <summary>Gets the steps charged so far.</summary>
    public long Steps { get; private set; }

    /// <summary>Gets the parse nodes charged so far.</summary>
    public long Nodes { get; private set; }

    /// <summary>Gets the retained states charged so far.</summary>
    public long RetainedStates { get; private set; }

    /// <summary>Gets the tokens charged so far.</summary>
    public long Tokens { get; private set; }

    /// <summary>Gets the input characters charged so far.</summary>
    public long InputCharacters { get; private set; }

    /// <summary>
    /// Enters one level of guarded recursion: checks the execution stack, then the depth limit of the phase. Every
    /// successful call must be paired with <see cref="ExitDepth"/> in a <c>finally</c> block.
    /// </summary>
    /// <exception cref="ParserResourceLimitException">The stack is insufficient or the depth limit is reached.</exception>
    public void EnterDepth()
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new ParserResourceLimitException(ParserResourceLimitKind.Stack, _phase, 0, _depth, _offset(), new InsufficientExecutionStackException());
        }

        int limit = _phase == ParserResourcePhase.Lexer ? _limits.MaxLexerDepth : _limits.MaxParseDepth;
        if (_depth >= limit)
        {
            throw Refuse(_phase == ParserResourcePhase.Lexer ? ParserResourceLimitKind.LexerDepth : ParserResourceLimitKind.ParseDepth, limit, (long)_depth + 1);
        }

        _depth++;
        if (_depth > MaxDepthReached)
        {
            MaxDepthReached = _depth;
        }
    }

    /// <summary>Leaves one level entered by <see cref="EnterDepth"/>.</summary>
    public void ExitDepth() => _depth--;

    /// <summary>
    /// Checks the execution stack without counting a depth level, for recursive walks whose depth is bounded by an
    /// already-validated structure (parse-tree compilation).
    /// </summary>
    /// <exception cref="ParserResourceLimitException">The stack is insufficient.</exception>
    public void EnsureStack()
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new ParserResourceLimitException(ParserResourceLimitKind.Stack, _phase, 0, _depth, _offset(), new InsufficientExecutionStackException());
        }
    }

    /// <summary>Charges one lexer or parser step.</summary>
    /// <exception cref="ParserResourceLimitException">The step budget of the phase is exhausted.</exception>
    public void ChargeStep()
    {
        long limit = _phase == ParserResourcePhase.Lexer ? _limits.MaxLexerSteps : _limits.MaxParserSteps;
        if (Steps >= limit)
        {
            throw Refuse(_phase == ParserResourcePhase.Lexer ? ParserResourceLimitKind.LexerSteps : ParserResourceLimitKind.ParserSteps, limit, Steps + 1);
        }

        Steps++;
    }

    /// <summary>Charges one constructed parse node.</summary>
    /// <exception cref="ParserResourceLimitException">The node budget is exhausted.</exception>
    public void ChargeNode()
    {
        if (Nodes >= _limits.MaxParseNodes)
        {
            throw Refuse(ParserResourceLimitKind.ParseNodes, _limits.MaxParseNodes, Nodes + 1);
        }

        Nodes++;
    }

    /// <summary>Charges one retained parser state, before it is inserted.</summary>
    /// <exception cref="ParserResourceLimitException">The retained-state budget is exhausted.</exception>
    public void ChargeRetainedState()
    {
        if (RetainedStates >= _limits.MaxRetainedStates)
        {
            throw Refuse(ParserResourceLimitKind.RetainedStates, _limits.MaxRetainedStates, RetainedStates + 1);
        }

        RetainedStates++;
    }

    /// <summary>Charges one emitted token.</summary>
    /// <exception cref="ParserResourceLimitException">The token budget is exhausted.</exception>
    public void ChargeToken()
    {
        if (Tokens >= _limits.MaxTokens)
        {
            throw Refuse(ParserResourceLimitKind.Tokens, _limits.MaxTokens, Tokens + 1);
        }

        Tokens++;
    }

    /// <summary>Charges one input character read from the source.</summary>
    /// <exception cref="ParserResourceLimitException">The input-character budget is exhausted.</exception>
    public void ChargeInputCharacter()
    {
        if (InputCharacters >= _limits.MaxInputCharacters)
        {
            throw Refuse(ParserResourceLimitKind.InputCharacters, _limits.MaxInputCharacters, InputCharacters + 1);
        }

        InputCharacters++;
    }

    /// <summary>Takes a snapshot of the counters.</summary>
    /// <returns>The usage.</returns>
    public ParserResourceUsage Snapshot() => new(_phase, MaxDepthReached, Steps, Nodes, RetainedStates, Tokens, InputCharacters);

    /// <summary>Builds a refusal.</summary>
    /// <param name="kind">The exceeded dimension.</param>
    /// <param name="limit">The limit.</param>
    /// <param name="attempted">The attempted value.</param>
    /// <returns>The exception to throw.</returns>
    private ParserResourceLimitException Refuse(ParserResourceLimitKind kind, long limit, long attempted)
        => new(kind, _phase, limit, attempted, _offset());
}

/// <summary>Counters of one finished (or refused) operation, kept for internal diagnostics and resource probes.</summary>
/// <param name="Phase">The phase.</param>
/// <param name="MaxDepth">The maximum recursion depth reached.</param>
/// <param name="Steps">Lexer or parser steps.</param>
/// <param name="Nodes">Constructed parse nodes.</param>
/// <param name="RetainedStates">Retained parser states.</param>
/// <param name="Tokens">Emitted tokens.</param>
/// <param name="InputCharacters">Input characters read.</param>
internal sealed record ParserResourceUsage(ParserResourcePhase Phase, int MaxDepth, long Steps, long Nodes, long RetainedStates, long Tokens, long InputCharacters);
