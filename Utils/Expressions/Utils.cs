﻿namespace Utils.Expressions;

public record Parenthesis(string Start, string End, string Separator = null)
{
    public bool Test(string token, out bool isEnd)
    {
        isEnd = false;
        if (token == End) { isEnd = true; return true; }
        if (token == Separator) return true;
        return false;
    }
    public bool Test(string token, bool ignoreSeparator, out bool isEnd)
    {
        isEnd = false;
        if (token == End) { isEnd = true; return true; }
        if (ignoreSeparator || token == Separator) return true;
        return false;
    }

    public static implicit operator Parenthesis((string Start, string End) value) => new(value.Start, value.End);
    public static implicit operator Parenthesis((string Start, string End, string Separator) value) => new (value.Start, value.End, value.Separator);
}

