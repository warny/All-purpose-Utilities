using System.IO;
using System.Text;

namespace Utils.Parser.Runtime;

/// <summary>
/// Internal forward-only character buffer over a <see cref="TextReader"/>.
/// </summary>
internal sealed class TextReaderBuffer(TextReader reader) : TextReaderLookahead
{
    /// <summary>Characters read ahead from the underlying reader.</summary>
    private readonly List<char> _buffer = [];
    /// <summary>Index into <see cref="_buffer"/> of the current lookahead window start.</summary>
    private int _startIndex;
    /// <summary>Absolute character position in the source stream.</summary>
    private int _position;
    /// <summary>Current one-based line number.</summary>
    private int _line = 1;
    /// <summary>Current one-based column number.</summary>
    private int _column = 1;
    /// <summary><see langword="true"/> when the underlying reader has been exhausted.</summary>
    private bool _isEnd;
    /// <summary><see langword="true"/> when the last consumed character was a carriage return, used to suppress the extra line increment for CRLF sequences.</summary>
    private bool _previousWasCarriageReturn;

    /// <inheritdoc />
    public int Position => _position;

    /// <inheritdoc />
    public int Line => _line;

    /// <inheritdoc />
    public int Column => _column;

    /// <inheritdoc />
    public bool IsEnd => Peek(0) < 0;

    /// <inheritdoc />
    public int Peek(int offset)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Lookahead offset cannot be negative.");
        }

        EnsureBuffered(offset + 1);
        int index = _startIndex + offset;
        if (index >= _buffer.Count)
        {
            return -1;
        }

        return _buffer[index];
    }

    /// <summary>
    /// Consumes a single character.
    /// </summary>
    public void Consume() => Consume(1);

    /// <summary>
    /// Consumes the specified number of characters.
    /// </summary>
    /// <param name="count">Number of characters to consume.</param>
    public void Consume(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Consume count cannot be negative.");
        }

        for (int i = 0; i < count; i++)
        {
            int next = Peek(0);
            if (next < 0)
            {
                throw new InvalidOperationException("Cannot consume past end of input.");
            }

            _startIndex++;
            _position++;
            UpdateLineAndColumn((char)next);
        }

        CompactBufferIfNeeded();
    }

    /// <summary>
    /// Reads text from the current lookahead window.
    /// </summary>
    /// <param name="startPosition">Absolute start position expected to be current position.</param>
    /// <param name="length">Length to read.</param>
    /// <returns>Text from lookahead buffer.</returns>
    public string GetText(int startPosition, int length)
    {
        if (startPosition != _position)
        {
            throw new InvalidOperationException("Only forward-only extraction from current position is supported.");
        }

        if (length <= 0)
        {
            return string.Empty;
        }

        EnsureBuffered(length);
        if (_startIndex + length > _buffer.Count)
        {
            throw new InvalidOperationException("Unable to read requested text length from forward-only buffer.");
        }

        var builder = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            builder.Append(_buffer[_startIndex + i]);
        }

        return builder.ToString();
    }

    /// <summary>Reads from the underlying reader until at least <paramref name="count"/> characters are available in the buffer.</summary>
    private void EnsureBuffered(int count)
    {
        while (!_isEnd && _buffer.Count - _startIndex < count)
        {
            int read = reader.Read();
            if (read < 0)
            {
                _isEnd = true;
                break;
            }

            _buffer.Add((char)read);
        }
    }

    /// <summary>Advances line and column tracking after consuming <paramref name="value"/>.</summary>
    private void UpdateLineAndColumn(char value)
    {
        if (value == '\r')
        {
            _line++;
            _column = 1;
            _previousWasCarriageReturn = true;
            return;
        }

        if (value == '\n')
        {
            if (!_previousWasCarriageReturn)
            {
                _line++;
            }

            _column = 1;
            _previousWasCarriageReturn = false;
            return;
        }

        _column++;
        _previousWasCarriageReturn = false;
    }

    /// <summary>Removes already-consumed characters from the front of the buffer when the consumed prefix is large enough.</summary>
    private void CompactBufferIfNeeded()
    {
        if (_startIndex == 0)
        {
            return;
        }

        if (_startIndex > 1024 || _startIndex * 2 > _buffer.Count)
        {
            _buffer.RemoveRange(0, _startIndex);
            _startIndex = 0;
        }
    }
}
