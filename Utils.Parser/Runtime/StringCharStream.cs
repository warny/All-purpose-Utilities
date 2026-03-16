namespace Utils.Parser.Runtime;

/// <summary>
/// An <see cref="ICharStream"/> implementation backed by an in-memory <see cref="string"/>.
/// All operations are O(1) because the entire source text is already in memory.
/// </summary>
public sealed class StringCharStream(string source) : ICharStream
{
    private int _position;

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public bool IsEnd => _position >= source.Length;

    /// <inheritdoc/>
    /// <remarks>Returns <c>'\0'</c> when <c>Position + offset</c> is past the end of the string.</remarks>
    public char Peek(int offset = 0) =>
        _position + offset < source.Length ? source[_position + offset] : '\0';

    /// <inheritdoc/>
    public void Consume(int count = 1) => _position += count;

    /// <inheritdoc/>
    public int SavePosition() => _position;

    /// <inheritdoc/>
    public void RestorePosition(int savedPosition) => _position = savedPosition;
}
