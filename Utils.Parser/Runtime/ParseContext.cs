namespace Utils.Parser.Runtime;

internal sealed class ParseContext(IReadOnlyList<Token> tokens)
{
    private int _position;

    public int Position => _position;
    public bool IsEnd => _position >= tokens.Count;

    public Token? Peek(int offset = 0)
    {
        var index = _position + offset;
        return index >= 0 && index < tokens.Count ? tokens[index] : null;
    }

    public Token Consume() => tokens[_position++];

    public int SavePosition() => _position;
    public void RestorePosition(int saved) => _position = saved;
}
