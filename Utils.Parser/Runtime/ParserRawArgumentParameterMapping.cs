using System;

namespace Utils.Parser.Runtime;

/// <summary>
/// Describes a single positional mapping from a raw call-site argument slice to a named child-rule parameter seed.
/// Used with generated <c>SetNextRuleParametersFromRawArguments</c> helpers.
/// </summary>
/// <remarks>
/// Mapping is explicit and requires a caller-supplied delegate. No argument is evaluated automatically.
/// No parameter is bound without an explicit call to the generated helper.
/// </remarks>
public readonly record struct ParserRawArgumentParameterMapping
{
    /// <summary>
    /// Gets the child-rule parameter metadata name that will receive the seeded value.
    /// Must not be <c>null</c>.
    /// </summary>
    public required string ParameterName { get; init; }

    /// <summary>
    /// Gets the zero-based index of the argument slice within the split raw-argument list.
    /// When the index is out of range, no seed is set for this mapping and the helper returns <c>false</c>.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Gets the caller-supplied delegate that converts the raw argument text to an untyped seed value.
    /// Must not be <c>null</c>. Exceptions from the delegate propagate to the caller.
    /// </summary>
    public required Func<string, object?> Map { get; init; }
}
