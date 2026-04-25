using System.IO;
using System.Text;

namespace Utils.Parser.Runtime;

/// <summary>
/// Internal forward-only character buffer over a <see cref="TextReader"/>.
/// </summary>
internal sealed class TextReaderBuffer(TextReader reader) : TextReaderLookahead
{
    private readonly Queue<char> _lookahead = [];
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private bool _isEnd;

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
        if (_lookahead.Count <= offset)
        {
            return -1;
        }

        return _lookahead.ElementAt(offset);
    }

    /// <summary>
    /// Consumes a single character.
    /// </summary>
    public void Consume()
    {
        Consume(1);
    }

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

            _lookahead.Dequeue();
            _position++;

            if (next == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
        }
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
        if (_lookahead.Count < length)
        {
            throw new InvalidOperationException("Unable to read requested text length from forward-only buffer.");
        }

        var builder = new StringBuilder(length);
        int index = 0;
        foreach (char value in _lookahead)
        {
            if (index >= length)
            {
                break;
            }

            builder.Append(value);
            index++;
        }

        return builder.ToString();
    }

    private void EnsureBuffered(int count)
    {
        while (!_isEnd && _lookahead.Count < count)
        {
            int read = reader.Read();
            if (read < 0)
            {
                _isEnd = true;
                break;
            }

            _lookahead.Enqueue((char)read);
        }
    }
}
