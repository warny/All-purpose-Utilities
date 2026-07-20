using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Utils.Parser.Antlr4.Common;

namespace Utils.Parser.Generators.Internal;

/// <summary>Validates the exact generated-C# positional rule-call argument binding contract for local parser-rule targets.</summary>
internal static class GeneratedRuleArgumentBindingValidator
{
    /// <summary>Validates all parser rule references that target locally declared parser rules.</summary>
    internal static ImmutableArray<GeneratedRuleArgumentBindingIssue> Validate(G4Grammar grammar)
    {
        var localResolver = new G4ImportedRuleResolver(new G4GrammarProjectIndex([new G4GrammarProjectEntry(grammar.Name, grammar)]));
        return Validate(grammar, callSite => localResolver.Resolve(grammar, callSite.RuleName));
    }

    /// <summary>Validates all parser rule references whose local targets are resolved deterministically by the provided resolver.</summary>
    /// <param name="grammar">Caller grammar whose parser-rule bodies contain the call sites.</param>
    /// <param name="resolveRule">Resolver that maps a call site to a structured target resolution.</param>
    /// <returns>Deterministic binding issues for unique local parser-rule targets.</returns>
    internal static ImmutableArray<GeneratedRuleArgumentBindingIssue> Validate(G4Grammar grammar, Func<G4RuleRef, G4RuleResolution> resolveRule)
    {
        var issues = ImmutableArray.CreateBuilder<GeneratedRuleArgumentBindingIssue>();
        foreach (var owner in grammar.ParserRules)
        foreach (var callSite in G4ContentWalker.EnumerateRuleReferences(owner.Content))
        {
            if (callSite.RawArguments is null) continue;
            var resolution = resolveRule(callSite);
            if (resolution.Kind == G4RuleResolutionKind.Local && resolution.Rule is not null)
            {
                ValidateCallSite(resolution.Rule, callSite, issues);
            }
        }
        return issues.ToImmutable();
    }

    /// <summary>Validates a single resolved call site.</summary>
    private static void ValidateCallSite(G4Rule target, G4RuleRef callSite, ImmutableArray<GeneratedRuleArgumentBindingIssue>.Builder issues)
    {
        var parameters = ParseParameters(target.Parameters);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name)) { Add(issues, callSite, "A target parameter does not expose a usable name.", parameter: parameter); return; }
            if (!names.Add(parameter.Name)) { Add(issues, callSite, $"Target parameter name '{parameter.Name}' is duplicated.", parameter: parameter); return; }
            if (parameter.RawType is null) { Add(issues, callSite, "The declared parameter type is unavailable or does not use the supported 'type name' shape.", parameter: parameter); return; }
            if (!ParserLiteralTypeConverter.IsSupportedDeclaredType(parameter.RawType)) { Add(issues, callSite, $"Target parameter '{parameter.Name}' uses unsupported declared type '{parameter.RawType}'.", parameter: parameter); return; }
        }

        var arguments = callSite.RawArguments is null ? Array.Empty<string>() : ParserRawArgumentSplitter.SplitTopLevel(callSite.RawArguments);
        for (int i = 0; i < arguments.Count; i++)
        {
            if (ParserRawArgumentSplitter.ContainsTopLevelNamedSeparator(arguments[i])) { Add(issues, callSite, "Named rule-call arguments are not supported.", i); return; }
        }
        if (arguments.Count != parameters.Count) { Add(issues, callSite, $"Expected exactly {parameters.Count} positional argument(s), but received {arguments.Count}."); return; }
        for (int i = 0; i < arguments.Count; i++)
        {
            if (!ParserSimpleLiteralParser.TryParse(arguments[i], out object? literal)) { Add(issues, callSite, $"Argument {i} is not a supported simple literal.", i, parameters[i]); return; }
            var conversion = ParserLiteralTypeConverter.Convert(literal, parameters[i].RawType!);
            if (!conversion.Success) { Add(issues, callSite, $"Argument {i} cannot bind to declared type '{parameters[i].RawType}'.", i, parameters[i]); return; }
        }
    }

    /// <summary>Parses raw rule parameters using the generated binding supported declaration shape.</summary>
    private static IReadOnlyList<SimpleRuleParameterShape> ParseParameters(string? rawParameters)
    {
        if (string.IsNullOrWhiteSpace(rawParameters)) return Array.Empty<SimpleRuleParameterShape>();
        return ParserRawArgumentSplitter.SplitTopLevel(rawParameters!).Select(ParseParameter).ToArray();
    }

    /// <summary>Parses one raw parameter declaration into a simple shape.</summary>
    private static SimpleRuleParameterShape ParseParameter(string declaration)
    {
        int assignment = FindTopLevelAssignment(declaration);
        string prefix = declaration.Substring(0, assignment).Trim();
        string? name = GetTrailingIdentifier(prefix);
        string? rawType = null;
        if (name is not null && prefix.Length > name.Length && char.IsWhiteSpace(prefix[prefix.Length - name.Length - 1]))
        {
            string type = prefix.Substring(0, prefix.Length - name.Length).TrimEnd();
            rawType = type.Length == 0 ? null : type;
        }
        return new SimpleRuleParameterShape(name ?? string.Empty, rawType, assignment < declaration.Length ? declaration.Substring(assignment + 1).Trim() : null);
    }

    /// <summary>Finds a top-level assignment separator or returns the declaration length.</summary>
    private static int FindTopLevelAssignment(string text)
    {
        int depth = 0;
        char? quote = null;
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (quote is not null)
            {
                if (current == '\\' && index + 1 < text.Length)
                {
                    index++;
                    continue;
                }

                if (current == quote)
                {
                    quote = null;
                }

                continue;
            }

            if (current == '"' || current == '\'')
            {
                quote = current;
                continue;
            }

            if (current == '(' || current == '[' || current == '{')
            {
                depth++;
            }
            else if (current == ')' || current == ']' || current == '}')
            {
                if (depth > 0)
                {
                    depth--;
                }
            }
            else if (depth == 0 && current == '=')
            {
                return index;
            }
        }

        return text.Length;
    }

    /// <summary>Gets the trailing identifier from a declaration prefix.</summary>
    private static string? GetTrailingIdentifier(string text)
    {
        int end = text.Length - 1;
        while (end >= 0 && char.IsWhiteSpace(text[end])) end--;
        if (end < 0 || !(char.IsLetter(text[end]) || text[end] == '_' || char.IsDigit(text[end]))) return null;
        int start = end;
        while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '_')) start--;
        return text.Substring(start + 1, end - start);
    }

    /// <summary>Adds one issue with shared metadata.</summary>
    private static void Add(ImmutableArray<GeneratedRuleArgumentBindingIssue>.Builder issues, G4RuleRef callSite, string reason, int? argumentIndex = null, SimpleRuleParameterShape? parameter = null)
        => issues.Add(new GeneratedRuleArgumentBindingIssue { TargetRuleName = callSite.RuleName, Reason = reason, CallSite = callSite, ArgumentIndex = argumentIndex, ParameterName = parameter?.Name, DeclaredType = parameter?.RawType });

    /// <summary>Simple rule parameter metadata used by generated binding validation.</summary>
    private sealed class SimpleRuleParameterShape
    {
        public SimpleRuleParameterShape(string name, string? rawType, string? rawDefaultValue) { Name = name; RawType = rawType; RawDefaultValue = rawDefaultValue; }
        public string Name { get; }
        public string? RawType { get; }
        public string? RawDefaultValue { get; }
    }
}
