namespace Utils.Parser.Runtime;

/// <summary>
/// Converts values produced by <see cref="ParserSimpleLiteralParser"/> to a deliberately limited set of built-in declared types.
/// </summary>
public static class ParserLiteralTypeConverter
{
    /// <summary>Converts one supported simple-literal value to the requested built-in target type.</summary>
    public static ParserLiteralConversionResult Convert(object? literalValue, string rawDeclaredType)
    {
        var result = Utils.Parser.Antlr4.Common.ParserLiteralTypeConverter.Convert(literalValue, rawDeclaredType);
        return new ParserLiteralConversionResult { Success = result.Success, Value = result.Value, Error = result.Error };
    }
}
