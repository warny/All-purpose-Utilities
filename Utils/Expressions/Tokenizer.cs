global using System;
global using System.Collections.Generic;
global using System.Linq;
global using Utils.Collections;
using Utils.Parser.Runtime;

namespace Utils.Expressions;

/// <summary>
/// Represents a tokenizer that parses and reads tokens from a given string content,
/// based on configurable symbols, whitespace definitions, and token readers.
/// </summary>
public class Tokenizer
{
    private readonly TokenizerRuntime _runtime;

    /// <summary>
    /// Gets the full content string being tokenized.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Gets the <see cref="SymbolTree"/> that defines recognized symbols
    /// within the content.
    /// </summary>
    public SymbolTree Symbols { get; }

    /// <summary>
    /// Gets the collection of characters considered to be whitespace.
    /// </summary>
    public char[] WhiteSpaces { get; }

    /// <summary>
    /// Gets an enumeration of functions that attempt to read tokens from content.
    /// </summary>
    public IEnumerable<TryReadToken> TokenReaders { get; }

    /// <summary>
    /// Gets an enumeration of transformers that can modify string tokens once read.
    /// </summary>
    public IEnumerable<StringTransformer> StringTransformers { get; }

    /// <summary>
    /// Gets the current tokenizer position (read-only).
    /// </summary>
    public ITokenizerPosition Position => new TokenizerPosition(_runtime.Position.Index, _runtime.Position.Length);

    /// <summary>
    /// Gets a string result defined during the most recent call to <see cref="ReadToken(bool)"/>.
    /// </summary>
    public string DefineString => _runtime.DefineString;

    /// <summary>
    /// Initializes a new instance of the <see cref="Tokenizer"/> class with the specified
    /// content and builder.
    /// </summary>
    /// <param name="content">The string content to tokenize.</param>
    /// <param name="builder">A builder interface providing symbols, whitespace, and token readers.</param>
    public Tokenizer(string content, IBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(builder);

        Content = content;
        Symbols = [.. builder.Symbols];
        WhiteSpaces = builder.SpaceSymbols;
        TokenReaders = builder.TokenReaders;
        StringTransformers = builder.StringTransformers;
        _runtime = new TokenizerRuntime(
            content,
            WhiteSpaces,
            builder.Symbols,
            TokenReaders.Select(reader => new RuntimeTryReadToken(reader)),
            StringTransformers.Select(transformer => new RuntimeStringTransformer(transformer)));
    }

    /// <summary>
    /// Reads the next token from the content.
    /// </summary>
    /// <param name="isIgnoreWhiteSpace">If <see langword="true"/>, any whitespace characters are skipped prior to reading.</param>
    /// <returns>
    /// The token string if read successfully; otherwise <see langword="null"/> if no more tokens can be read.
    /// </returns>
    public string? ReadToken(bool isIgnoreWhiteSpace = true)
    {
        try
        {
            return _runtime.ReadToken(isIgnoreWhiteSpace);
        }
        catch (TokenizerRuntimeException ex)
        {
            throw new ParseUnknownException(ex.Token, ex.Index);
        }
    }

    /// <summary>
    /// Attempts to read a specific symbol from the current position.
    /// </summary>
    /// <param name="symbol">The symbol string to match.</param>
    /// <param name="throwExceptionIfError">
    /// If <see langword="true"/>, throws an exception if the symbol does not match.
    /// Otherwise, returns <see langword="false"/>.
    /// </param>
    /// <returns><see langword="true"/> if the symbol was matched; otherwise <see langword="false"/>.</returns>
    public bool ReadSymbol(string symbol, bool throwExceptionIfError = true)
    {
        try
        {
            return _runtime.ReadSymbol(symbol, throwExceptionIfError);
        }
        catch (TokenizerRuntimeException ex)
        {
            if (!throwExceptionIfError)
            {
                return false;
            }

            ParseException.Assert(ex.Token, symbol, ex.Index);
            return false;
        }
    }

    /// <summary>
    /// Peeks the next token without advancing the tokenizer's position.
    /// </summary>
    /// <returns>The next token, or <see langword="null"/> if no token is available.</returns>
    public string? PeekToken()
    {
        try
        {
            return _runtime.PeekToken();
        }
        catch (TokenizerRuntimeException ex)
        {
            throw new ParseUnknownException(ex.Token, ex.Index);
        }
    }

    /// <summary>
    /// Pushes the current tokenizer state onto an internal stack.
    /// </summary>
    public void PushPosition()
    {
        _runtime.PushPosition();
    }

    /// <summary>
    /// Pops a previously saved tokenizer state and restores it.
    /// </summary>
    public void PopPosition()
    {
        _runtime.PopPosition();
    }

    /// <summary>
    /// Discards the most recently saved tokenizer state.
    /// </summary>
    public void DiscardPosition()
    {
        _runtime.DiscardPosition();
    }

    /// <summary>
    /// Resets the tokenizer position to the beginning of the content.
    /// </summary>
    public void ResetPosition()
    {
        _runtime.Reset();
    }

    /// <summary>
    /// Represents the tokenizer's internal position.
    /// </summary>
    private class TokenizerPosition(int index, int length) : ITokenizerPosition
    {
        /// <summary>
        /// Gets the current index within the content.
        /// </summary>
        public int Index { get; } = index;

        /// <summary>
        /// Gets the length of the current token.
        /// </summary>
        public int Length { get; } = length;

        /// <inheritdoc/>
        public override string ToString() => $"Index: {Index}, Length: {Length}";
    }
}

/// <summary>
/// Exposes read-only properties representing the tokenizer's position within the content.
/// </summary>
public interface ITokenizerPosition
{
    /// <summary>
    /// Gets the current index in the content.
    /// </summary>
    int Index { get; }

    /// <summary>
    /// Gets the current token length.
    /// </summary>
    int Length { get; }
}
