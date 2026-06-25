using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Parser.Diagnostics.EmbeddedCode;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{
    /// <summary>
    /// Emits local variables that expose runtime context metadata to user-authored embedded C#.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="predicate">Whether predicate-specific or action-specific source names should be exposed.</param>
    private static void EmitContextLocals(StringBuilder sb, bool predicate)
    {
        sb.AppendLine("        string ruleName = context.Rule.Name;");
        sb.AppendLine("        int inputPosition = context.InputPosition;");
        sb.AppendLine("        int alternativeIndex = context.AlternativeIndex;");
        sb.AppendLine("        int elementIndex = context.ElementIndex;");
        if (predicate)
        {
            sb.AppendLine("        string predicateCode = context.PredicateCode;");
        }
        else
        {
            sb.AppendLine("        string actionCode = context.ActionCode;");
        }
    }

    /// <summary>
    /// Emits normalized user-authored embedded C# into a generated hook body.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="body">Normalized generated embedded-code body.</param>
    /// <param name="indent">Indentation prefix applied to generated code lines.</param>
    private static void EmitGeneratedEmbeddedCodeBody(StringBuilder sb, GeneratedEmbeddedCodeBody body, string indent)
    {
        if (body.Kind == GeneratedEmbeddedCodeBodyKind.Expression)
        {
            sb.AppendLine($"{indent}return {body.Code};");
            return;
        }

        foreach (string line in SplitEmbeddedCodeLines(body.Code))
        {
            sb.AppendLine($"{indent}{line}");
        }
    }

    /// <summary>
    /// Splits user-authored embedded C# into lines after normalizing platform-specific newline forms.
    /// </summary>
    /// <param name="code">Embedded C# code to split.</param>
    /// <returns>Lines that can be re-emitted into a generated hook body.</returns>
    private static IEnumerable<string> SplitEmbeddedCodeLines(string code)
    {
        if (code.Length == 0)
        {
            yield break;
        }

        foreach (string line in code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            yield return line;
        }
    }


    /// <summary>
    /// Applies the configured embedded-code transformer and rejects diagnostics marked as errors.
    /// </summary>
    /// <param name="transformer">Transformer selected for generation.</param>
    /// <param name="code">Raw embedded code.</param>
    /// <param name="location">Grammar location of the embedded code.</param>
    /// <param name="grammar">Owning grammar.</param>
    /// <param name="rule">Owning parser rule, or <c>null</c> for grammar-level code.</param>
    /// <returns>Transformed code safe to emit.</returns>
    private static string TransformEmbeddedCode(
        IParserEmbeddedCodeTransformer transformer,
        string code,
        ParserEmbeddedCodeLocation location,
        G4Grammar grammar,
        G4Rule? rule)
    {
        ParserEmbeddedCodeTransformationResult result = transformer.Transform(new ParserEmbeddedCodeTransformationContext
        {
            Code = code,
            Location = location,
            GrammarName = grammar.Name,
            RuleName = rule?.Name,
            Parameters = CreateDeclarationDescriptors(rule?.Parameters),
            Locals = CreateDeclarationDescriptors(rule is null || rule.Locals.Count == 0 ? null : string.Join(", ", rule.Locals)),
            Returns = CreateDeclarationDescriptors(rule?.Returns),
            Labels = rule is null ? ParserEmbeddedCodeTransformationContext.EmptyLabels : CreateLabelDescriptors(rule.Content)
        });

        ParserEmbeddedCodeDiagnostic? error = result.Diagnostics.FirstOrDefault(static diagnostic => diagnostic.Severity == ParserEmbeddedCodeDiagnosticSeverity.Error);
        if (error is not null)
        {
            string codeText = string.IsNullOrWhiteSpace(error.Code) ? "APU embedded-code transformer" : error.Code!;
            throw new InvalidOperationException($"{codeText}: {error.Message}");
        }

        return result.Code;
    }

    /// <summary>
    /// Creates conservative declaration descriptors from a raw comma-separated declaration list.
    /// </summary>
    /// <param name="rawDeclarations">Raw declaration list, or <c>null</c> when absent.</param>
    /// <returns>Passive descriptors for transformer metadata.</returns>
    private static IReadOnlyList<ParserEmbeddedRuleDeclarationDescriptor> CreateDeclarationDescriptors(string? rawDeclarations)
    {
        if (string.IsNullOrWhiteSpace(rawDeclarations))
        {
            return Array.Empty<ParserEmbeddedRuleDeclarationDescriptor>();
        }

        return rawDeclarations!.Split(',')
            .Select(static declaration => declaration.Trim())
            .Where(static declaration => declaration.Length > 0)
            .Select(static declaration => new ParserEmbeddedRuleDeclarationDescriptor
            {
                RawDeclaration = declaration,
                Name = ExtractDeclarationName(declaration)
            })
            .ToArray();
    }

    /// <summary>
    /// Conservatively extracts the last identifier before a top-level default assignment.
    /// </summary>
    /// <param name="declaration">Raw declaration text.</param>
    /// <returns>The extracted identifier, or <c>null</c> when the declaration is not recognized.</returns>
    private static string? ExtractDeclarationName(string declaration)
    {
        int end = declaration.IndexOf('=');
        string candidate = (end >= 0 ? declaration.Substring(0, end) : declaration).Trim();
        int index = candidate.Length - 1;
        while (index >= 0 && (char.IsLetterOrDigit(candidate[index]) || candidate[index] == '_'))
        {
            index--;
        }

        string name = candidate.Substring(index + 1);
        return name.Length == 0 ? null : name;
    }

    /// <summary>
    /// Creates label descriptors for visible parser rule-reference labels without collapsing multi-target list labels.
    /// </summary>
    /// <param name="content">Rule content to inspect.</param>
    /// <returns>Label descriptors keyed by label name.</returns>
    private static IReadOnlyDictionary<string, ParserEmbeddedRuleLabelDescriptor> CreateLabelDescriptors(G4Content content)
    {
        var labels = new Dictionary<string, List<(bool IsList, string RuleName)>>(StringComparer.Ordinal);
        CollectLabelTargets(content, labels);
        return labels.ToDictionary(
            static item => item.Key,
            static item => new ParserEmbeddedRuleLabelDescriptor
            {
                Name = item.Key,
                IsList = item.Value.Any(static target => target.IsList),
                RuleNames = item.Value.Select(static target => target.RuleName).Distinct(StringComparer.Ordinal).ToArray()
            },
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Recursively collects parser rule-reference label targets from rule content.
    /// </summary>
    /// <param name="content">Content node to inspect.</param>
    /// <param name="labels">Mutable target map.</param>
    private static void CollectLabelTargets(G4Content content, Dictionary<string, List<(bool IsList, string RuleName)>> labels)
    {
        switch (content)
        {
            case G4RuleRef ruleRef when ruleRef.LabelName is not null:
                if (!labels.TryGetValue(ruleRef.LabelName, out var targets))
                {
                    targets = new List<(bool IsList, string RuleName)>();
                    labels.Add(ruleRef.LabelName, targets);
                }
                targets.Add((ruleRef.LabelIsAdditive, ruleRef.RuleName));
                break;
            case G4Alternation alternation:
                foreach (G4Alternative alternative in alternation.Alternatives) CollectLabelTargets(alternative, labels);
                break;
            case G4Alternative alternative:
                foreach (G4Content item in alternative.Items) CollectLabelTargets(item, labels);
                break;
            case G4Sequence sequence:
                foreach (G4Content item in sequence.Items) CollectLabelTargets(item, labels);
                break;
            case G4Quantifier quantifier:
                CollectLabelTargets(quantifier.Inner, labels);
                break;
            case G4Negation negation:
                CollectLabelTargets(negation.Inner, labels);
                break;
        }
    }

    /// <summary>
    /// Gets the stable generated execution-context class name for a generated grammar facade.
    /// </summary>
    /// <param name="className">Generated grammar facade class name.</param>
    /// <returns>The generated execution-context class name.</returns>
    private static string GetExecutionContextClassName(string className)
    {
        return className + "ExecutionContext";
    }

    /// <summary>
    /// Describes how user-authored embedded C# should be inserted into a generated hook.
    /// </summary>
    private enum GeneratedEmbeddedCodeBodyKind
    {
        /// <summary>The predicate body is emitted as a returned C# expression.</summary>
        Expression,

        /// <summary>The body is emitted as C# statements inside the hook method.</summary>
        Block
    }

    /// <summary>
    /// Normalized generated embedded-code body used by private generated hook emission.
    /// </summary>
    private sealed class GeneratedEmbeddedCodeBody
    {
        /// <summary>
        /// Initializes a normalized generated embedded-code body.
        /// </summary>
        /// <param name="kind">Emission shape for the generated hook body.</param>
        /// <param name="code">Trimmed user-authored C# body without ANTLR wrapper braces.</param>
        private GeneratedEmbeddedCodeBody(GeneratedEmbeddedCodeBodyKind kind, string code)
        {
            Kind = kind;
            Code = code;
        }

        /// <summary>Gets the emission shape for the generated hook body.</summary>
        public GeneratedEmbeddedCodeBodyKind Kind { get; }

        /// <summary>Gets the trimmed user-authored C# body without ANTLR wrapper braces.</summary>
        public string Code { get; }

        /// <summary>
        /// Classifies a parser semantic predicate body as an expression or statement block.
        /// </summary>
        /// <param name="code">Raw embedded predicate code without ANTLR braces.</param>
        /// <returns>A normalized predicate body.</returns>
        public static GeneratedEmbeddedCodeBody ForPredicate(string code)
        {
            string trimmedCode = code.Trim();
            var kind = ContainsReturnKeyword(trimmedCode)
                ? GeneratedEmbeddedCodeBodyKind.Block
                : GeneratedEmbeddedCodeBodyKind.Expression;

            return new GeneratedEmbeddedCodeBody(kind, trimmedCode);
        }

        /// <summary>
        /// Classifies a parser inline action body as statements emitted into a void hook.
        /// </summary>
        /// <param name="code">Raw embedded action code without ANTLR braces.</param>
        /// <returns>A normalized action body.</returns>
        public static GeneratedEmbeddedCodeBody ForAction(string code)
        {
            return new GeneratedEmbeddedCodeBody(GeneratedEmbeddedCodeBodyKind.Block, code.Trim());
        }

        /// <summary>
        /// Detects a C# <c>return</c> keyword using lightweight token-boundary checks only.
        /// </summary>
        /// <param name="code">Trimmed user-authored C# body.</param>
        /// <returns><see langword="true"/> when a delimited return keyword appears in the body.</returns>
        private static bool ContainsReturnKeyword(string code)
        {
            const string keyword = "return";
            int index = 0;

            while ((index = code.IndexOf(keyword, index, StringComparison.Ordinal)) >= 0)
            {
                int end = index + keyword.Length;
                bool startsAtBoundary = index == 0 || !IsIdentifierCharacter(code[index - 1]);
                bool endsAtBoundary = end == code.Length || !IsIdentifierCharacter(code[end]);
                if (startsAtBoundary && endsAtBoundary)
                {
                    return true;
                }

                index = end;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a character can be part of a C# identifier for lightweight keyword boundary checks.
        /// </summary>
        /// <param name="value">Character to inspect.</param>
        /// <returns><see langword="true"/> when the character is treated as identifier text.</returns>
        private static bool IsIdentifierCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_' || value == '@';
        }
    }

}
