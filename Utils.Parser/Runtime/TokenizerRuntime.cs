namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a tokenization error raised by <see cref="TokenizerRuntime"/>.
/// </summary>
/// <param name="token">Token or character that caused the error.</param>
/// <param name="index">Position where the error occurred.</param>
public sealed class TokenizerRuntimeException(string token, int index) : Exception($"Unknown token '{token}' at index {index}.")
{
    /// <summary>
    /// Gets the token or character that could not be recognized.
    /// </summary>
    public string Token { get; } = token;

    /// <summary>
    /// Gets the index where tokenization failed.
    /// </summary>
    public int Index { get; } = index;
}

/// <summary>
/// Represents a function that attempts to read a token at a specific position.
/// </summary>
/// <param name="content">Source content being tokenized.</param>
/// <param name="index">Current position in <paramref name="content"/>.</param>
/// <param name="length">Returned token length when successful.</param>
/// <returns><see langword="true"/> when a token was read; otherwise <see langword="false"/>.</returns>
public delegate bool RuntimeTryReadToken(string content, int index, out int length);

/// <summary>
/// Represents a function that attempts to transform a token into a defined string value.
/// </summary>
/// <param name="token">Token text to transform.</param>
/// <param name="result">Transformed string result when successful.</param>
/// <returns><see langword="true"/> when transform succeeded; otherwise <see langword="false"/>.</returns>
public delegate bool RuntimeStringTransformer(string token, out string result);

/// <summary>
/// Represents a read-only tokenizer position.
/// </summary>
/// <param name="Index">Current token start index.</param>
/// <param name="Length">Current token length.</param>
public readonly record struct RuntimeTokenizerPosition(int Index, int Length);

/// <summary>
/// Provides reusable tokenization mechanics for parser-oriented consumers.
/// </summary>
public sealed class TokenizerRuntime
{
    private readonly string _content;
    private readonly HashSet<char> _whiteSpaces;
    private readonly IReadOnlyList<string> _symbols;
    private readonly IReadOnlyList<RuntimeTryReadToken> _tokenReaders;
    private readonly IReadOnlyList<RuntimeStringTransformer> _stringTransformers;
    private readonly Stack<RuntimeTokenizerState> _savedStates = new();

    private int _index;
    private int _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenizerRuntime"/> class.
    /// </summary>
    /// <param name="content">Source content to tokenize.</param>
    /// <param name="whiteSpaces">Whitespace characters to skip when requested.</param>
    /// <param name="symbols">Symbols to match when token readers do not consume input.</param>
    /// <param name="tokenReaders">Custom token readers.</param>
    /// <param name="stringTransformers">Custom string transformers.</param>
    public TokenizerRuntime(
        string content,
        IEnumerable<char> whiteSpaces,
        IEnumerable<string> symbols,
        IEnumerable<RuntimeTryReadToken> tokenReaders,
        IEnumerable<RuntimeStringTransformer> stringTransformers)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(whiteSpaces);
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(tokenReaders);
        ArgumentNullException.ThrowIfNull(stringTransformers);

        _content = content;
        _whiteSpaces = [.. whiteSpaces];
        _symbols = symbols
            .Where(symbol => !string.IsNullOrEmpty(symbol))
            .Distinct()
            .OrderByDescending(symbol => symbol.Length)
            .ToList();
        _tokenReaders = tokenReaders.ToList();
        _stringTransformers = stringTransformers.ToList();

        Reset();
    }

    /// <summary>
    /// Gets the current tokenizer position.
    /// </summary>
    public RuntimeTokenizerPosition Position => new(_index, _length);

    /// <summary>
    /// Gets the most recently defined string value.
    /// </summary>
    public string DefineString { get; private set; } = string.Empty;

    /// <summary>
    /// Reads the next token.
    /// </summary>
    /// <param name="ignoreWhiteSpace">When <see langword="true"/>, skips configured whitespace characters.</param>
    /// <returns>The token text, or <see langword="null"/> when end-of-content is reached.</returns>
    public string? ReadToken(bool ignoreWhiteSpace = true)
    {
        if (!Read(ignoreWhiteSpace))
        {
            return null;
        }

        string token = _content.Substring(_index, _length);
        DefineString = string.Empty;
        foreach (RuntimeStringTransformer transformer in _stringTransformers)
        {
            if (transformer(token, out string result))
            {
                DefineString = result;
                break;
            }
        }

        return token;
    }

    /// <summary>
    /// Reads a symbol at the current position.
    /// </summary>
    /// <param name="symbol">Symbol expected at current location.</param>
    /// <param name="throwExceptionIfError">Indicates whether a mismatch throws.</param>
    /// <returns><see langword="true"/> when symbol matched; otherwise <see langword="false"/>.</returns>
    public bool ReadSymbol(string symbol, bool throwExceptionIfError)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        if (_index + _length >= _content.Length)
        {
            return false;
        }

        while (_index + _length < _content.Length && char.IsWhiteSpace(_content[_index + _length]))
        {
            _length++;
        }

        if (_index + _length + symbol.Length > _content.Length)
        {
            if (throwExceptionIfError)
            {
                throw new TokenizerRuntimeException(string.Empty, _index);
            }

            return false;
        }

        string current = _content.Substring(_index + _length, symbol.Length);
        if (!string.Equals(current, symbol, StringComparison.Ordinal))
        {
            if (throwExceptionIfError)
            {
                throw new TokenizerRuntimeException(current, _index);
            }

            return false;
        }

        _index += _length;
        _length = symbol.Length;
        return true;
    }

    /// <summary>
    /// Peeks the next token without consuming it.
    /// </summary>
    /// <returns>The next token, or <see langword="null"/>.</returns>
    public string? PeekToken()
    {
        PushPosition();
        string? token = ReadToken(true);
        PopPosition();
        return token;
    }

    /// <summary>
    /// Saves the current position.
    /// </summary>
    public void PushPosition()
    {
        _savedStates.Push(new RuntimeTokenizerState(_index, _length, DefineString));
    }

    /// <summary>
    /// Restores the last saved position.
    /// </summary>
    public void PopPosition()
    {
        RuntimeTokenizerState state = _savedStates.Pop();
        _index = state.Index;
        _length = state.Length;
        DefineString = state.DefineString;
    }

    /// <summary>
    /// Discards the last saved position.
    /// </summary>
    public void DiscardPosition()
    {
        _savedStates.Pop();
    }

    /// <summary>
    /// Resets tokenizer state.
    /// </summary>
    public void Reset()
    {
        _savedStates.Clear();
        _index = 0;
        _length = 0;
        DefineString = string.Empty;
    }

    private bool Read(bool ignoreWhiteSpace)
    {
        _index += _length;
        _length = 1;

        if (_index >= _content.Length)
        {
            _index = 0;
            return false;
        }

        if (ignoreWhiteSpace)
        {
            while (_index < _content.Length && _whiteSpaces.Contains(_content[_index]))
            {
                _index++;
            }

            if (_index >= _content.Length)
            {
                _index = 0;
                _length = 0;
                return false;
            }
        }

        foreach (RuntimeTryReadToken tokenReader in _tokenReaders)
        {
            if (tokenReader(_content, _index, out int tokenLength))
            {
                _length = tokenLength;
                return true;
            }
        }

        string? symbol = _symbols.FirstOrDefault(current =>
            _index + current.Length <= _content.Length &&
            string.Compare(_content, _index, current, 0, current.Length, StringComparison.Ordinal) == 0);

        if (symbol is not null)
        {
            _length = symbol.Length;
            return true;
        }

        throw new TokenizerRuntimeException(_content[_index].ToString(), _index);
    }

    private readonly record struct RuntimeTokenizerState(int Index, int Length, string DefineString);
}
