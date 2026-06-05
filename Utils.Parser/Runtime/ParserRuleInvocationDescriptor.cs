using System.Collections.ObjectModel;
using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Describes passive metadata associated with a parser rule invocation.
/// The descriptor preserves grammar metadata for observation only and does not bind,
/// allocate, propagate, or execute rule-level metadata.
/// </summary>
public sealed class ParserRuleInvocationDescriptor
{
    /// <summary>
    /// Backing store for passive parameter descriptors.
    /// </summary>
    private IReadOnlyList<ParserRuleParameterDescriptor> _parameters = [];

    /// <summary>
    /// Backing store for passive return descriptors.
    /// </summary>
    private IReadOnlyList<ParserRuleReturnDescriptor> _returns = [];

    /// <summary>
    /// Backing store for passive local descriptors.
    /// </summary>
    private IReadOnlyList<ParserRuleLocalDescriptor> _locals = [];

    /// <summary>
    /// Backing store for passive rule option metadata.
    /// </summary>
    private IReadOnlyDictionary<string, string> _options = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Backing store for passive exception metadata descriptors.
    /// </summary>
    private IReadOnlyList<ParserRuleExceptionDescriptor> _exceptions = [];

    /// <summary>
    /// Gets the parser rule name associated with the invocation.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Gets the raw parameter metadata text when it is available in the parser model.
    /// </summary>
    public string? RawParameters { get; init; }

    /// <summary>
    /// Gets the raw return metadata text when it is available in the parser model.
    /// </summary>
    public string? RawReturnType { get; init; }

    /// <summary>
    /// Gets the raw locals metadata text when it is available in the parser model.
    /// </summary>
    public string? RawLocals { get; init; }

    /// <summary>
    /// Gets passive parameter descriptors declared by the parser rule.
    /// </summary>
    public IReadOnlyList<ParserRuleParameterDescriptor> Parameters
    {
        get => _parameters;
        init => _parameters = value is null
            ? throw new ArgumentNullException(nameof(value))
            : value.ToArray();
    }

    /// <summary>
    /// Gets passive return descriptors declared by the parser rule.
    /// </summary>
    public IReadOnlyList<ParserRuleReturnDescriptor> Returns
    {
        get => _returns;
        init => _returns = value is null
            ? throw new ArgumentNullException(nameof(value))
            : value.ToArray();
    }

    /// <summary>
    /// Gets passive local descriptors declared by the parser rule.
    /// </summary>
    public IReadOnlyList<ParserRuleLocalDescriptor> Locals
    {
        get => _locals;
        init => _locals = value is null
            ? throw new ArgumentNullException(nameof(value))
            : value.ToArray();
    }

    /// <summary>
    /// Gets passive rule option metadata keyed by option name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options
    {
        get => _options;
        init => _options = value is null
            ? throw new ArgumentNullException(nameof(value))
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(value, StringComparer.Ordinal));
    }

    /// <summary>
    /// Gets passive exception metadata descriptors declared by the parser rule.
    /// </summary>
    public IReadOnlyList<ParserRuleExceptionDescriptor> Exceptions
    {
        get => _exceptions;
        init => _exceptions = value is null
            ? throw new ArgumentNullException(nameof(value))
            : value.ToArray();
    }

    /// <summary>
    /// Creates a passive invocation descriptor from the metadata currently exposed by a parser rule.
    /// </summary>
    /// <param name="rule">Parser rule whose available metadata should be described.</param>
    /// <returns>A passive descriptor containing only currently represented rule metadata.</returns>
    public static ParserRuleInvocationDescriptor FromRule(Rule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var parameterDescriptors = rule.Parameters?
            .Select(static parameter => new ParserRuleParameterDescriptor
            {
                Name = parameter.Name,
                RawDeclaration = GetRawDeclaration(parameter.Type, parameter.Name)
            })
            .ToArray() ?? [];
        var returnDescriptors = rule.Returns?
            .Select(static ruleReturn => new ParserRuleReturnDescriptor
            {
                Name = ruleReturn.Name,
                RawDeclaration = GetRawDeclaration(ruleReturn.Type, ruleReturn.Name)
            })
            .ToArray() ?? [];

        return new ParserRuleInvocationDescriptor
        {
            RuleName = rule.Name,
            RawParameters = JoinRawDeclarations(parameterDescriptors.Select(static descriptor => descriptor.RawDeclaration)),
            RawReturnType = JoinRawDeclarations(returnDescriptors.Select(static descriptor => descriptor.RawDeclaration)),
            RawLocals = null,
            Parameters = parameterDescriptors,
            Returns = returnDescriptors,
            Locals = [],
            Options = rule.Options?.Values ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Exceptions = []
        };
    }

    /// <summary>
    /// Preserves a declaration as raw metadata without inferring target-language types.
    /// </summary>
    /// <param name="type">Type text currently exposed by the parser model.</param>
    /// <param name="name">Name text currently exposed by the parser model.</param>
    /// <returns>The raw declaration text available to the descriptor.</returns>
    private static string GetRawDeclaration(string type, string name)
    {
        return string.Equals(type, name, StringComparison.Ordinal)
            ? type
            : $"{type} {name}";
    }

    /// <summary>
    /// Joins represented metadata declarations into a raw descriptor field.
    /// </summary>
    /// <param name="descriptors">Descriptors whose raw declarations should be joined.</param>
    /// <returns>A comma-separated raw metadata string, or <c>null</c> when no metadata is represented.</returns>
    private static string? JoinRawDeclarations(IEnumerable<string> rawDeclarations)
    {
        var raw = string.Join(", ", rawDeclarations);
        return raw.Length == 0 ? null : raw;
    }
}

/// <summary>
/// Describes a parser rule parameter declaration as passive metadata only.
/// </summary>
public sealed class ParserRuleParameterDescriptor
{
    /// <summary>
    /// Gets the parameter name text currently exposed by the parser model.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the raw parameter declaration text without semantic type binding.
    /// </summary>
    public required string RawDeclaration { get; init; }
}

/// <summary>
/// Describes a parser rule return declaration as passive metadata only.
/// </summary>
public sealed class ParserRuleReturnDescriptor
{
    /// <summary>
    /// Gets the return value name text currently exposed by the parser model.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the raw return declaration text without semantic type binding.
    /// </summary>
    public required string RawDeclaration { get; init; }
}

/// <summary>
/// Describes a parser rule local declaration as passive metadata only.
/// </summary>
public sealed class ParserRuleLocalDescriptor
{
    /// <summary>
    /// Gets the local name text when it is available in the parser model.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the raw local declaration text without semantic type binding or allocation.
    /// </summary>
    public required string RawDeclaration { get; init; }
}

/// <summary>
/// Describes parser rule exception metadata as passive metadata only.
/// </summary>
public sealed class ParserRuleExceptionDescriptor
{
    /// <summary>
    /// Gets the exception metadata kind, such as <c>throws</c>, <c>catch</c>, or <c>finally</c>, when available.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// Gets the raw exception metadata text without exception handling semantics.
    /// </summary>
    public required string RawDeclaration { get; init; }
}
