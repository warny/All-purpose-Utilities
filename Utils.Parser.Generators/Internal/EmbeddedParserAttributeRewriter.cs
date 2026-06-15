using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Rewrites the deliberately small supported subset of ANTLR-style parser return references in embedded C#.
/// </summary>
internal static class EmbeddedParserAttributeRewriter
{
    /// <summary>
    /// Validates every generated parser embedded-code location in a grammar.
    /// </summary>
    /// <param name="grammar">Grammar to validate.</param>
    /// <returns>Line-associated parser attribute validation errors.</returns>
    public static IReadOnlyList<EmbeddedParserAttributeDiagnostic> ValidateGrammar(G4Grammar grammar)
    {
        if (grammar is null) throw new ArgumentNullException(nameof(grammar));
        var diagnostics = new List<EmbeddedParserAttributeDiagnostic>();
        foreach (G4Rule rule in grammar.ParserRules)
        {
            if (rule.InitAction is not null)
            {
                AddDiagnostics(grammar, rule, rule.InitAction, EmbeddedParserAttributeLocationKind.Init, diagnostics);
            }

            if (rule.AfterAction is not null)
            {
                AddDiagnostics(grammar, rule, rule.AfterAction, EmbeddedParserAttributeLocationKind.After, diagnostics);
            }

            AddContentDiagnostics(grammar, rule, rule.Content, diagnostics);
        }

        return diagnostics;
    }

    /// <summary>Adds diagnostics for one embedded action.</summary>
    private static void AddDiagnostics(G4Grammar grammar, G4Rule rule, G4EmbeddedAction action, EmbeddedParserAttributeLocationKind kind, List<EmbeddedParserAttributeDiagnostic> diagnostics)
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite(action.Code, grammar, rule, kind);
        foreach (string error in result.Errors)
        {
            diagnostics.Add(new EmbeddedParserAttributeDiagnostic(action.Line, error));
        }
    }

    /// <summary>Recursively validates inline actions and predicates in parser content.</summary>
    private static void AddContentDiagnostics(G4Grammar grammar, G4Rule rule, G4Content content, List<EmbeddedParserAttributeDiagnostic> diagnostics)
    {
        switch (content)
        {
            case G4EmbeddedAction action:
                AddDiagnostics(grammar, rule, action, action.IsPredicate ? EmbeddedParserAttributeLocationKind.Predicate : EmbeddedParserAttributeLocationKind.InlineAction, diagnostics);
                break;
            case G4Alternation alternation:
                foreach (G4Alternative alternative in alternation.Alternatives) AddContentDiagnostics(grammar, rule, alternative, diagnostics);
                break;
            case G4Alternative alternative:
                foreach (G4Content item in alternative.Items) AddContentDiagnostics(grammar, rule, item, diagnostics);
                break;
            case G4Sequence sequence:
                foreach (G4Content item in sequence.Items) AddContentDiagnostics(grammar, rule, item, diagnostics);
                break;
            case G4Quantifier quantifier:
                AddContentDiagnostics(grammar, rule, quantifier.Inner, diagnostics);
                break;
            case G4Negation negation:
                AddContentDiagnostics(grammar, rule, negation.Inner, diagnostics);
                break;
        }
    }

    /// <summary>
    /// Rewrites supported attribute reads and returns deterministic validation errors for unsupported forms.
    /// </summary>
    /// <param name="code">Embedded C# source to inspect.</param>
    /// <param name="grammar">Owning grammar.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="locationKind">Embedded-code lifecycle location.</param>
    /// <returns>The rewritten source and any validation errors.</returns>
    public static EmbeddedParserAttributeRewriteResult Rewrite(
        string code,
        G4Grammar grammar,
        G4Rule rule,
        EmbeddedParserAttributeLocationKind locationKind)
    {
        if (code is null) throw new ArgumentNullException(nameof(code));
        if (grammar is null) throw new ArgumentNullException(nameof(grammar));
        if (rule is null) throw new ArgumentNullException(nameof(rule));

        var errors = new List<string>();
        var output = new StringBuilder(code.Length);
        var labels = CollectLabels(rule.Content);
        var parserRules = grammar.ParserRules.ToDictionary(static candidate => candidate.Name, StringComparer.Ordinal);
        var currentReturns = ParseDeclarationNames(rule.Returns);
        int index = 0;

        while (index < code.Length)
        {
            if (TryCopyTriviaOrLiteral(code, ref index, output))
            {
                continue;
            }

            if (code[index] != '$' || index + 1 >= code.Length || !IsIdentifierStart(code[index + 1]))
            {
                output.Append(code[index++]);
                continue;
            }

            int attributeStart = index;
            index++;
            string root = ReadIdentifier(code, ref index);
            int dotIndex = SkipWhitespace(code, index);
            if (dotIndex >= code.Length || code[dotIndex] != '.')
            {
                errors.Add($"Bare parser attribute '${root}' is not supported. Only '$label.return' and '$rule.return' reads are supported.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            int returnStart = SkipWhitespace(code, dotIndex + 1);
            if (returnStart >= code.Length || !IsIdentifierStart(code[returnStart]))
            {
                errors.Add($"Malformed parser attribute starting with '${root}'. Expected a return name after '.'.");
                output.Append(code, attributeStart, Math.Max(1, returnStart - attributeStart));
                index = returnStart;
                continue;
            }

            index = returnStart;
            string returnName = ReadIdentifier(code, ref index);
            string attributeText = $"${root}.{returnName}";
            int next = SkipWhitespace(code, index);
            if (next < code.Length && code[next] == '.')
            {
                errors.Add($"Chained parser attribute '{attributeText}' is not supported.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (IsWriteContext(code, attributeStart, index))
            {
                errors.Add($"Parser attribute writes are not supported for '{attributeText}'. Use SetRuleReturn(context, name, value) for current-rule returns.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (locationKind == EmbeddedParserAttributeLocationKind.Predicate)
            {
                errors.Add($"Parser attribute read '{attributeText}' is not supported in semantic predicates.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (string.Equals(root, rule.Name, StringComparison.Ordinal))
            {
                if (!currentReturns.Contains(returnName))
                {
                    errors.Add($"Return '{returnName}' is not declared by current parser rule '{rule.Name}'.");
                    output.Append(code, attributeStart, index - attributeStart);
                    continue;
                }

                output.Append("GetRequiredRuleReturn(context, \"")
                    .Append(Escape(returnName))
                    .Append("\")");
                continue;
            }

            if (!labels.TryGetValue(root, out RuleLabelTarget? label))
            {
                errors.Add($"Parser attribute root '{root}' is not the current rule name or a visible assignment rule-reference label.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (label.IsAdditive)
            {
                errors.Add($"List label '{root}' cannot be read as a scalar parser attribute. Use GetLabeledRuleCallReturns(context, \"{root}\", \"{returnName}\").");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (!parserRules.TryGetValue(label.RuleName, out G4Rule? targetRule))
            {
                errors.Add($"Token label '{root}' cannot be used as a parser rule-return attribute.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (!ParseDeclarationNames(targetRule.Returns).Contains(returnName))
            {
                errors.Add($"Return '{returnName}' is not declared by parser rule '{targetRule.Name}' referenced by assignment label '{root}'.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (locationKind == EmbeddedParserAttributeLocationKind.Init)
            {
                errors.Add($"Assignment label '{root}' is not available in @init. Read '{attributeText}' only after the child rule call succeeds.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            output.Append("GetRequiredLabeledRuleCallReturn(context, \"")
                .Append(Escape(root))
                .Append("\", \"")
                .Append(Escape(returnName))
                .Append("\")");
        }

        return new EmbeddedParserAttributeRewriteResult(output.ToString(), errors);
    }

    /// <summary>
    /// Collects rule-reference labels visible within the owning parser rule using ordinal names.
    /// </summary>
    /// <param name="content">Rule content to inspect.</param>
    /// <returns>Rule-wide label targets.</returns>
    private static Dictionary<string, RuleLabelTarget> CollectLabels(G4Content content)
    {
        var labels = new Dictionary<string, RuleLabelTarget>(StringComparer.Ordinal);
        CollectLabels(content, labels);
        return labels;
    }

    /// <summary>
    /// Recursively collects labeled rule references without crossing into referenced child rules.
    /// </summary>
    /// <param name="content">Content node to inspect.</param>
    /// <param name="labels">Destination label map.</param>
    private static void CollectLabels(G4Content content, Dictionary<string, RuleLabelTarget> labels)
    {
        switch (content)
        {
            case G4RuleRef ruleRef when ruleRef.LabelName is not null:
                labels[ruleRef.LabelName] = new RuleLabelTarget(ruleRef.RuleName, ruleRef.LabelIsAdditive);
                break;
            case G4Alternation alternation:
                foreach (G4Alternative alternative in alternation.Alternatives)
                {
                    CollectLabels(alternative, labels);
                }
                break;
            case G4Alternative alternative:
                foreach (G4Content item in alternative.Items)
                {
                    CollectLabels(item, labels);
                }
                break;
            case G4Sequence sequence:
                foreach (G4Content item in sequence.Items)
                {
                    CollectLabels(item, labels);
                }
                break;
            case G4Quantifier quantifier:
                CollectLabels(quantifier.Inner, labels);
                break;
            case G4Negation negation:
                CollectLabels(negation.Inner, labels);
                break;
        }
    }

    /// <summary>
    /// Extracts lexical declaration names from a raw comma-separated return clause without resolving C# types.
    /// </summary>
    /// <param name="rawDeclarations">Raw declaration text.</param>
    /// <returns>Ordinal declaration names.</returns>
    private static HashSet<string> ParseDeclarationNames(string? rawDeclarations)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(rawDeclarations))
        {
            return names;
        }

        foreach (string declaration in SplitTopLevel(rawDeclarations!))
        {
            int end = declaration.Length - 1;
            while (end >= 0 && char.IsWhiteSpace(declaration[end]))
            {
                end--;
            }

            int start = end;
            while (start >= 0 && IsIdentifierPart(declaration[start]))
            {
                start--;
            }

            if (end >= 0 && start < end)
            {
                names.Add(declaration.Substring(start + 1, end - start));
            }
        }

        return names;
    }

    /// <summary>
    /// Splits declaration text at top-level commas while preserving nested generic, tuple, array, and initializer text.
    /// </summary>
    /// <param name="text">Raw declaration text.</param>
    /// <returns>Top-level declaration segments.</returns>
    private static IEnumerable<string> SplitTopLevel(string text)
    {
        int start = 0;
        int angle = 0;
        int round = 0;
        int square = 0;
        int curly = 0;
        for (int index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '<': angle++; break;
                case '>': angle = Math.Max(0, angle - 1); break;
                case '(': round++; break;
                case ')': round = Math.Max(0, round - 1); break;
                case '[': square++; break;
                case ']': square = Math.Max(0, square - 1); break;
                case '{': curly++; break;
                case '}': curly = Math.Max(0, curly - 1); break;
                case ',' when angle == 0 && round == 0 && square == 0 && curly == 0:
                    yield return text.Substring(start, index - start).Trim();
                    start = index + 1;
                    break;
            }
        }

        string final = text.Substring(start).Trim();
        if (final.Length > 0)
        {
            yield return final;
        }
    }

    /// <summary>
    /// Copies a C# comment, character literal, or string literal without inspecting attribute-like text inside it.
    /// </summary>
    /// <param name="code">Source text.</param>
    /// <param name="index">Current source index, advanced when trivia or a literal is copied.</param>
    /// <param name="output">Rewrite destination.</param>
    /// <returns><see langword="true"/> when a complete trivia or literal region was copied.</returns>
    private static bool TryCopyTriviaOrLiteral(string code, ref int index, StringBuilder output)
    {
        int start = index;
        if (code[index] == '/' && index + 1 < code.Length && code[index + 1] == '/')
        {
            index += 2;
            while (index < code.Length && code[index] != '\n') index++;
        }
        else if (code[index] == '/' && index + 1 < code.Length && code[index + 1] == '*')
        {
            index += 2;
            while (index + 1 < code.Length && !(code[index] == '*' && code[index + 1] == '/')) index++;
            index = Math.Min(code.Length, index + 2);
        }
        else if (TryGetStringPrefix(code, index, out int quoteIndex, out bool verbatim))
        {
            int quoteCount = CountRun(code, quoteIndex, '"');
            if (quoteCount >= 3)
            {
                index = quoteIndex + quoteCount;
                while (index < code.Length && CountRun(code, index, '"') < quoteCount) index++;
                index = Math.Min(code.Length, index + quoteCount);
            }
            else
            {
                index = quoteIndex + 1;
                while (index < code.Length)
                {
                    if (code[index] == '"')
                    {
                        if (verbatim && index + 1 < code.Length && code[index + 1] == '"')
                        {
                            index += 2;
                            continue;
                        }

                        index++;
                        break;
                    }

                    if (!verbatim && code[index] == '\\' && index + 1 < code.Length) index += 2;
                    else index++;
                }
            }
        }
        else if (code[index] == '\'')
        {
            index++;
            while (index < code.Length)
            {
                if (code[index] == '\\' && index + 1 < code.Length) index += 2;
                else if (code[index++] == '\'') break;
            }
        }
        else
        {
            return false;
        }

        output.Append(code, start, index - start);
        return true;
    }

    /// <summary>
    /// Recognizes normal, verbatim, interpolated, and raw C# string prefixes.
    /// Interpolated strings are copied as a whole because interpolation-expression parsing is intentionally out of scope.
    /// </summary>
    /// <param name="code">Source text.</param>
    /// <param name="index">Candidate prefix index.</param>
    /// <param name="quoteIndex">Index of the opening quote.</param>
    /// <param name="verbatim">Whether doubled quotes escape content.</param>
    /// <returns><see langword="true"/> when a supported string prefix starts at the index.</returns>
    private static bool TryGetStringPrefix(string code, int index, out int quoteIndex, out bool verbatim)
    {
        quoteIndex = index;
        verbatim = false;
        if (code[index] == '"') return true;
        if (code[index] == '@' && index + 1 < code.Length && code[index + 1] == '"')
        {
            quoteIndex = index + 1;
            verbatim = true;
            return true;
        }
        if (code[index] == '$' && index + 1 < code.Length && code[index + 1] == '"')
        {
            quoteIndex = index + 1;
            return true;
        }
        if (index + 2 < code.Length && ((code[index] == '$' && code[index + 1] == '@') || (code[index] == '@' && code[index + 1] == '$')) && code[index + 2] == '"')
        {
            quoteIndex = index + 2;
            verbatim = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Detects assignment, increment/decrement, and ref/out write contexts around an attribute expression.
    /// </summary>
    /// <param name="code">Embedded source.</param>
    /// <param name="start">Attribute start.</param>
    /// <param name="end">Attribute end.</param>
    /// <returns><see langword="true"/> when the attribute is used as a write target.</returns>
    private static bool IsWriteContext(string code, int start, int end)
    {
        int next = SkipWhitespace(code, end);
        string[] followingOperators = ["++", "--", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=", "="];
        if (followingOperators.Any(op => StartsWith(code, next, op)) && !StartsWith(code, next, "==") && !StartsWith(code, next, "=>"))
        {
            return true;
        }

        int previous = start - 1;
        while (previous >= 0 && char.IsWhiteSpace(code[previous])) previous--;
        if (previous >= 1 && ((code[previous - 1] == '+' && code[previous] == '+') || (code[previous - 1] == '-' && code[previous] == '-')))
        {
            return true;
        }

        int wordEnd = previous + 1;
        while (previous >= 0 && IsIdentifierPart(code[previous])) previous--;
        string previousWord = code.Substring(previous + 1, wordEnd - previous - 1);
        return string.Equals(previousWord, "ref", StringComparison.Ordinal) || string.Equals(previousWord, "out", StringComparison.Ordinal);
    }

    /// <summary>Reads one identifier and advances the supplied index.</summary>
    private static string ReadIdentifier(string text, ref int index)
    {
        int start = index++;
        while (index < text.Length && IsIdentifierPart(text[index])) index++;
        return text.Substring(start, index - start);
    }

    /// <summary>Skips whitespace from a source index.</summary>
    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        return index;
    }

    /// <summary>Counts a repeated character run.</summary>
    private static int CountRun(string text, int index, char value)
    {
        int count = 0;
        while (index + count < text.Length && text[index + count] == value) count++;
        return count;
    }

    /// <summary>Tests whether source text starts with a value using ordinal matching.</summary>
    private static bool StartsWith(string text, int index, string value)
    {
        return index >= 0 && index + value.Length <= text.Length && string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
    }

    /// <summary>Determines whether a character can start the supported identifier form.</summary>
    private static bool IsIdentifierStart(char value) => value == '_' || char.IsLetter(value);

    /// <summary>Determines whether a character can continue the supported identifier form.</summary>
    private static bool IsIdentifierPart(char value) => value == '_' || char.IsLetterOrDigit(value);

    /// <summary>Escapes text for a generated C# string literal.</summary>
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>Stores the target of one visible rule-reference label.</summary>
    private sealed class RuleLabelTarget
    {
        /// <summary>Initializes label target metadata.</summary>
        public RuleLabelTarget(string ruleName, bool isAdditive)
        {
            RuleName = ruleName;
            IsAdditive = isAdditive;
        }

        /// <summary>Gets the referenced rule name.</summary>
        public string RuleName { get; }

        /// <summary>Gets whether the label is additive.</summary>
        public bool IsAdditive { get; }
    }
}

/// <summary>Identifies the generated embedded-code location being rewritten.</summary>
internal enum EmbeddedParserAttributeLocationKind
{
    /// <summary>Rule initialization action.</summary>
    Init,
    /// <summary>Inline parser action.</summary>
    InlineAction,
    /// <summary>Semantic predicate.</summary>
    Predicate,
    /// <summary>Rule after action.</summary>
    After
}

/// <summary>Contains rewritten embedded source and deterministic validation errors.</summary>
internal sealed class EmbeddedParserAttributeRewriteResult
{
    /// <summary>Initializes a rewrite result.</summary>
    public EmbeddedParserAttributeRewriteResult(string code, IReadOnlyList<string> errors)
    {
        Code = code;
        Errors = errors;
    }

    /// <summary>Gets rewritten C# source.</summary>
    public string Code { get; }

    /// <summary>Gets validation errors.</summary>
    public IReadOnlyList<string> Errors { get; }
}


/// <summary>Associates one parser attribute validation message with its grammar line.</summary>
internal sealed class EmbeddedParserAttributeDiagnostic
{
    /// <summary>Initializes a parser attribute diagnostic.</summary>
    public EmbeddedParserAttributeDiagnostic(int line, string message)
    {
        Line = line;
        Message = message;
    }

    /// <summary>Gets the one-based grammar line.</summary>
    public int Line { get; }

    /// <summary>Gets the deterministic validation message.</summary>
    public string Message { get; }
}
