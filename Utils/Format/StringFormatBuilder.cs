using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Utils.Expressions;

namespace Utils.Format;

/// <summary>
/// Builds string-format delegates while validating embedded expressions with an injected compiler.
/// </summary>
public sealed class StringFormatBuilder : IStringFormatBuilder
{
    private readonly IExpressionCompiler _compiler;

    /// <summary>
    /// Initializes a new instance of <see cref="StringFormatBuilder"/>.
    /// </summary>
    /// <param name="compiler">Compiler used to validate embedded expressions.</param>
    public StringFormatBuilder(IExpressionCompiler compiler)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    /// <inheritdoc />
    public T Create<T>(string formatString, params string[] names) where T : Delegate
        => Create<T>(formatString, null, null, names);

    /// <inheritdoc />
    public T Create<T>(string formatString, ICustomFormatter? customFormatter, CultureInfo? cultureInfo, params string[] names) where T : Delegate
    {
        ParameterExpression[] parameters = GetDelegateParameters<T>(names);
        ValidateExpressions(formatString, parameters);
        return StringFormat.Create<T>(formatString, customFormatter, cultureInfo, names);
    }

    /// <summary>
    /// Builds parameter expressions from the target delegate signature.
    /// </summary>
    /// <typeparam name="T">Delegate type.</typeparam>
    /// <param name="names">Optional parameter names.</param>
    /// <returns>Parameter expressions matching the delegate signature.</returns>
    private static ParameterExpression[] GetDelegateParameters<T>(string[] names) where T : Delegate
    {
        var delegateParameters = typeof(T).GetMethod("Invoke")?.GetParameters() ?? [];
        if (names.Length != 0 && names.Length != delegateParameters.Length)
        {
            throw new ArgumentException("Invalid number of names", nameof(names));
        }

        return delegateParameters
            .Select((p, i) => Expression.Parameter(p.ParameterType, names.Length > 0 ? names[i] : p.Name ?? $"arg{i}"))
            .ToArray();
    }

    /// <summary>
    /// Validates each formatted expression part using the injected compiler.
    /// </summary>
    /// <param name="formatString">Format string to validate.</param>
    /// <param name="parameters">Available parameter symbols.</param>
    private void ValidateExpressions(string formatString, ParameterExpression[] parameters)
    {
        var parser = new InterpolatedStringParser(formatString);
        var symbols = parameters.ToDictionary(p => p.Name ?? string.Empty, static p => (Expression)p, StringComparer.Ordinal);

        foreach (FormattedPart formattedPart in parser.OfType<FormattedPart>())
        {
            _compiler.Compile(formattedPart.ExpressionText, symbols);
        }
    }
}
