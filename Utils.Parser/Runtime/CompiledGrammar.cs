using Utils.Parser.Model;
using System.IO;
using System.Threading;

namespace Utils.Parser.Runtime;

/// <summary>
/// A ready-to-use grammar instance that encapsulates the full parse pipeline:
/// <see cref="LexerEngine"/> tokenization followed by <see cref="ParserEngine"/> parsing.
/// <para>
/// Obtain an instance from a resolved <see cref="ParserDefinition"/> via the constructor,
/// or compile directly from an ANTLR4 grammar source string via
/// <c>Antlr4GrammarConverter.Compile(grammarText)</c>.
/// </para>
/// <para>
/// Every <see cref="Tokenize"/> and <see cref="Parse"/> call runs under <see cref="ResourceLimits"/> with a private,
/// per-call budget; an excess ends the call with a <see cref="ParserResourceLimitException"/>.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Tokenize"/> and <see cref="Parse"/> may be called concurrently on one shared instance, from any number of
/// threads and without external locking, when the runtime feature policy is <see cref="ParserRuntimeFeaturePolicy.Default"/>
/// or any policy whose components are stateless or themselves thread-safe. Every call runs on a private
/// <see cref="LexerEngine"/> and a private <see cref="ParserEngine"/>, so the mode stack, recursion frames, state
/// registry, memoized results, look-ahead cache and resource budget of one operation are never visible to another one.
/// Only grammar-invariant data is shared: the immutable <see cref="Definition"/>, <see cref="ResourceLimits"/>, and the
/// lexer and parser preparations (keyword tries, candidate rules, scheduling shapes) built once by the constructor and
/// never mutated afterwards.
/// </para>
/// <para>
/// The policy components themselves are not copied: a custom policy whose components keep mutable state (for example
/// a <see cref="StackParserRuleInvocationFrameManager"/>, a recording <see cref="IParserRuntimeObserver"/> or a policy
/// bound to a generated execution context) is shared by every call of this instance, and concurrent calls are then
/// supported only if the caller guarantees that those components are thread-safe; otherwise use one instance per
/// concurrent operation.
/// </para>
/// </remarks>
public sealed class CompiledGrammar
{
    /// <summary>The runtime feature policy handed to the engines of every call.</summary>
    private readonly ParserRuntimeFeaturePolicy _runtimeFeaturePolicy;

    /// <summary>The grammar-invariant lexer tables, shared by the lexer engines of every call.</summary>
    private readonly LexerEngine.Preparation _lexerPreparation;

    /// <summary>The grammar-invariant scheduling data, shared by the parser engines of every call.</summary>
    private readonly ParserEngine.Preparation _parserPreparation;

    /// <summary>The lexer engine of the last call that started a tokenization, for internal diagnostics only.</summary>
    private LexerEngine _lastLexer;

    /// <summary>The parser engine of the last call that started a parse, for internal diagnostics only.</summary>
    private ParserEngine _lastParser;

    /// <summary>The resolved <see cref="ParserDefinition"/> backing this grammar.</summary>
    public ParserDefinition Definition { get; }

    /// <summary>Gets the resource limits applied to every tokenization and parse of this grammar.</summary>
    public ParserResourceLimits ResourceLimits { get; }

    /// <summary>
    /// Initialises a <see cref="CompiledGrammar"/> from a fully resolved
    /// <see cref="ParserDefinition"/> (i.e. the output of <c>RuleResolver.Resolve</c>).
    /// </summary>
    /// <param name="definition">A resolved grammar definition.</param>
    public CompiledGrammar(ParserDefinition definition)
        : this(definition, ParserRuntimeFeaturePolicy.Default)
    {
    }

    /// <summary>
    /// Initialises a <see cref="CompiledGrammar"/> from a fully resolved
    /// <see cref="ParserDefinition"/> with an explicit runtime feature policy and <see cref="ParserResourceLimits.Default"/>.
    /// </summary>
    /// <param name="definition">A resolved grammar definition.</param>
    /// <param name="runtimeFeaturePolicy">Runtime feature policy applied to parser execution.</param>
    public CompiledGrammar(ParserDefinition definition, ParserRuntimeFeaturePolicy runtimeFeaturePolicy)
        : this(definition, runtimeFeaturePolicy, ParserResourceLimits.Default)
    {
    }

    /// <summary>
    /// Initialises a <see cref="CompiledGrammar"/> from a fully resolved
    /// <see cref="ParserDefinition"/> with an explicit runtime feature policy and explicit resource limits, for example
    /// <see cref="ParserResourceLimits.Unlimited"/> for trusted inputs larger than the defaults allow.
    /// </summary>
    /// <param name="definition">A resolved grammar definition.</param>
    /// <param name="runtimeFeaturePolicy">Runtime feature policy applied to parser execution.</param>
    /// <param name="resourceLimits">Limits applied to each tokenization and parse.</param>
    public CompiledGrammar(ParserDefinition definition, ParserRuntimeFeaturePolicy runtimeFeaturePolicy, ParserResourceLimits resourceLimits)
    {
        ArgumentNullException.ThrowIfNull(resourceLimits);
        Definition = definition;
        ResourceLimits = resourceLimits;
        _runtimeFeaturePolicy = runtimeFeaturePolicy;
        _lexerPreparation = new LexerEngine.Preparation(definition);
        _parserPreparation = new ParserEngine.Preparation(definition);

        // The first pair of engines validates the policy exactly as the historical constructor did, and serves the
        // internal diagnostic accessors until the first call.
        _lastLexer = new LexerEngine(_lexerPreparation, runtimeFeaturePolicy, resourceLimits);
        _lastParser = new ParserEngine(_parserPreparation, runtimeFeaturePolicy, resourceLimits);
    }

    /// <summary>
    /// Gets the lexer engine of the last call that started a tokenization on this grammar, for internal diagnostics and
    /// resource probes only. It is meaningful only while the grammar is used sequentially.
    /// </summary>
    internal LexerEngine Lexer => Volatile.Read(ref _lastLexer);

    /// <summary>
    /// Gets the parser engine of the last call that started a parse on this grammar, for internal diagnostics and
    /// resource probes only. It is meaningful only while the grammar is used sequentially.
    /// </summary>
    internal ParserEngine Parser => Volatile.Read(ref _lastParser);

    /// <summary>
    /// Tokenizes <paramref name="input"/> and returns all tokens produced by the lexer,
    /// in source order. Skipped tokens (e.g. whitespace with <c>-&gt; skip</c>) are already
    /// excluded from the stream at this stage.
    /// </summary>
    /// <param name="input">Source text to tokenize.</param>
    /// <returns>Read-only list of tokens.</returns>
    /// <exception cref="ParserResourceLimitException">The tokenization exceeds <see cref="ResourceLimits"/>.</exception>
    /// <remarks>Safe to call concurrently with other <see cref="Tokenize"/> and <see cref="Parse"/> calls; see the class remarks.</remarks>
    public IReadOnlyList<Token> Tokenize(string input)
    {
        RejectOversizedInput(input);
        var stream = new StringReader(input);
        return CreateLexer().Tokenize(stream).ToList();
    }

    /// <summary>
    /// Runs the full parse pipeline on <paramref name="input"/>: tokenizes the text
    /// with a <see cref="LexerEngine"/> and then builds a parse tree with a
    /// <see cref="ParserEngine"/>, both private to this call.
    /// </summary>
    /// <param name="input">Source text to parse.</param>
    /// <returns>
    /// The root <see cref="ParseNode"/>.  Returns an <see cref="ErrorNode"/> when the
    /// input does not conform to the grammar rather than throwing an exception.
    /// </returns>
    /// <exception cref="ParserResourceLimitException">
    /// The tokenization or the parse exceeds <see cref="ResourceLimits"/>; this is a refusal, not a syntax error.
    /// </exception>
    /// <remarks>Safe to call concurrently with other <see cref="Tokenize"/> and <see cref="Parse"/> calls; see the class remarks.</remarks>
    public ParseNode Parse(string input)
    {
        RejectOversizedInput(input);
        var stream = new StringReader(input);
        var tokens = CreateLexer().Tokenize(stream).ToList();
        return CreateParser().Parse(tokens);
    }

    /// <summary>Creates the private lexer engine of one call over the shared preparation.</summary>
    /// <returns>The engine.</returns>
    private LexerEngine CreateLexer()
    {
        var lexer = new LexerEngine(_lexerPreparation, _runtimeFeaturePolicy, ResourceLimits);
        Volatile.Write(ref _lastLexer, lexer);
        return lexer;
    }

    /// <summary>Creates the private parser engine of one call over the shared preparation.</summary>
    /// <returns>The engine.</returns>
    private ParserEngine CreateParser()
    {
        var parser = new ParserEngine(_parserPreparation, _runtimeFeaturePolicy, ResourceLimits);
        Volatile.Write(ref _lastParser, parser);
        return parser;
    }

    /// <summary>
    /// Refuses an input longer than <see cref="ParserResourceLimits.MaxInputCharacters"/> before any tokenization work;
    /// the lexer enforces the same limit while reading from arbitrary readers.
    /// </summary>
    /// <param name="input">The source text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ParserResourceLimitException">The input is too long.</exception>
    private void RejectOversizedInput(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length > ResourceLimits.MaxInputCharacters)
        {
            throw new ParserResourceLimitException(
                ParserResourceLimitKind.InputCharacters,
                ParserResourcePhase.Lexer,
                ResourceLimits.MaxInputCharacters,
                input.Length,
                ResourceLimits.MaxInputCharacters);
        }
    }
}
