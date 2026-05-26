namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Represents ANTLR4 grammar options declared in an <c>options { ... }</c> prequel block.
/// </summary>
/// <param name="Values">Option key/value pairs using ANTLR identifier keys.</param>
public sealed record Antlr4OptionSet(IReadOnlyDictionary<string, string> Values);
