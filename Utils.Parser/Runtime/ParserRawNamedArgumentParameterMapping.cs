using System;

namespace Utils.Parser.Runtime;

/// <summary>
/// Describes a single named mapping from a raw call-site argument entry to a child-rule parameter seed.
/// Used with generated <c>SetNextRuleParametersFromNamedRawArguments</c> helpers.
/// </summary>
/// <remarks>
/// Mapping is explicit and requires a caller-supplied delegate. No argument is evaluated automatically.
/// No parameter is bound without an explicit call to the generated helper.
/// </remarks>
public readonly record struct ParserRawNamedArgumentParameterMapping
{
    /// <summary>
    /// Gets the child-rule parameter metadata name that will receive the seeded value.
    /// Must not be <c>null</c>.
    /// </summary>
    public required string ParameterName { get; init; }

    /// <summary>
    /// Gets the name of the raw argument entry to look up in the named argument dictionary.
    /// Must not be <c>null</c>. When absent from the dictionary, no seed is set and the helper returns <c>false</c>.
    /// </summary>
    public required string ArgumentName { get; init; }

    /// <summary>
    /// Gets the caller-supplied delegate that converts the raw argument text to an untyped seed value.
    /// Must not be <c>null</c>. Exceptions from the delegate propagate to the caller.
    /// </summary>
    public required Func<string, object?> Map { get; init; }
}
