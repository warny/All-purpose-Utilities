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
        var returns = ParseTypedDeclarations(rule.Returns);
        var parameters = ParseTypedDeclarations(rule.Parameters);
        var locals = ParseTypedDeclarations(rule.Locals.Count == 0 ? null : string.Join(", ", rule.Locals));
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
                if (!TryRewriteBareAttributeWrite(code, rule, locationKind, errors, output, parameters, locals, returns, labels, grammar, attributeStart, index, root, ref index))
                {
                    RewriteBareAttribute(code, rule, locationKind, errors, output, parameters, locals, returns, labels, attributeStart, index, root);
                }
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
                && earlyLabel.Assignments.Count == 0
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
                if (IsRefOrOutContext(code, attributeStart))
                {
                    errors.Add("ref/out parser attributes are not supported by the ANTLR-style transformer. Parser attribute writes are not supported.");
                }
                else if (string.Equals(root, rule.Name, StringComparison.Ordinal))
                {
                    errors.Add("Current-rule dotted return writes are not supported by the ANTLR-style transformer. Use bare '$returnName = ...' or SetRuleReturn(...) explicitly.");
                }
                else if (labels.TryGetValue(root, out RuleLabelTargets? writeLabel) && writeLabel.List.Count > 0 && writeLabel.Assignments.Count == 0)
                {
                    errors.Add("List-labeled rule-call return projections are read-only. Parser attribute writes are not supported.");
                }
                else if (labels.ContainsKey(root))
                {
                    errors.Add("Labeled rule-call return attributes are read-only. Parser attribute writes are not supported.");
                }
                else
                {
                    errors.Add($"Parser attribute writes are not supported for '{attributeText}'.");
                }

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
                errors.Add($"Dotted current-rule return attribute '{attributeText}' is not supported by the current-rule return transformer. Use bare '${returnName}' instead.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (!labels.TryGetValue(root, out RuleLabelTargets? label))
            {
                errors.Add($"Parser attribute root '{root}' is not the current rule name or a visible assignment rule-reference label.");
                output.Append(code, attributeStart, index - attributeStart);
                continue;
            }

            if (!TryRewriteAssignmentLabelReturn(grammar, locationKind, errors, output, root, returnName, attributeText, label, attributeStart, index, code))
            {
                output.Append(code, attributeStart, index - attributeStart);
            }

            continue;
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
    /// Rewrites one supported assignment-label child return read to the generated helper API.
    /// </summary>
    /// <param name="grammar">Grammar that owns the current parser rule.</param>
    /// <param name="locationKind">Embedded-code lifecycle location.</param>
    /// <param name="errors">Destination for deterministic diagnostics.</param>
    /// <param name="output">Destination for rewritten C#.</param>
    /// <param name="labelName">Visible assignment label name.</param>
    /// <param name="returnName">Requested child return name.</param>
    /// <param name="attributeText">Original attribute text for diagnostics.</param>
    /// <param name="label">Collected label targets for <paramref name="labelName"/>.</param>
    /// <param name="attributeStart">Start index of the attribute in <paramref name="code"/>.</param>
    /// <param name="attributeEnd">End index of the parsed attribute in <paramref name="code"/>.</param>
    /// <param name="code">Original embedded C# source.</param>
    /// <returns><c>true</c> when the caller should not copy the original attribute text.</returns>
    private static bool TryRewriteAssignmentLabelReturn(
        G4Grammar grammar,
        EmbeddedParserAttributeLocationKind locationKind,
        List<string> errors,
        StringBuilder output,
        string labelName,
        string returnName,
        string attributeText,
        RuleLabelTargets label,
        int attributeStart,
        int attributeEnd,
        string code)
    {
        if (label.List.Count > 0)
        {
            errors.Add($"List-label parser attribute '{attributeText}' is not supported. Use GetLabeledRuleCallReturns(context, \"{Escape(labelName)}\", \"{Escape(returnName)}\") explicitly for list labels.");
            return false;
        }

        if (locationKind == EmbeddedParserAttributeLocationKind.Init)
        {
            errors.Add($"Assignment label '{labelName}' is not available in @init. Labeled child rule-call returns can be read only after the child rule call succeeds.");
            return false;
        }

        if (label.Assignments.Count == 0)
        {
            errors.Add($"Parser attribute root '{labelName}' is not a visible assignment rule-reference label.");
            return false;
        }

        var targetRules = new List<G4Rule>();
        foreach (RuleLabelTarget assignment in label.Assignments)
        {
            G4Rule? targetRule = grammar.ParserRules.FirstOrDefault(candidate => string.Equals(candidate.Name, assignment.RuleName, StringComparison.Ordinal));
            if (targetRule is null)
            {
                errors.Add($"Token label '{labelName}' cannot be used as a parser rule-return attribute.");
                return false;
            }

            targetRules.Add(targetRule);
        }

        List<string> missingReturnRules = targetRules
            .Where(targetRule => !ParseTypedDeclarations(targetRule.Returns).ContainsKey(returnName))
            .Select(targetRule => targetRule.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (missingReturnRules.Count > 0)
        {
            string missingRules = string.Join("', '", missingReturnRules);
            string ruleText = missingReturnRules.Count == 1 ? $"parser rule '{missingRules}'" : $"parser rules '{missingRules}'";
            errors.Add($"Return '{returnName}' is not declared by every parser rule referenced by assignment label '{labelName}'. Missing on {ruleText}.");
            return false;
        }

        output.Append("GetRequiredLabeledRuleCallReturn(context, \"")
            .Append(Escape(labelName))
            .Append("\", \"")
            .Append(Escape(returnName))
            .Append("\")");
        return true;
    }


    /// <summary>
    /// Tries to rewrite a supported current-rule local write at the current parser attribute position.
    /// </summary>
    private static bool TryRewriteBareAttributeWrite(
        string code,
        G4Rule rule,
        EmbeddedParserAttributeLocationKind locationKind,
        List<string> errors,
        StringBuilder output,
        Dictionary<string, TypedDeclaration> parameters,
        Dictionary<string, TypedDeclaration> locals,
        Dictionary<string, TypedDeclaration> returns,
        Dictionary<string, RuleLabelTargets> labels,
        G4Grammar grammar,
        int attributeStart,
        int attributeEnd,
        string root,
        ref int index)
    {
        string attributeText = "$" + root;
        int previous = PreviousNonWhitespace(code, attributeStart - 1);
        bool hasPrefixIncrement = previous >= 1 && code[previous - 1] == '+' && code[previous] == '+';
        bool hasPrefixDecrement = previous >= 1 && code[previous - 1] == '-' && code[previous] == '-';
        int operatorStart = SkipWhitespace(code, attributeEnd);
        string? assignmentOperator = ReadAssignmentOperator(code, operatorStart);
        bool hasPostfixIncrement = StartsWith(code, operatorStart, "++");
        bool hasPostfixDecrement = StartsWith(code, operatorStart, "--");
        bool isWrite = assignmentOperator is not null || hasPostfixIncrement || hasPostfixDecrement || hasPrefixIncrement || hasPrefixDecrement;
        bool isRefOrOut = IsRefOrOutContext(code, attributeStart);
        if (!isWrite && !isRefOrOut)
        {
            return false;
        }

        if (isRefOrOut)
        {
            errors.Add("ref/out parser attributes are not supported by the ANTLR-style transformer. Parser attribute writes are not supported.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        int declarationCount = (parameters.ContainsKey(root) ? 1 : 0) + (locals.ContainsKey(root) ? 1 : 0) + (returns.ContainsKey(root) ? 1 : 0);
        if (declarationCount > 1)
        {
            errors.Add($"Parser attribute '{attributeText}' is ambiguous because the current rule declares multiple attributes with this name. Use explicit helper APIs.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        if (parameters.ContainsKey(root))
        {
            errors.Add($"Parser parameter '{attributeText}' is read-only. Use a local or explicit helper API for mutable state.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        if (labels.ContainsKey(root))
        {
            errors.Add($"Bare parser attribute '{attributeText}' is a label access and is read-only.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        bool isReturn = returns.TryGetValue(root, out TypedDeclaration? returnDeclaration);
        TypedDeclaration? local = null;
        if (!isReturn && !locals.TryGetValue(root, out local))
        {
            errors.Add($"Bare parser attribute '{attributeText}' does not resolve to a current-rule parameter, local, or return.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        if (locationKind == EmbeddedParserAttributeLocationKind.Predicate)
        {
            errors.Add(isReturn ? "Parser return writes are not supported in semantic predicates." : "Parser local writes are not supported in semantic predicates.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        if (isReturn && locationKind == EmbeddedParserAttributeLocationKind.Init)
        {
            errors.Add("Parser return writes are not supported in @init. Use @after or explicit runtime APIs when the return frame is available.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        TypedDeclaration declaration = isReturn ? returnDeclaration! : local!;
        if (string.IsNullOrWhiteSpace(declaration.RawType))
        {
            errors.Add($"Bare parser attribute '{attributeText}' cannot be typed because declaration '{declaration.RawDeclaration}' does not expose a raw type.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        if (hasPrefixIncrement || hasPrefixDecrement)
        {
            if (!IsStandalonePrefixUpdate(code, previous - 1, operatorStart))
            {
                errors.Add("Increment/decrement parser attributes are supported only as standalone statements.");
                output.Append(code, attributeStart, attributeEnd - attributeStart);
                return true;
            }

            RemoveTrailingOperator(output, code, previous - 1, attributeStart);
            int statementEnd = SkipWhitespace(code, operatorStart);
            AppendAttributeUpdate(output, isReturn, declaration.RawType!, root, hasPrefixIncrement ? "+" : "-", "1");
            index = statementEnd;
            return true;
        }

        if (hasPostfixIncrement || hasPostfixDecrement)
        {
            int afterOperator = operatorStart + 2;
            if (!IsStandalonePostfixUpdate(code, attributeStart, afterOperator))
            {
                errors.Add("Increment/decrement parser attributes are supported only as standalone statements.");
                output.Append(code, attributeStart, attributeEnd - attributeStart);
                return true;
            }

            AppendAttributeUpdate(output, isReturn, declaration.RawType!, root, hasPostfixIncrement ? "+" : "-", "1");
            index = afterOperator;
            return true;
        }

        int rhsStart = operatorStart + assignmentOperator!.Length;
        int rhsEnd = FindStatementTerminator(code, rhsStart);
        if (rhsEnd < 0)
        {
            errors.Add($"Parser attribute write '{attributeText}' must be a complete statement ending with ';'.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        string rhs = code.Substring(rhsStart, rhsEnd - rhsStart).Trim();
        if (ContainsTopLevelAssignment(rhs))
        {
            errors.Add("Nested parser attribute assignment expressions are not supported by the ANTLR-style transformer.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        EmbeddedParserAttributeRewriteResult rhsResult = Rewrite(rhs, grammar, rule, locationKind);
        errors.AddRange(rhsResult.Errors);
        if (assignmentOperator == "=")
        {
            AppendAttributeSet(output, isReturn, declaration.RawType!, root, rhsResult.Code);
        }
        else
        {
            AppendAttributeUpdate(output, isReturn, declaration.RawType!, root, assignmentOperator.Substring(0, assignmentOperator.Length - 1), rhsResult.Code);
        }

        index = rhsEnd;
        return true;
    }

    /// <summary>
    /// Rewrites one bare current-rule parameter or local attribute, or records a deterministic validation error.
    /// </summary>
    private static void RewriteBareAttribute(
        string code,
        G4Rule rule,
        EmbeddedParserAttributeLocationKind locationKind,
        List<string> errors,
        StringBuilder output,
        Dictionary<string, TypedDeclaration> parameters,
        Dictionary<string, TypedDeclaration> locals,
        Dictionary<string, TypedDeclaration> returns,
        Dictionary<string, RuleLabelTargets> labels,
        int attributeStart,
        int attributeEnd,
        string root)
    {
        string attributeText = "$" + root;
        if (IsWriteContext(code, attributeStart, attributeEnd))
        {
            errors.Add($"Parser attribute writes are not supported for '{attributeText}'. Use explicit parameter/local helpers such as SetRuleLocal(context, name, value).");
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
        bool hasReturn = returns.TryGetValue(root, out TypedDeclaration? returnDeclaration);
        if ((hasParameter ? 1 : 0) + (hasLocal ? 1 : 0) + (hasReturn ? 1 : 0) > 1)
        {
            errors.Add($"Bare parser attribute '{attributeText}' is ambiguous because current rule '{rule.Name}' declares multiple attributes named '{root}'.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return;
        }

        TypedDeclaration? declaration = hasParameter ? parameter : hasLocal ? local : returnDeclaration;
        if (declaration is null)
        {
            string message = labels.ContainsKey(root)
                ? $"Bare parser attribute '{attributeText}' is a label access and is not supported. Use '$" + root + ".returnName' for declared child rule returns."
                : $"Bare parser attribute '{attributeText}' does not resolve to a current-rule parameter, local, or return.";
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

        output.Append(hasParameter ? "GetRequiredRuleParameter<" : hasLocal ? "GetRequiredRuleLocal<" : "GetRequiredRuleReturn<")
            .Append(declaration.RawType)
            .Append(">(context, \"")
            .Append(Escape(root))
            .Append("\")");
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


    /// <summary>Reads a supported assignment operator at the supplied index.</summary>
    private static string? ReadAssignmentOperator(string code, int index)
    {
        string[] operators = ["<<=", ">>=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "="];
        return operators.FirstOrDefault(op => StartsWith(code, index, op) && IsAssignmentOperatorStart(code, index, op));
    }

    /// <summary>Determines whether an operator token is an assignment rather than a comparison or lambda operator.</summary>
    private static bool IsAssignmentOperatorStart(string code, int index, string op)
    {
        if (op != "=")
        {
            return true;
        }

        if (StartsWith(code, index, "==") || StartsWith(code, index, "=>"))
        {
            return false;
        }

        char previous = index > 0 ? code[index - 1] : '\0';
        return previous != '=' && previous != '!' && previous != '<' && previous != '>';
    }

    /// <summary>Appends a typed current-rule local or return setter helper call.</summary>
    private static void AppendAttributeSet(StringBuilder output, bool isReturn, string rawType, string name, string value)
    {
        output.Append(isReturn ? "SetRequiredRuleReturn<" : "SetRequiredRuleLocal<").Append(rawType).Append(">(context, \"").Append(Escape(name)).Append("\", ").Append(value).Append(')');
    }

    /// <summary>Appends a typed current-rule local or return getter/operator/setter helper call.</summary>
    private static void AppendAttributeUpdate(StringBuilder output, bool isReturn, string rawType, string name, string op, string value)
    {
        output.Append(isReturn ? "SetRequiredRuleReturn<" : "SetRequiredRuleLocal<").Append(rawType).Append(">(context, \"").Append(Escape(name)).Append("\", ")
            .Append(isReturn ? "GetRequiredRuleReturn<" : "GetRequiredRuleLocal<")
            .Append(rawType).Append(">(context, \"").Append(Escape(name)).Append("\") ").Append(op).Append(' ').Append(value).Append(')');
    }

    /// <summary>Finds the semicolon that terminates the current top-level embedded C# statement.</summary>
    private static int FindStatementTerminator(string code, int index)
    {
        int round = 0;
        int square = 0;
        int curly = 0;
        while (index < code.Length)
        {
            var ignored = new StringBuilder();
            if (TryCopyTriviaOrLiteral(code, ref index, ignored)) continue;
            switch (code[index])
            {
                case '(' : round++; break;
                case ')' : round = Math.Max(0, round - 1); break;
                case '[' : square++; break;
                case ']' : square = Math.Max(0, square - 1); break;
                case '{' : curly++; break;
                case '}' : curly = Math.Max(0, curly - 1); break;
                case ';' when round == 0 && square == 0 && curly == 0: return index;
            }
            index++;
        }
        return -1;
    }

    /// <summary>Conservatively detects unsupported nested assignment expressions in a right-hand side.</summary>
    private static bool ContainsTopLevelAssignment(string text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            var ignored = new StringBuilder();
            if (TryCopyTriviaOrLiteral(text, ref index, ignored)) { index--; continue; }
            if (ReadAssignmentOperator(text, index) is not null) return true;
        }
        return false;
    }

    /// <summary>Determines whether a parser attribute is preceded by ref or out.</summary>
    private static bool IsRefOrOutContext(string code, int start)
    {
        int previous = PreviousNonWhitespace(code, start - 1);
        int wordEnd = previous + 1;
        while (previous >= 0 && IsIdentifierPart(code[previous])) previous--;
        string previousWord = code.Substring(previous + 1, wordEnd - previous - 1);
        return string.Equals(previousWord, "ref", StringComparison.Ordinal) || string.Equals(previousWord, "out", StringComparison.Ordinal);
    }

    /// <summary>Checks statement boundaries for a prefix increment or decrement local write.</summary>
    private static bool IsStandalonePrefixUpdate(string code, int operatorStart, int afterAttribute)
    {
        int before = PreviousNonWhitespace(code, operatorStart - 1);
        int after = SkipWhitespace(code, afterAttribute);
        return (before < 0 || code[before] == ';' || code[before] == '{' || code[before] == '}') && after < code.Length && code[after] == ';';
    }

    /// <summary>Checks statement boundaries for a postfix increment or decrement local write.</summary>
    private static bool IsStandalonePostfixUpdate(string code, int attributeStart, int afterOperator)
    {
        int before = PreviousNonWhitespace(code, attributeStart - 1);
        int after = SkipWhitespace(code, afterOperator);
        return (before < 0 || code[before] == ';' || code[before] == '{' || code[before] == '}') && after < code.Length && code[after] == ';';
    }

    /// <summary>Removes a previously copied prefix increment or decrement operator and whitespace from the output.</summary>
    private static void RemoveTrailingOperator(StringBuilder output, string code, int operatorStart, int attributeStart)
    {
        int length = attributeStart - operatorStart;
        if (length > 0 && output.Length >= length)
        {
            output.Length -= length;
        }
    }

    /// <summary>Finds the previous non-whitespace character index.</summary>
    private static int PreviousNonWhitespace(string code, int index)
    {
        while (index >= 0 && char.IsWhiteSpace(code[index])) index--;
        return index;
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
        /// <summary>Gets every assignment-label target in lexical encounter order.</summary>
        public List<RuleLabelTarget> Assignments { get; } = [];

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
                Assignments.Add(target);
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
