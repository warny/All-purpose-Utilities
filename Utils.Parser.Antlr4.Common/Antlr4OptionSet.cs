using System.Collections.Immutable;

namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Represents ANTLR4 grammar options declared in an <c>options { ... }</c> prequel block.
/// </summary>
/// <param name="Values">Option key/value pairs using ordinal ANTLR identifier keys.</param>
public sealed record Antlr4OptionSet(IReadOnlyDictionary<string, string> Values)
{
    private IReadOnlyDictionary<string, string> _values = Values.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>Gets an immutable ordinal snapshot of option key/value pairs.</summary>
    public IReadOnlyDictionary<string, string> Values
    {
        get => _values;
        init => _values = value.ToImmutableDictionary(StringComparer.Ordinal);
    }
}
