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
/// </summary>
public sealed class CompiledGrammar
{
    private readonly LexerEngine _lexer;
    private readonly ParserEngine _parser;

    /// <summary>The resolved <see cref="ParserDefinition"/> backing this grammar.</summary>
    public ParserDefinition Definition { get; }

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
    /// <see cref="ParserDefinition"/> with an explicit runtime feature policy.
    /// </summary>
    /// <param name="definition">A resolved grammar definition.</param>
    /// <param name="runtimeFeaturePolicy">Runtime feature policy applied to parser execution.</param>
    public CompiledGrammar(ParserDefinition definition, ParserRuntimeFeaturePolicy runtimeFeaturePolicy)
    {
        Definition = definition;
        _lexer = new LexerEngine(definition);
        _parser = new ParserEngine(definition, runtimeFeaturePolicy);
    }

    /// <summary>
    /// Tokenizes <paramref name="input"/> and returns all tokens produced by the lexer,
    /// in source order. Skipped tokens (e.g. whitespace with <c>-&gt; skip</c>) are already
    /// excluded from the stream at this stage.
    /// </summary>
    /// <param name="input">Source text to tokenize.</param>
    /// <returns>Read-only list of tokens.</returns>
    public IReadOnlyList<Token> Tokenize(string input)
    {
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
    public ParseNode Parse(string input)
    {
        var stream = new StringReader(input);
        var tokens = _lexer.Tokenize(stream).ToList();
        return _parser.Parse(tokens);
    }
}
