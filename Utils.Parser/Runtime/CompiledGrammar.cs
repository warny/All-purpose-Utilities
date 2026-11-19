using Utils.Parser.Model;
using System.IO;

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
/// This class is not thread-safe: it owns one <see cref="LexerEngine"/> and one <see cref="ParserEngine"/>, whose
/// per-operation registries are reset by every call. Concurrent calls must use separate instances.
/// </remarks>
public sealed class CompiledGrammar
{
    private readonly LexerEngine _lexer;
    private readonly ParserEngine _parser;

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
        _lexer = new LexerEngine(definition, runtimeFeaturePolicy, resourceLimits);
        _parser = new ParserEngine(definition, runtimeFeaturePolicy, resourceLimits);
    }

    /// <summary>Gets the lexer engine, for internal diagnostics and resource probes.</summary>
    internal LexerEngine Lexer => _lexer;

    /// <summary>Gets the parser engine, for internal diagnostics and resource probes.</summary>
    internal ParserEngine Parser => _parser;

    /// <summary>
    /// Tokenizes <paramref name="input"/> and returns all tokens produced by the lexer,
    /// in source order. Skipped tokens (e.g. whitespace with <c>-&gt; skip</c>) are already
    /// excluded from the stream at this stage.
    /// </summary>
    /// <param name="input">Source text to tokenize.</param>
    /// <returns>Read-only list of tokens.</returns>
    /// <exception cref="ParserResourceLimitException">The tokenization exceeds <see cref="ResourceLimits"/>.</exception>
    public IReadOnlyList<Token> Tokenize(string input)
    {
        RejectOversizedInput(input);
        var stream = new StringReader(input);
        return _lexer.Tokenize(stream).ToList();
    }

    /// <summary>
    /// Runs the full parse pipeline on <paramref name="input"/>: tokenizes the text
    /// with the embedded <see cref="LexerEngine"/> and then builds a parse tree with the
    /// <see cref="ParserEngine"/>.
    /// </summary>
    /// <param name="input">Source text to parse.</param>
    /// <returns>
    /// The root <see cref="ParseNode"/>.  Returns an <see cref="ErrorNode"/> when the
    /// input does not conform to the grammar rather than throwing an exception.
    /// </returns>
    /// <exception cref="ParserResourceLimitException">
    /// The tokenization or the parse exceeds <see cref="ResourceLimits"/>; this is a refusal, not a syntax error.
    /// </exception>
    public ParseNode Parse(string input)
    {
        RejectOversizedInput(input);
        var stream = new StringReader(input);
        var tokens = _lexer.Tokenize(stream).ToList();
        return _parser.Parse(tokens);
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
