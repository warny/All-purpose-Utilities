using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace Utils.Objects;

/// <summary>
/// Provides parsing utilities based on interpolated string handlers.
/// </summary>
public static class InterpolatedParser
{
    /// <summary>
    /// Parses <paramref name="input"/> according to an interpolated string pattern.
    /// The pattern determines the expected literals and the types of formatted values.
    /// </summary>
    /// <param name="input">String to parse.</param>
    /// <param name="handler">Handler generated from the interpolated string.</param>
    /// <param name="values">Parsed values if the method succeeds.</param>
    /// <returns><c>true</c> if parsing succeeded, otherwise <c>false</c>.</returns>
    public static bool TryParse(string input,
            [InterpolatedStringHandlerArgument("input")] ref ParseInterpolatedStringHandler handler,
            out object[] values)
    {
        values = handler.GetValues();
        return handler.Success;
    }

    /// <summary>
    /// Parses <paramref name="input"/> according to an interpolated string and
    /// tries to create an instance of <typeparamref name="T"/> using the parsed
    /// values.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="input">String to parse.</param>
    /// <param name="handler">Handler generated from the interpolated string.</param>
    /// <param name="result">The parsed object when successful.</param>
    /// <returns><c>true</c> if parsing succeeded and the object could be created.</returns>
    public static bool TryParse<T>(string input,
            [InterpolatedStringHandlerArgument("input")] ref ParseInterpolatedStringHandler handler,
            out T result)
    {
        if (TryParse(input, ref handler, out var values) && TryCreate(values, out result))
        {
            return true;
        }

        result = default!;
        return false;
    }

    private static bool TryCreate<T>(object[] values, out T result)
    {
        foreach (ConstructorInfo ctor in typeof(T).GetConstructors())
        {
            ParameterInfo[] parameters = ctor.GetParameters();
            if (parameters.Length != values.Length) continue;

            object[] args = new object[values.Length];
            bool ok = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                object value = values[i];
                Type targetType = parameters[i].ParameterType;

                if (value is null)
                {
                    args[i] = null!;
                    continue;
                }

                if (targetType.IsAssignableFrom(value.GetType()))
                {
                    args[i] = value;
                    continue;
                }

                try
                {
                    if (value is string s)
                    {
                        args[i] = Parsers.Parse(s, targetType);
                    }
                    else
                    {
                        args[i] = Convert.ChangeType(value, targetType);
                    }
                }
                catch
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                result = (T)ctor.Invoke(args);
                return true;
            }
        }

        result = default!;
        return false;
    }
}

/// <summary>
/// Interpolated string handler that parses an input string and extracts values
/// based on the interpolation pattern.
/// </summary>
[InterpolatedStringHandler]
public ref struct ParseInterpolatedStringHandler
{
    private readonly string _input;
    private int _position;
    private readonly List<object> _values;
    private readonly Queue<Type> _pendingTypes;
    private bool _success;
    private bool _finalized;

    /// <summary>
    /// Initializes a new handler for parsing.
    /// </summary>
    /// <param name="literalLength">Total length of literal segments.</param>
    /// <param name="formattedCount">Number of formatted segments.</param>
    /// <param name="input">The input string to parse.</param>
    public ParseInterpolatedStringHandler(int literalLength, int formattedCount, string input)
    {
        _input = input ?? string.Empty;
        _position = 0;
        _values = new List<object>(formattedCount);
        _pendingTypes = new Queue<Type>(formattedCount);
        _success = true;
        _finalized = false;
    }

    /// <summary>
    /// Indicates whether the parsing succeeded.
    /// </summary>
    public bool Success
    {
        get
        {
            EnsureFinalized();
            return _success && _position == _input.Length;
        }
    }

    /// <summary>
    /// Retrieves the parsed values.
    /// </summary>
    /// <returns>An array of the parsed values.</returns>
    public object[] GetValues()
    {
        EnsureFinalized();
        return [.. _values];
    }

    /// <summary>
    /// Processes a literal segment.
    /// </summary>
    /// <param name="literal">Literal text from the interpolated string.</param>
    public void AppendLiteral(string literal)
    {
        if (!_success) return;
        ParsePending(literal);
        if (!_success) return;
        if (!_input.AsSpan(_position).StartsWith(literal, StringComparison.Ordinal))
        {
            _success = false;
            return;
        }
        _position += literal.Length;
    }

    /// <summary>
    /// Registers a formatted value to be parsed.
    /// </summary>
    /// <typeparam name="T">Type of the value to parse.</typeparam>
    /// <param name="_">Placeholder value (ignored).</param>
    public void AppendFormatted<T>(T _)
    {
        if (!_success) return;
        _pendingTypes.Enqueue(typeof(T));
    }

    private void EnsureFinalized()
    {
        if (_finalized) return;
        ParsePending(string.Empty);
        _finalized = true;
    }

    private void ParsePending(string nextLiteral)
    {
        if (_pendingTypes.Count == 0) return;
        var type = _pendingTypes.Dequeue();
        int index = nextLiteral.Length == 0 ? _input.Length : _input.IndexOf(nextLiteral, _position, StringComparison.Ordinal);
        if (index < 0)
        {
            _success = false;
            return;
        }
        string segment = _input.Substring(_position, index - _position);
        try
        {
            _values.Add(Parsers.Parse(segment, type));
        }
        catch
        {
            _success = false;
            return;
        }
        _position = index;
    }
}
