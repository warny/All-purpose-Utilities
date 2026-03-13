namespace Utils.Parser.Runtime;

public sealed class StringCharStream(string source) : ICharStream
{
    private int _position;

    public int Position => _position;
    public bool IsEnd => _position >= source.Length;

    public char Peek(int offset = 0) =>
        _position + offset < source.Length ? source[_position + offset] : '\0';

    public void Consume(int count = 1) => _position += count;

    public int SavePosition() => _position;
    public void RestorePosition(int savedPosition) => _position = savedPosition;
}
