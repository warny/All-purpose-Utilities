namespace Utils.Expressions;

/// <summary>
/// Describes a set of parenthesis tokens used by the expression parser.
/// </summary>
/// <param name="Start">Token that opens the group.</param>
/// <param name="End">Token that closes the group.</param>
/// <param name="Separator">Optional separator token used between arguments.</param>
public record Parenthesis(string Start, string End, string Separator = null)
{
    /// <summary>
    /// Tests whether a token represents the end or separator of the parenthesis set.
    /// </summary>
    /// <param name="token">Token to test.</param>
    /// <param name="isEnd">Set to <see langword="true"/> if the token is the closing token.</param>
    /// <returns><see langword="true"/> if the token matches one of the markers.</returns>
    public bool Test(string token, out bool isEnd)
    {
        isEnd = false;
        if (token == End) { isEnd = true; return true; }
        if (token == Separator) return true;
        return false;
    }
    /// <summary>
    /// Tests a token while optionally ignoring the separator.
    /// </summary>
    /// <param name="token">Token to test.</param>
    /// <param name="ignoreSeparator">When true, separator tokens are ignored.</param>
    /// <param name="isEnd">Set to <see langword="true"/> if the token is the closing token.</param>
    /// <returns><see langword="true"/> if the token matches.</returns>
    public bool Test(string token, bool ignoreSeparator, out bool isEnd)
    {
        isEnd = false;
        if (token == End) { isEnd = true; return true; }
        if (ignoreSeparator || token == Separator) return true;
        return false;
    }

    /// <summary>
    /// Creates a <see cref="Parenthesis"/> from a tuple of start and end tokens.
    /// </summary>
    public static implicit operator Parenthesis((string Start, string End) value) => new(value.Start, value.End);

    /// <summary>
    /// Creates a <see cref="Parenthesis"/> from a tuple including a separator.
    /// </summary>
    public static implicit operator Parenthesis((string Start, string End, string Separator) value) => new(value.Start, value.End, value.Separator);
}

