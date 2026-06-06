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
        var localDescriptors = rule.Locals?
            .SelectMany(static local => BuildLocalDescriptors(local.RawDeclaration))
            .ToArray() ?? [];
        var exceptionDescriptors = BuildExceptionDescriptors(rule.ExceptionMetadata);

        return new ParserRuleInvocationDescriptor
        {
            RuleName = rule.Name,
            RawParameters = JoinRawDeclarations(parameterDescriptors.Select(static descriptor => descriptor.RawDeclaration)),
            RawReturnType = JoinRawDeclarations(returnDescriptors.Select(static descriptor => descriptor.RawDeclaration)),
            RawLocals = JoinRawDeclarations(localDescriptors.Select(static descriptor => descriptor.RawDeclaration)),
            Parameters = parameterDescriptors,
            Returns = returnDescriptors,
            Locals = localDescriptors,
            Options = rule.Options?.Values ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Exceptions = exceptionDescriptors
        };
    }

    /// <summary>
    /// Builds passive local descriptors by separating top-level declarations and capturing only their names.
    /// </summary>
    /// <param name="rawLocals">Raw contents of a rule <c>locals [...]</c> clause.</param>
    /// <returns>Untyped local descriptors preserving each raw declaration and its lexical name.</returns>
    private static IEnumerable<ParserRuleLocalDescriptor> BuildLocalDescriptors(string rawLocals)
    {
        foreach (string declaration in SplitTopLevelLocalDeclarations(rawLocals))
        {
            yield return new ParserRuleLocalDescriptor
            {
                Name = GetLocalDeclarationName(declaration),
                RawDeclaration = declaration
            };
        }
    }

    /// <summary>
    /// Splits raw local metadata at top-level commas without interpreting target-language types.
    /// </summary>
    /// <param name="rawLocals">Raw local declaration list to split.</param>
    /// <returns>Trimmed declarations in source order.</returns>
    private static IEnumerable<string> SplitTopLevelLocalDeclarations(string rawLocals)
    {
        int start = 0;
        int squareDepth = 0;
        int roundDepth = 0;
        int braceDepth = 0;
        int angleDepth = 0;

        for (int index = 0; index < rawLocals.Length; index++)
        {
            switch (rawLocals[index])
            {
                case '[': squareDepth++; break;
                case ']': squareDepth = int.Max(0, squareDepth - 1); break;
                case '(': roundDepth++; break;
                case ')': roundDepth = int.Max(0, roundDepth - 1); break;
                case '{': braceDepth++; break;
                case '}': braceDepth = int.Max(0, braceDepth - 1); break;
                case '<': angleDepth++; break;
                case '>': angleDepth = int.Max(0, angleDepth - 1); break;
                case ',' when squareDepth == 0 && roundDepth == 0 && braceDepth == 0 && angleDepth == 0:
                    string declaration = rawLocals[start..index].Trim();
                    if (declaration.Length > 0)
                    {
                        yield return declaration;
                    }

                    start = index + 1;
                    break;
            }
        }

        string finalDeclaration = rawLocals[start..].Trim();
        if (finalDeclaration.Length > 0)
        {
            yield return finalDeclaration;
        }
    }

    /// <summary>
    /// Gets the final identifier from a raw local declaration without binding or validating its type.
    /// </summary>
    /// <param name="declaration">One raw local declaration.</param>
    /// <returns>The lexical local name, or <c>null</c> when no identifier is available.</returns>
    private static string? GetLocalDeclarationName(string declaration)
    {
        int assignmentIndex = declaration.IndexOf('=');
        int index = (assignmentIndex >= 0 ? assignmentIndex : declaration.Length) - 1;
        while (index >= 0 && char.IsWhiteSpace(declaration[index]))
        {
            index--;
        }

        int end = index + 1;
        while (index >= 0 && (char.IsLetterOrDigit(declaration[index]) || declaration[index] == '_'))
        {
            index--;
        }

        if (end == index + 1 || (!char.IsLetter(declaration[index + 1]) && declaration[index + 1] != '_'))
        {
            return null;
        }

        return declaration[(index + 1)..end];
    }

    /// <summary>
    /// Builds passive exception descriptors from metadata preserved by the parser rule model.
    /// </summary>
    /// <param name="metadata">Rule exception metadata, or <c>null</c> when none is represented.</param>
    /// <returns>Passive exception descriptors preserving only available raw text.</returns>
    private static ParserRuleExceptionDescriptor[] BuildExceptionDescriptors(RuleExceptionMetadata? metadata)
    {
        if (metadata is null)
        {
            return [];
        }

        var descriptors = new List<ParserRuleExceptionDescriptor>();
        descriptors.AddRange(metadata.Throws.Select(static declaration => new ParserRuleExceptionDescriptor
        {
            Kind = "throws",
            RawDeclaration = declaration
        }));
        descriptors.AddRange(metadata.CatchClauses.Select(static clause => new ParserRuleExceptionDescriptor
        {
            Kind = "catch",
            RawDeclaration = $"[{clause.RawArgument}] {{ {clause.RawAction} }}"
        }));

        if (metadata.FinallyAction is not null)
        {
            descriptors.Add(new ParserRuleExceptionDescriptor
            {
                Kind = "finally",
                RawDeclaration = $"{{ {metadata.FinallyAction} }}"
            });
        }

        return descriptors.ToArray();
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
