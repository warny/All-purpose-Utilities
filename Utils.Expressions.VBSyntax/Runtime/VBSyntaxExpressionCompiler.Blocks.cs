using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Utils.Parser.Runtime;

namespace Utils.Expressions.VBSyntax.Runtime;

public sealed partial class VBSyntaxExpressionCompiler
{
    // ── Block compilation ─────────────────────────────────────────────────────

    /// <summary>
    /// Compiles a block instruction by sequencing child expressions.
    /// </summary>
    private static Expression? CompileBlock(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        List<Expression> expressions = children
            .Where(static c => c is not null)
            .Cast<Expression>()
            .ToList();

        context.BlockScope.TryGetValue(nav.Node, out List<ParameterExpression>? blockVars);
        List<ParameterExpression> vars = blockVars ?? [];

        if (expressions.Count == 0)
            return vars.Count > 0 ? Expression.Block(vars, Expression.Empty()) : Expression.Empty();

        if (expressions.Count == 1 && vars.Count == 0)
            return expressions[0];

        return Expression.Block(vars, expressions);
    }

    // ── Identifier / member / invocation ─────────────────────────────────────

    /// <summary>
    /// Compiles an <c>identifier_part</c> grammar rule.
    /// Delegates to <see cref="CompileIdentifierExpression"/> for chained access.
    /// Returns <see langword="null"/> when inside a <c>method_declaration</c> body — the
    /// declaration handler re-compiles the body from source text and does not use child results.
    /// </summary>
    private static Expression? CompileIdentifierPart(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        if (context.InsideMethodBody)
            return null;

        string expressionText = GetNodeSourceText(context, nav).Trim();
        if (string.IsNullOrWhiteSpace(expressionText))
            return children.FirstOrDefault(static c => c is not null);

        char first = expressionText[0];
        if (!(char.IsLetter(first) || first == '_'))
            return children.FirstOrDefault(static c => c is not null);

        return CompileIdentifierExpression(expressionText, context);
    }

    /// <summary>
    /// Compiles a chained identifier/member/invocation expression from source text.
    /// In VB, both method calls and array indexers use <c>(…)</c>.
    /// </summary>
    private static Expression CompileIdentifierExpression(
        string expressionText,
        CompilationContext context)
    {
        int index = 0;
        SkipWhitespace(expressionText, ref index);
        string identifier = ReadIdentifier(expressionText, ref index);

        // Try to resolve the root as a known type (for static member access)
        Type? staticType = TryResolveVBTypeToken(identifier);
        Expression? current = staticType is null
            ? ResolveIdentifier(context, identifier)
            : null;

        while (index < expressionText.Length)
        {
            SkipWhitespace(expressionText, ref index);
            if (index >= expressionText.Length) break;

            // Member access: .MemberName
            if (expressionText[index] == '.')
            {
                index++;
                string memberName = ReadIdentifier(expressionText, ref index);
                SkipWhitespace(expressionText, ref index);

                if (index < expressionText.Length && expressionText[index] == '(')
                {
                    string argText = ReadBalancedContent(expressionText, ref index, '(', ')');
                    Expression[] args = [.. ParseInvocationArguments(argText, context)];

                    if (staticType is null)
                    {
                        current = ExpressionEx.CreateMemberExpression(
                            current!, memberName, BindingFlags.Public | BindingFlags.IgnoreCase, args);
                    }
                    else
                    {
                        MethodInfo? method = SelectBestMethod(
                            staticType
                                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                                .Where(m => string.Equals(m.Name, memberName, StringComparison.OrdinalIgnoreCase)),
                            args);
                        if (method is null)
                            throw new MissingMemberException(memberName);
                        current = Expression.Call(method, ConvertArgumentsForParameters(method.GetParameters(), args));
                    }

                    staticType = null;
                    continue;
                }

                current = staticType is null
                    ? ExpressionEx.CreateMemberExpression(current!, memberName, BindingFlags.Public | BindingFlags.IgnoreCase)
                    : ExpressionEx.CreateStaticExpression(staticType, memberName, BindingFlags.Public | BindingFlags.IgnoreCase);
                staticType = null;
                continue;
            }

            // Invocation or indexer: both use (…) in VB
            if (expressionText[index] == '(')
            {
                string argText = ReadBalancedContent(expressionText, ref index, '(', ')');
                Expression[] args = [.. ParseInvocationArguments(argText, context)];

                if (current is ConstantExpression { Value: MethodInfo mi })
                {
                    current = Expression.Call(mi, ConvertArgumentsForParameters(mi.GetParameters(), args));
                    continue;
                }

                if (current is ConstantExpression { Value: MethodInfo[] methodGroup } &&
                    TryBuildMethodCall(methodGroup, args, out Expression? groupCall))
                {
                    current = groupCall;
                    continue;
                }

                // If the type has an Item indexer, prefer that when args look like indices
                if (current is not null)
                {
                    PropertyInfo? indexer = current.Type.GetProperty("Item",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (indexer is not null && args.Length > 0)
                    {
                        current = ExpressionEx.CreateMemberExpression(
                            current, "Item", BindingFlags.Public | BindingFlags.IgnoreCase, args);
                        continue;
                    }
                }

                MethodInfo? invoke = current!.Type.GetMethod("Invoke",
                    BindingFlags.Public | BindingFlags.Instance);
                if (invoke is null)
                    throw new InvalidOperationException($"Expression '{current}' is not invokable.");

                current = Expression.Invoke(current, ConvertArgumentsForParameters(invoke.GetParameters(), args));
                continue;
            }

            break;
        }

        return current
            ?? throw new InvalidOperationException(
                $"Unable to resolve identifier expression '{expressionText}'.");
    }

    // ── Identifier resolution ─────────────────────────────────────────────────

    /// <summary>
    /// Resolves a VB identifier from the compilation symbol table or runtime context.
    /// </summary>
    private static Expression ResolveIdentifier(CompilationContext context, string identifier)
    {
        if (context.Symbols.TryGetValue(identifier, out Expression? expression))
            return expression;

        if (context.RuntimeContext is not null &&
            context.RuntimeContext.TryGet(identifier, out object? value))
        {
            return value switch
            {
                Expression valueExpression    => valueExpression,
                Delegate   valueDelegate      => Expression.Constant(valueDelegate),
                _                             => Expression.Constant(value, value?.GetType() ?? typeof(object)),
            };
        }

        throw new InvalidOperationException($"Unknown identifier '{identifier}'.");
    }

    // ── Object creation: New Type(args) ───────────────────────────────────────

    /// <summary>
    /// Compiles a <c>New Type(args)</c> object creation expression.
    /// </summary>
    private static Expression? CompileObjectCreation(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        string source = GetNodeSourceText(context, nav).Trim();

        // Source starts with "New "
        if (!source.StartsWith("New", StringComparison.OrdinalIgnoreCase))
            return children.FirstOrDefault(static c => c is not null);

        string body = source[3..].Trim();
        int parenIdx = body.IndexOf('(');
        string typeName = parenIdx >= 0 ? body[..parenIdx].Trim() : body;
        Type targetType = ResolveVBType(typeName);

        Expression[] args = [];
        if (parenIdx >= 0)
        {
            int closeIdx = FindMatchingDelimiter(body, parenIdx, '(', ')');
            string argText = body[(parenIdx + 1)..closeIdx];
            args = [.. ParseInvocationArguments(argText, context)];
        }

        return BuildObjectCreation(targetType, args);
    }

    // ── Argument parsing utilities ────────────────────────────────────────────

    /// <summary>
    /// Parses a comma-separated argument list from raw source text.
    /// </summary>
    private static List<Expression> ParseInvocationArguments(
        string argumentSegment,
        CompilationContext context)
    {
        var arguments = new List<Expression>();
        foreach (string chunk in SplitTopLevel(argumentSegment, ','))
        {
            string trimmed = chunk.Trim();
            if (trimmed.Length == 0) continue;

            arguments.Add(context.RuntimeContext is null
                ? context.Compiler.Compile(trimmed, context.Symbols)
                : context.Compiler.Compile(trimmed, context.RuntimeContext));
        }

        return arguments;
    }

    /// <summary>
    /// Splits source text on a separator while keeping nested parentheses intact.
    /// </summary>
    private static IEnumerable<string> SplitTopLevel(string content, char separator)
    {
        var builder = new StringBuilder();
        int depth = 0;
        foreach (char c in content)
        {
            if (c == '(') depth++;
            else if (c == ')') depth--;

            if (c == separator && depth == 0)
            {
                yield return builder.ToString();
                builder.Clear();
                continue;
            }

            builder.Append(c);
        }

        if (builder.Length > 0)
            yield return builder.ToString();
    }

    // ── Method / constructor resolution ───────────────────────────────────────

    /// <summary>
    /// Tries to select the best overload from a method group.
    /// </summary>
    private static bool TryBuildMethodCall(
        IReadOnlyList<MethodInfo> methods,
        Expression[] arguments,
        out Expression? callExpression)
    {
        callExpression = null;
        MethodInfo? selected = SelectBestMethod(methods, arguments);
        if (selected is null) return false;
        callExpression = Expression.Call(
            selected, ConvertArgumentsForParameters(selected.GetParameters(), arguments));
        return true;
    }

    /// <summary>
    /// Selects the most compatible method candidate for the supplied arguments.
    /// </summary>
    private static MethodInfo? SelectBestMethod(
        IEnumerable<MethodInfo> candidates,
        Expression[] arguments)
    {
        MethodInfo? best = null;
        int bestScore = int.MaxValue;
        foreach (MethodInfo candidate in candidates)
        {
            int? score = CalculateCompatibilityScore(candidate.GetParameters(), arguments);
            if (score is null || score.Value >= bestScore) continue;
            bestScore = score.Value;
            best = candidate;
        }

        return best;
    }

    /// <summary>
    /// Builds a constructor call for the supplied type and arguments.
    /// </summary>
    private static Expression BuildObjectCreation(Type targetType, Expression[] arguments)
    {
        ConstructorInfo[] ctors = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (ctors.Length == 0 && arguments.Length == 0 && targetType.IsValueType)
            return Expression.New(targetType);

        ConstructorInfo? selected = SelectBestConstructor(ctors, arguments);
        if (selected is null)
            throw new InvalidOperationException(
                $"No compatible public constructor found on '{targetType.FullName}'.");

        return Expression.New(selected, ConvertArgumentsForParameters(selected.GetParameters(), arguments));
    }

    /// <summary>
    /// Selects the most compatible constructor for the supplied arguments.
    /// </summary>
    private static ConstructorInfo? SelectBestConstructor(
        IEnumerable<ConstructorInfo> constructors,
        Expression[] arguments)
    {
        ConstructorInfo? best = null;
        int bestScore = int.MaxValue;
        foreach (ConstructorInfo ctor in constructors)
        {
            int? score = CalculateCompatibilityScore(ctor.GetParameters(), arguments);
            if (score is null || score.Value >= bestScore) continue;
            bestScore = score.Value;
            best = ctor;
        }

        return best;
    }

    /// <summary>
    /// Calculates a compatibility score for a method/constructor parameter list against arguments.
    /// Returns <see langword="null"/> when incompatible.
    /// </summary>
    private static int? CalculateCompatibilityScore(
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<Expression> arguments)
    {
        if (parameters.Count != arguments.Count) return null;

        int score = 0;
        for (int i = 0; i < parameters.Count; i++)
        {
            Type paramType = parameters[i].ParameterType;
            Type argType   = arguments[i].Type;
            if (argType == paramType) continue;
            if (paramType.IsAssignableFrom(argType)) { score += 1; continue; }
            if (IsNumericType(paramType) && IsNumericType(argType)) { score += 2; continue; }
            if (arguments[i] is ConstantExpression { Value: null } && !paramType.IsValueType) { score += 3; continue; }
            return null;
        }

        return score;
    }

    /// <summary>
    /// Converts invocation arguments to the expected parameter types.
    /// </summary>
    private static Expression[] ConvertArgumentsForParameters(
        IReadOnlyList<ParameterInfo> parameters,
        Expression[] arguments)
    {
        if (parameters.Count != arguments.Length)
            throw new InvalidOperationException(
                "Invocation argument count does not match parameter count.");

        Expression[] converted = new Expression[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
            converted[i] = ConvertIfNeeded(arguments[i], parameters[i].ParameterType);
        return converted;
    }

    /// <summary>
    /// Converts invocation arguments to lambda parameter types.
    /// </summary>
    private static Expression[] ConvertArgumentsForParameters(
        IReadOnlyList<ParameterExpression> parameters,
        Expression[] arguments)
    {
        if (parameters.Count != arguments.Length)
            throw new InvalidOperationException(
                "Invocation argument count does not match parameter count.");

        Expression[] converted = new Expression[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
            converted[i] = ConvertIfNeeded(arguments[i], parameters[i].Type);
        return converted;
    }

    // ── Text scanning utilities ───────────────────────────────────────────────

    /// <summary>
    /// Reads an identifier token (letters, digits, underscore) from <paramref name="text"/>
    /// starting at <paramref name="index"/> and advances the index.
    /// </summary>
    private static string ReadIdentifier(string text, ref int index)
    {
        int start = index;
        while (index < text.Length &&
               (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
        {
            index++;
        }

        if (index == start)
            throw new InvalidOperationException("Identifier expected.");

        return text[start..index];
    }

    /// <summary>
    /// Advances <paramref name="index"/> past any whitespace characters in <paramref name="text"/>.
    /// </summary>
    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;
    }

    /// <summary>
    /// Reads the content between matching delimiters and advances the index past the closing one.
    /// </summary>
    private static string ReadBalancedContent(
        string text,
        ref int index,
        char open,
        char close)
    {
        int openIdx  = index;
        int closeIdx = FindMatchingDelimiter(text, openIdx, open, close);
        index = closeIdx + 1;
        return text[(openIdx + 1)..closeIdx];
    }

    /// <summary>
    /// Finds the index of the closing delimiter that matches the opening one at
    /// <paramref name="openIndex"/>.
    /// </summary>
    private static int FindMatchingDelimiter(
        string text,
        int openIndex,
        char open,
        char close)
    {
        int depth = 0;
        for (int i = openIndex; i < text.Length; i++)
        {
            if (text[i] == open)  depth++;
            else if (text[i] == close)
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        throw new InvalidOperationException("Unable to find matching delimiter.");
    }

    // ── Type-token helper ─────────────────────────────────────────────────────

    /// <summary>
    /// Tries to resolve a source token as a VB type name without throwing.
    /// </summary>
    private static Type? TryResolveVBTypeToken(string token)
    {
        try { return ResolveVBType(token); }
        catch { return null; }
    }
}
