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

        return RewriteCore(code, grammar, rule, locationKind, allowWrites: true);
    }

    /// <summary>
    /// Rewrites supported attributes with an explicit mode controlling whether local-write statements are accepted.
    /// </summary>
    private static EmbeddedParserAttributeRewriteResult RewriteCore(
        string code,
        G4Grammar grammar,
        G4Rule rule,
        EmbeddedParserAttributeLocationKind locationKind,
        bool allowWrites)
    {
        var errors = new List<string>();
        var output = new StringBuilder(code.Length);
        var labels = CollectLabels(rule.Content);
        var parserRules = grammar.ParserRules.ToDictionary(static candidate => candidate.Name, StringComparer.Ordinal);
        var currentReturns = ParseDeclarationNames(rule.Returns);
        var parameters = ParseTypedDeclarations(rule.Parameters);
        var locals = ParseTypedDeclarations(rule.Locals.Count == 0 ? null : string.Join(", ", rule.Locals));
        int index = 0;

        while (index < code.Length)
        {
            if (TryCopyTriviaOrLiteral(code, ref index, output))
            {
                continue;
            }

            if (allowWrites && TryRewritePrefixLocalUpdate(code, ref index, grammar, rule, locationKind, errors, output, parameters, locals))
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
                if (allowWrites && TryRewriteBareLocalWrite(code, ref index, grammar, rule, locationKind, errors, output, parameters, locals, labels, attributeStart, root))
                {
                    continue;
                }

                RewriteBareAttribute(code, rule, locationKind, allowWrites, errors, output, parameters, locals, labels, attributeStart, index, root);
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
            bool isUnambiguousListRoot = labels.TryGetValue(root, out RuleLabelTargets? earlyLabel)
                && earlyLabel.Assignment is null
                && earlyLabel.List.Count > 0;
            bool continuesWithSupportedListMember = isUnambiguousListRoot
                && next < code.Length
                && code[next] == '.'
                && (IsMemberName(code, next + 1, "Count") || IsMemberName(code, next + 1, "Select"));
            if (next < code.Length && code[next] == '.' && !continuesWithSupportedListMember)
            {
                errors.Add($"Chained parser attribute '{attributeText}' is not supported.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (IsWriteContext(code, attributeStart, index))
            {
                string message = string.Equals(root, rule.Name, StringComparison.Ordinal)
                    ? $"Current-rule return attribute '{attributeText}' is read-only through parser attribute syntax in generated C#. Use SetRuleReturn(context, \"{Escape(returnName)}\", value) instead."
                    : $"Label-return attribute '{attributeText}' is read-only through parser attribute syntax in generated C#.";
                errors.Add(message);
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

            if (!labels.TryGetValue(root, out RuleLabelTargets? label))
            {
                errors.Add($"Parser attribute root '{root}' is not the current rule name or a visible assignment rule-reference label.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (label.Assignment is not null && label.List.Count > 0)
            {
                errors.Add($"Parser attribute root '{root}' is used as both assignment and list label in this rule. Use explicit helpers to disambiguate.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (label.List.Count > 0)
            {
                if (label.List.Any(target => !parserRules.ContainsKey(target.RuleName)))
                {
                    errors.Add($"Token label '{root}' cannot be used as a parser rule-return attribute.");
                    output.Append(code, attributeStart, index - attributeStart);
                    continue;
                }

                bool isDeclaredByAnyTarget = label.List
                    .Select(target => parserRules[target.RuleName])
                    .Any(targetRule => ParseDeclarationNames(targetRule.Returns).Contains(returnName));
                if (!isDeclaredByAnyTarget)
                {
                    errors.Add($"Return '{returnName}' is not declared by any parser rule referenced by list label '{root}'.");
                    output.Append(code, attributeStart, index - attributeStart);
                    continue;
                }
            }
            else
            {
                RuleLabelTarget target = label.Assignment!;
                if (!parserRules.TryGetValue(target.RuleName, out G4Rule? targetRule))
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
            }

            if (locationKind == EmbeddedParserAttributeLocationKind.Init)
            {
                string labelKind = label.List.Count == 0 ? "Assignment" : "List";
                errors.Add($"{labelKind} label '{root}' is not available in @init. Read '{attributeText}' only after the child rule call succeeds.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            output.Append(label.List.Count == 0
                    ? "GetRequiredLabeledRuleCallReturn(context, \""
                    : "GetLabeledRuleCallReturns(context, \"")
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
    private static Dictionary<string, RuleLabelTargets> CollectLabels(G4Content content)
    {
        var labels = new Dictionary<string, RuleLabelTargets>(StringComparer.Ordinal);
        CollectLabels(content, labels);
        return labels;
    }

    /// <summary>
    /// Recursively collects labeled rule references without crossing into referenced child rules.
    /// </summary>
    /// <param name="content">Content node to inspect.</param>
    /// <param name="labels">Destination label map.</param>
    private static void CollectLabels(G4Content content, Dictionary<string, RuleLabelTargets> labels)
    {
        switch (content)
        {
            case G4RuleRef ruleRef when ruleRef.LabelName is not null:
                if (!labels.TryGetValue(ruleRef.LabelName, out RuleLabelTargets? targets))
                {
                    targets = new RuleLabelTargets();
                    labels.Add(ruleRef.LabelName, targets);
                }

                targets.Add(new RuleLabelTarget(ruleRef.RuleName), ruleRef.LabelIsAdditive);
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
    /// Rewrites one bare current-rule parameter or local attribute, or records a deterministic validation error.
    /// </summary>
    private static void RewriteBareAttribute(
        string code,
        G4Rule rule,
        EmbeddedParserAttributeLocationKind locationKind,
        bool allowWrites,
        List<string> errors,
        StringBuilder output,
        Dictionary<string, TypedDeclaration> parameters,
        Dictionary<string, TypedDeclaration> locals,
        Dictionary<string, RuleLabelTargets> labels,
        int attributeStart,
        int attributeEnd,
        string root)
    {
        string attributeText = "$" + root;
        if (IsWriteContext(code, attributeStart, attributeEnd))
        {
            string message;
            if (IsRefOrOutContext(code, attributeStart))
            {
                message = $"ref/out parser attributes are not supported for '{attributeText}' in generated embedded parser C#.";
            }
            else if (parameters.ContainsKey(root))
            {
                message = $"Parser parameter '{attributeText}' is read-only. Use a local if mutable state is needed.";
            }
            else if (locals.ContainsKey(root))
            {
                message = allowWrites
                    ? $"Parser local write '{attributeText}' is only supported as a standalone assignment or increment/decrement statement."
                    : $"Parser local write '{attributeText}' is not supported inside expressions.";
            }
            else
            {
                message = $"Parser attribute write target '{attributeText}' does not resolve to a writable current-rule local.";
            }

            errors.Add(message);
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return;
        }

        if (locationKind == EmbeddedParserAttributeLocationKind.Predicate)
        {
            errors.Add($"Parser attribute read '{attributeText}' is not supported in semantic predicates.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return;
        }

        bool hasParameter = parameters.TryGetValue(root, out TypedDeclaration? parameter);
        bool hasLocal = locals.TryGetValue(root, out TypedDeclaration? local);
        if (hasParameter && hasLocal)
        {
            errors.Add($"Bare parser attribute '{attributeText}' is ambiguous because current rule '{rule.Name}' declares both a parameter and a local named '{root}'.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return;
        }

        TypedDeclaration? declaration = hasParameter ? parameter : local;
        if (declaration is null)
        {
            string message = labels.ContainsKey(root)
                ? $"Bare parser attribute '{attributeText}' is a label access and is not supported. Use '$" + root + ".returnName' for declared child rule returns."
                : $"Bare parser attribute '{attributeText}' does not resolve to a current-rule parameter or local.";
            errors.Add(message);
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return;
        }

        if (string.IsNullOrWhiteSpace(declaration.RawType))
        {
            errors.Add($"Bare parser attribute '{attributeText}' cannot be typed because declaration '{declaration.RawDeclaration}' does not expose a raw type.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return;
        }

        output.Append(hasParameter ? "GetRequiredRuleParameter<" : "GetRequiredRuleLocal<")
            .Append(declaration.RawType)
            .Append(">(context, \"")
            .Append(Escape(root))
            .Append("\")");
    }

    /// <summary>
    /// Rewrites a supported standalone assignment, compound assignment, or postfix update of a current-rule local.
    /// </summary>
    private static bool TryRewriteBareLocalWrite(
        string code,
        ref int index,
        G4Grammar grammar,
        G4Rule rule,
        EmbeddedParserAttributeLocationKind locationKind,
        List<string> errors,
        StringBuilder output,
        Dictionary<string, TypedDeclaration> parameters,
        Dictionary<string, TypedDeclaration> locals,
        Dictionary<string, RuleLabelTargets> labels,
        int attributeStart,
        string root)
    {
        int afterAttribute = index;
        int operatorStart = SkipWhitespace(code, afterAttribute);
        string? assignmentOperator = ReadAssignmentOperator(code, operatorStart);
        string? postfixOperator = StartsWith(code, operatorStart, "++") ? "++" : StartsWith(code, operatorStart, "--") ? "--" : null;
        if (assignmentOperator is null && postfixOperator is null)
        {
            return false;
        }

        string attributeText = "$" + root;
        if (!locals.TryGetValue(root, out TypedDeclaration? local))
        {
            string message = parameters.ContainsKey(root)
                ? $"Parser parameter '{attributeText}' is read-only. Use a local if mutable state is needed."
                : labels.ContainsKey(root)
                    ? $"Bare parser attribute '{attributeText}' is a label access and is read-only through generated embedded parser C#."
                    : $"Parser attribute write target '{attributeText}' does not resolve to a writable current-rule local.";
            errors.Add(message);
            int statementEnd = FindTopLevelSemicolon(code, afterAttribute);
            if (statementEnd >= 0)
            {
                output.Append(code, attributeStart, statementEnd - attributeStart + 1);
                index = statementEnd + 1;
            }
            else
            {
                output.Append(code, attributeStart, afterAttribute - attributeStart);
            }
            return true;
        }

        if (locationKind == EmbeddedParserAttributeLocationKind.Predicate)
        {
            errors.Add($"Parser local write '{attributeText}' is not supported in semantic predicates.");
            int statementEnd = FindTopLevelSemicolon(code, afterAttribute);
            if (statementEnd >= 0)
            {
                output.Append(code, attributeStart, statementEnd - attributeStart + 1);
                index = statementEnd + 1;
            }
            else
            {
                output.Append(code, attributeStart, afterAttribute - attributeStart);
            }
            return true;
        }

        if (string.IsNullOrWhiteSpace(local.RawType))
        {
            errors.Add($"Parser local write '{attributeText}' cannot be typed because declaration '{local.RawDeclaration}' does not expose a raw type.");
            output.Append(code, attributeStart, afterAttribute - attributeStart);
            return false;
        }

        if (postfixOperator is not null)
        {
            int semicolon = SkipWhitespace(code, operatorStart + postfixOperator.Length);
            if (semicolon >= code.Length || code[semicolon] != ';' || !IsStandaloneStatementStart(code, attributeStart))
            {
                errors.Add($"Parser local update '{attributeText}{postfixOperator}' is supported only as a standalone statement.");
                output.Append(code, attributeStart, afterAttribute - attributeStart);
                return false;
            }

            AppendSetLocal(output, local.RawType!, root, $"GetRequiredRuleLocal<{local.RawType}>(context, \"{Escape(root)}\") {(postfixOperator == "++" ? "+" : "-")} 1");
            index = semicolon + 1;
            return true;
        }

        if (!IsStandaloneStatementStart(code, attributeStart))
        {
            errors.Add($"Parser local assignment '{attributeText} {assignmentOperator}' is supported only as a standalone statement.");
            output.Append(code, attributeStart, afterAttribute - attributeStart);
            return false;
        }

        int rhsStart = operatorStart + assignmentOperator!.Length;
        int semicolonIndex = FindTopLevelSemicolon(code, rhsStart);
        if (semicolonIndex < 0)
        {
            errors.Add($"Parser local assignment '{attributeText} {assignmentOperator}' must end with a top-level semicolon.");
            output.Append(code, attributeStart, afterAttribute - attributeStart);
            return false;
        }

        string rhs = code.Substring(rhsStart, semicolonIndex - rhsStart).Trim();
        EmbeddedParserAttributeRewriteResult rewrittenRhs = RewriteCore(rhs, grammar, rule, locationKind, allowWrites: false);
        errors.AddRange(rewrittenRhs.Errors);
        string valueExpression = assignmentOperator == "="
            ? rewrittenRhs.Code
            : $"GetRequiredRuleLocal<{local.RawType}>(context, \"{Escape(root)}\") {assignmentOperator.Substring(0, assignmentOperator.Length - 1)} {rewrittenRhs.Code}";
        AppendSetLocal(output, local.RawType!, root, valueExpression);
        index = semicolonIndex + 1;
        return true;
    }

    /// <summary>
    /// Rewrites a supported standalone prefix increment or decrement of a current-rule local.
    /// </summary>
    private static bool TryRewritePrefixLocalUpdate(
        string code,
        ref int index,
        G4Grammar grammar,
        G4Rule rule,
        EmbeddedParserAttributeLocationKind locationKind,
        List<string> errors,
        StringBuilder output,
        Dictionary<string, TypedDeclaration> parameters,
        Dictionary<string, TypedDeclaration> locals)
    {
        string? updateOperator = StartsWith(code, index, "++") ? "++" : StartsWith(code, index, "--") ? "--" : null;
        if (updateOperator is null)
        {
            return false;
        }

        int attributeStart = SkipWhitespace(code, index + updateOperator.Length);
        if (attributeStart >= code.Length || code[attributeStart] != '$' || attributeStart + 1 >= code.Length || !IsIdentifierStart(code[attributeStart + 1]))
        {
            return false;
        }

        if (!IsStandaloneStatementStart(code, index))
        {
            return false;
        }

        int nameIndex = attributeStart + 1;
        string root = ReadIdentifier(code, ref nameIndex);
        int semicolon = SkipWhitespace(code, nameIndex);
        if (semicolon >= code.Length || code[semicolon] != ';')
        {
            errors.Add($"Parser local update '{updateOperator}${root}' is supported only as a standalone statement.");
            output.Append(code[index++]);
            return true;
        }

        string attributeText = "$" + root;
        if (!locals.TryGetValue(root, out TypedDeclaration? local) || string.IsNullOrWhiteSpace(local.RawType))
        {
            string message = parameters.ContainsKey(root)
                ? $"Parser parameter '{attributeText}' is read-only. Use a local if mutable state is needed."
                : $"Parser attribute write target '{attributeText}' does not resolve to a writable typed current-rule local.";
            errors.Add(message);
            output.Append(code, index, nameIndex - index);
            index = nameIndex;
            return true;
        }

        if (locationKind == EmbeddedParserAttributeLocationKind.Predicate)
        {
            errors.Add($"Parser local write '{attributeText}' is not supported in semantic predicates.");
            output.Append(code, index, nameIndex - index);
            index = nameIndex;
            return true;
        }

        AppendSetLocal(output, local.RawType!, root, $"GetRequiredRuleLocal<{local.RawType}>(context, \"{Escape(root)}\") {(updateOperator == "++" ? "+" : "-")} 1");
        index = semicolon + 1;
        return true;
    }

    /// <summary>
    /// Extracts declaration names and raw type prefixes from comma-separated rule parameter or local metadata.
    /// </summary>
    /// <param name="rawDeclarations">Raw declaration text.</param>
    /// <returns>Typed declarations keyed by ordinal metadata name.</returns>
    private static Dictionary<string, TypedDeclaration> ParseTypedDeclarations(string? rawDeclarations)
    {
        var declarations = new Dictionary<string, TypedDeclaration>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(rawDeclarations))
        {
            return declarations;
        }

        foreach (string declaration in SplitTopLevel(rawDeclarations!))
        {
            string prefix = RemoveTopLevelInitializer(declaration).Trim();
            string? name = GetTrailingIdentifier(prefix);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            string nameValue = name!;
            string? rawType = null;
            if (prefix.Length > nameValue.Length && prefix.EndsWith(nameValue, StringComparison.Ordinal))
            {
                int nameStart = prefix.Length - nameValue.Length;
                if (char.IsWhiteSpace(prefix[nameStart - 1]))
                {
                    rawType = prefix.Substring(0, nameStart).TrimEnd();
                }
            }

            declarations[nameValue] = new TypedDeclaration(nameValue, string.IsNullOrWhiteSpace(rawType) ? null : rawType, declaration);
        }

        return declarations;
    }

    /// <summary>
    /// Removes a top-level default initializer from a parameter declaration while preserving generic and literal text.
    /// </summary>
    private static string RemoveTopLevelInitializer(string declaration)
    {
        int angle = 0;
        int round = 0;
        int square = 0;
        int curly = 0;
        for (int index = 0; index < declaration.Length; index++)
        {
            switch (declaration[index])
            {
                case '<': angle++; break;
                case '>': angle = Math.Max(0, angle - 1); break;
                case '(': round++; break;
                case ')': round = Math.Max(0, round - 1); break;
                case '[': square++; break;
                case ']': square = Math.Max(0, square - 1); break;
                case '{': curly++; break;
                case '}': curly = Math.Max(0, curly - 1); break;
                case '=' when angle == 0 && round == 0 && square == 0 && curly == 0:
                    return declaration.Substring(0, index);
            }
        }

        return declaration;
    }

    /// <summary>
    /// Gets the final identifier from a declaration prefix.
    /// </summary>
    private static string? GetTrailingIdentifier(string text)
    {
        int end = text.Length - 1;
        while (end >= 0 && char.IsWhiteSpace(text[end])) end--;
        int start = end;
        while (start >= 0 && IsIdentifierPart(text[start])) start--;
        return end >= 0 && start < end ? text.Substring(start + 1, end - start) : null;
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

    /// <summary>
    /// Detects whether an attribute is preceded by the unsupported <c>ref</c> or <c>out</c> modifier.
    /// </summary>
    private static bool IsRefOrOutContext(string code, int start)
    {
        int previous = start - 1;
        while (previous >= 0 && char.IsWhiteSpace(code[previous])) previous--;
        int wordEnd = previous + 1;
        while (previous >= 0 && IsIdentifierPart(code[previous])) previous--;
        string previousWord = code.Substring(previous + 1, wordEnd - previous - 1);
        return string.Equals(previousWord, "ref", StringComparison.Ordinal) || string.Equals(previousWord, "out", StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads a supported assignment operator at the supplied index.
    /// </summary>
    private static string? ReadAssignmentOperator(string code, int index)
    {
        string[] operators = ["<<=", ">>=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "="];
        foreach (string op in operators)
        {
            if (StartsWith(code, index, op) && !StartsWith(code, index, "==") && !StartsWith(code, index, "=>"))
            {
                return op;
            }
        }

        return null;
    }

    /// <summary>
    /// Appends a typed local setter helper call using the declared raw local type.
    /// </summary>
    private static void AppendSetLocal(StringBuilder output, string rawType, string localName, string valueExpression)
    {
        output.Append("SetRequiredRuleLocal<")
            .Append(rawType)
            .Append(">(context, \"")
            .Append(Escape(localName))
            .Append("\", ")
            .Append(valueExpression)
            .Append(");");
    }

    /// <summary>
    /// Determines whether an attribute write starts at a conservative statement boundary.
    /// </summary>
    private static bool IsStandaloneStatementStart(string code, int start)
    {
        int previous = start - 1;
        while (previous >= 0 && char.IsWhiteSpace(code[previous])) previous--;
        return previous < 0 || code[previous] is ';' or '{' or '}';
    }

    /// <summary>
    /// Finds the next top-level semicolon without splitting strings, characters, comments, or nested expressions.
    /// </summary>
    private static int FindTopLevelSemicolon(string code, int start)
    {
        int round = 0;
        int square = 0;
        int curly = 0;
        int angle = 0;
        int index = start;
        var sink = new StringBuilder();
        while (index < code.Length)
        {
            int before = index;
            if (TryCopyTriviaOrLiteral(code, ref index, sink))
            {
                sink.Clear();
                if (index == before) index++;
                continue;
            }

            char value = code[index];
            switch (value)
            {
                case '(':
                    round++;
                    break;
                case ')':
                    round = Math.Max(0, round - 1);
                    break;
                case '[':
                    square++;
                    break;
                case ']':
                    square = Math.Max(0, square - 1);
                    break;
                case '{':
                    curly++;
                    break;
                case '}':
                    curly = Math.Max(0, curly - 1);
                    break;
                case '<':
                    angle++;
                    break;
                case '>':
                    angle = Math.Max(0, angle - 1);
                    break;
                case ';' when round == 0 && square == 0 && curly == 0 && angle == 0:
                    return index;
            }

            index++;
        }

        return -1;
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

    /// <summary>Tests whether an ordinal member name starts at an index and ends at an identifier boundary.</summary>
    private static bool IsMemberName(string text, int index, string value)
    {
        return StartsWith(text, index, value)
            && (index + value.Length >= text.Length || !IsIdentifierPart(text[index + value.Length]));
    }

    /// <summary>Determines whether a character can start the supported identifier form.</summary>
    private static bool IsIdentifierStart(char value) => value == '_' || char.IsLetter(value);

    /// <summary>Determines whether a character can continue the supported identifier form.</summary>
    private static bool IsIdentifierPart(char value) => value == '_' || char.IsLetterOrDigit(value);

    /// <summary>Escapes text for a generated C# string literal.</summary>
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>Stores assignment and list targets for one visible lexical label name.</summary>
    private sealed class RuleLabelTargets
    {
        /// <summary>Gets the last assignment-label target, when present.</summary>
        public RuleLabelTarget? Assignment { get; private set; }

        /// <summary>Gets every list-label target in lexical encounter order.</summary>
        public List<RuleLabelTarget> List { get; } = [];

        /// <summary>Adds a target to the namespace selected by the label operator.</summary>
        /// <param name="target">Referenced rule target.</param>
        /// <param name="isAdditive">Whether the target belongs to the list-label namespace.</param>
        public void Add(RuleLabelTarget target, bool isAdditive)
        {
            if (isAdditive)
            {
                List.Add(target);
            }
            else
            {
                Assignment = target;
            }
        }
    }

    /// <summary>Stores the target of one visible rule-reference label.</summary>
    private sealed class RuleLabelTarget
    {
        /// <summary>Initializes label target metadata.</summary>
        public RuleLabelTarget(string ruleName)
        {
            RuleName = ruleName;
        }

        /// <summary>Gets the referenced rule name.</summary>
        public string RuleName { get; }
    }

    /// <summary>Stores a current-rule parameter or local declaration that can be emitted as a typed helper access.</summary>
    private sealed class TypedDeclaration
    {
        /// <summary>Initializes typed declaration metadata.</summary>
        public TypedDeclaration(string name, string? rawType, string rawDeclaration)
        {
            Name = name;
            RawType = rawType;
            RawDeclaration = rawDeclaration;
        }

        /// <summary>Gets the declaration name.</summary>
        public string Name { get; }

        /// <summary>Gets the raw C# type text, when conservatively extractable.</summary>
        public string? RawType { get; }

        /// <summary>Gets the original raw declaration text.</summary>
        public string RawDeclaration { get; }
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
