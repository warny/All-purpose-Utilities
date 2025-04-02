global using System;
global using System.Collections.Generic;
global using System.Linq;
global using Utils.Collections;

namespace Utils.Expressions;

/// <summary>
/// Represents a tokenizer that parses and reads tokens from a given string content,
/// based on configurable symbols, whitespace definitions, and token readers.
/// </summary>
public class Tokenizer
{
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
	public ITokenizerPosition Position => _position;

	/// <summary>
	/// Gets a string result defined during the most recent call to <see cref="ReadToken(bool)"/>,
	/// if any transformer modified the token content.
	/// </summary>
	public string DefineString { get; private set; }

	private TokenizerPosition _position;
	private readonly Stack<TokenizerPosition> _savedPositions = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="Tokenizer"/> class with the specified
	/// content and builder.
	/// </summary>
	/// <param name="content">The string content to tokenize.</param>
	/// <param name="builder">A builder interface providing symbols, whitespace, and token readers.</param>
	public Tokenizer(string content, IBuilder builder)
	{
		ResetPosition();
		Symbols = [.. builder.Symbols];
		WhiteSpaces = builder.SpaceSymbols;
		TokenReaders = builder.TokenReaders;
		StringTransformers = builder.StringTransformers;
		Content = content;
	}

	/// <summary>
	/// Reads the next token from the content.
	/// </summary>
	/// <param name="isIgnoreWhiteSpace">If <see langword="true"/>, any whitespace characters are skipped prior to reading.</param>
	/// <returns>
	/// The token string if read successfully; otherwise <see langword="null"/> if no more tokens can be read.
	/// </returns>
	public string ReadToken(bool isIgnoreWhiteSpace = true)
	{
		if (Read(true, isIgnoreWhiteSpace))
		{
			var token = Content.Substring(Position.Index, Position.Length);
			foreach (var stringTransformer in StringTransformers)
			{
				if (stringTransformer(token, out var result))
				{
					DefineString = result;
					break;
				}
			}
			return token;
		}

		return null;
	}

	/// <summary>
	/// Attempts to read a specific symbol from the current position. If successful,
	/// advances the position past the symbol.
	/// </summary>
	/// <param name="symbol">The symbol string to match.</param>
	/// <param name="throwExceptionIfError">
	/// If <see langword="true"/>, throws an exception if the symbol does not match.
	/// Otherwise, returns <see langword="false"/> on a mismatch.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the symbol was matched; otherwise <see langword="false"/>.
	/// </returns>
	public bool ReadSymbol(string symbol, bool throwExceptionIfError = true)
	{
		if (_position.Index + _position.Length >= Content.Length)
		{
			return false;
		}

		// Skip whitespace characters
		while (char.IsWhiteSpace(Content[_position.Index + _position.Length]))
		{
			_position.Length++;
		}

		// Check if we have enough space left to match the symbol
		if (throwExceptionIfError)
		{
			ParseException.Assert(
				Content.Substring(_position.Index + _position.Length, symbol.Length),
				symbol,
				_position.Index);
		}
		else if (_position.Index + _position.Length + symbol.Length >= Content.Length)
		{
			return false;
		}
		else if (Content.Substring(_position.Index + _position.Length, symbol.Length) != symbol)
		{
			return false;
		}

		// Move the reading position to the end of the matched symbol
		_position.Index += _position.Length;
		_position.Length = symbol.Length;
		return true;
	}

	/// <summary>
	/// Peeks the next token without advancing the tokenizer's position.
	/// </summary>
	/// <returns>The next token, or <see langword="null"/> if no token is available.</returns>
	public string PeekToken()
	{
		PushPosition();
		string token = ReadToken(true);
		PopPosition();
		return token;
	}

	/// <summary>
	/// Pushes the current tokenizer position onto an internal stack, allowing you
	/// to restore this position later via <see cref="PopPosition()"/>.
	/// </summary>
	public void PushPosition()
	{
		_savedPositions.Push(new TokenizerPosition(_position.Index, _position.Length));
	}

	/// <summary>
	/// Pops a previously saved tokenizer position from the internal stack
	/// and restores it as the current position.
	/// </summary>
	public void PopPosition()
	{
		var restoredPosition = _savedPositions.Pop();
		_position = restoredPosition;
	}

	/// <summary>
	/// Discards the most recently saved position by popping it from the
	/// internal stack, but does not restore it to the current position.
	/// </summary>
	public void DiscardPosition()
	{
		_savedPositions.Pop();
	}

	/// <summary>
	/// Resets the tokenizer position to the beginning of the content,
	/// and clears any saved positions.
	/// </summary>
	public void ResetPosition()
	{
		_savedPositions.Clear();
		_position = new TokenizerPosition(0, 0);
	}

	/// <summary>
	/// Performs the actual read logic by advancing and analyzing characters from the content.
	/// </summary>
	/// <param name="isBuildDefineString">Indicates whether to set <see cref="DefineString"/> if applicable.</param>
	/// <param name="isIgnoreWhiteSpace">If <see langword="true"/>, skips whitespace before reading.</param>
	/// <returns>
	/// <see langword="true"/> if a token is successfully read; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ParseUnknownException">
	/// Thrown when an unrecognized symbol is encountered and cannot be parsed.
	/// </exception>
	private bool Read(bool isBuildDefineString, bool isIgnoreWhiteSpace)
	{
		// Advance the cursor and reset the length to 1
		_position.Index += _position.Length;
		_position.Length = 1;

		// If the cursor has reached the end, reset to the beginning and return false
		if (_position.Index == Content.Length)
		{
			_position.Index = 0;
			return false;
		}

		// Skip whitespace if requested
		if (isIgnoreWhiteSpace && WhiteSpaces.Contains(Content[_position.Index]))
		{
			return Read(isBuildDefineString, isIgnoreWhiteSpace);
		}

		// Attempt to read via each custom token reader
		char currentChar = Content[_position.Index];
		foreach (var tokenReader in TokenReaders)
		{
			if (tokenReader(Content, _position.Index, out var tokenLength))
			{
				_position.Length = tokenLength;
				return true;
			}
		}

		// Attempt to read via symbol definitions
		if (!TryGetNextChar(false, out var nextInner))
		{
			// Reached the end of content
			return true;
		}

		if (Symbols.TryGetValue(currentChar, out SymbolLeaf leaf))
		{
			SymbolLeaf currentLeaf = leaf;
			while (!leaf.IsFinal)
			{
				if (!TryGetNextChar(false, out nextInner)) break;
				if (!leaf.TryFindNext(nextInner, out leaf)) break;
				if (leaf.Value is not null) currentLeaf = leaf;
			}

			if (currentLeaf.Value is not null)
			{
				_position.Length = currentLeaf.Value.Length;
				return true;
			}
		}

		throw new ParseUnknownException(currentChar.ToString(), _position.Index);
	}

	/// <summary>
	/// Attempts to get the next character in the content, optionally ignoring whitespace.
	/// </summary>
	/// <param name="ignoreWhiteSpace">If <see langword="true"/>, skips whitespace.</param>
	/// <param name="cNext">
	/// When this method returns, contains the next character, if one exists.
	/// </param>
	/// <returns><see langword="true"/> if another character was found; otherwise <see langword="false"/>.</returns>
	private bool TryGetNextChar(bool ignoreWhiteSpace, out char cNext)
	{
		cNext = '\0';
		for (int i = 0; i < int.MaxValue; i++)
		{
			if (_position.Index + _position.Length + i >= Content.Length)
			{
				return false;
			}

			cNext = Content[_position.Index + _position.Length];
			if (!ignoreWhiteSpace || !char.IsWhiteSpace(cNext))
			{
				break;
			}
		}
		return true;
	}

	/// <summary>
	/// Represents the tokenizer's internal position, including
	/// an index into the <see cref="Tokenizer.Content"/> and
	/// the current token length.
	/// </summary>
	private class TokenizerPosition : ITokenizerPosition
	{
		public TokenizerPosition(int index, int length)
		{
			Index = index;
			Length = length;
		}

		/// <summary>
		/// Gets or sets the current index within the content.
		/// </summary>
		public int Index { get; set; }

		/// <summary>
		/// Gets or sets the length of the current token.
		/// </summary>
		public int Length { get; set; }

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
