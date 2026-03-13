namespace Utils.Parser.Runtime;

/// <summary>
/// Position absolue dans le fichier source — ligne/colonne calculées à la demande
/// </summary>
public record SourceSpan(int Position, int Length)
{
    public (int Line, int Column) ToLineColumn(string source)
    {
        int line = 1, col = 1;
        for (int i = 0; i < Position && i < source.Length; i++)
        {
            if (source[i] == '\n') { line++; col = 1; }
            else col++;
        }
        return (line, col);
    }
}

public record Token(
    SourceSpan Span,
    string RuleName,
    string ModeName,
    string Text
);
