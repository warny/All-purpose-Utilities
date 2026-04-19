using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Collections;
using Utils.Objects;
using Utils.Parser.Runtime;

namespace Utils.Expressions.CSyntax.Runtime;

/// <summary>
/// Compiles C-like parse trees into LINQ expression trees by using
/// <see cref="ParseTreeCompiler{TContext, TResult}"/>.
/// </summary>
public sealed partial class CSyntaxExpressionCompiler
{
    /// <summary>
    /// Compiles a block instruction by sequencing child expressions.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Block expression or empty expression.</returns>
    private static Expression? CompileBlock(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        List<Expression> expressions = children.Where(static c => c is not null).Cast<Expression>().ToList();
        context.BlockScope.TryGetValue(nav.Node, out List<ParameterExpression>? blockVars);
        List<ParameterExpression> vars = blockVars ?? [];

        if (expressions.Count == 0)
        {
            return vars.Count > 0 ? Expression.Block(vars, Expression.Empty()) : Expression.Empty();
        }

        if (expressions.Count == 1 && vars.Count == 0)
        {
            return expressions[0];
        }

        return Expression.Block(vars, expressions);
    }

    /// <summary>
    /// Folds a binary rule where all operators are expected to be identical.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <param name="allowedOperators">Allowed operator token texts.</param>
    /// <param name="factory">Binary expression factory.</param>
    /// <returns>Compiled folded expression.</returns>
    private static Expression? FoldBinary(
        ParseTreeNavigator nav,
        IReadOnlyList<Expression?> children,
        IReadOnlyCollection<string> allowedOperators,
        Func<string, Expression, Expression, Expression> factory)
    {
        return FoldBinary(nav, children, factory, allowedOperators);
    }

    /// <summary>
    /// Folds a binary chain rule using operator tokens found between operands.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <param name="factory">Operator-aware binary expression factory.</param>
    /// <param name="allowedOperators">Optional operator whitelist.</param>
    /// <returns>Compiled folded expression.</returns>
    private static Expression? FoldBinary(
        ParseTreeNavigator nav,
        IReadOnlyList<Expression?> children,
        Func<string, Expression, Expression, Expression> factory,
        IReadOnlyCollection<string>? allowedOperators = null)
    {
        if (nav.RawChildren is null || nav.RawChildren.Count == 0)
        {
            return null;
        }

        List<Expression> operands = children
            .Where(static child => child is not null)
            .Cast<Expression>()
            .ToList();
        if (operands.Count == 0)
        {
            return null;
        }

        if (operands.Count == 1)
        {
            return operands[0];
        }

        HashSet<string> allowed = allowedOperators is null
            ? new HashSet<string>(GetAllBinaryOperators(), StringComparer.Ordinal)
            : new HashSet<string>(allowedOperators, StringComparer.Ordinal);
        List<string> operators = CollectOperatorTokens(nav.RawChildren.Skip(1), allowed)
            .ToList();
        if (operators.Count == 0)
        {
            return operands[0];
        }

        Expression current = operands[0];
        int pairCount = Math.Min(operators.Count, operands.Count - 1);
        for (int i = 0; i < pairCount; i++)
        {
            string op = operators[i];
            Expression right = operands[i + 1];
            current = factory(op, current, right);
        }

        return current;
    }

    private static IReadOnlyList<string> GetAllBinaryOperators() =>
        ["||", "&&", "==", "!=", "<", "<=", ">", ">=", "<<", ">>", "+", "-", "*", "/", "%", "**"];

    /// <summary>
    /// Collects operator tokens from a sequence of parse nodes.
    /// </summary>
    /// <param name="nodes">Nodes to inspect recursively.</param>
    /// <param name="allowedOperators">Set of accepted operator tokens.</param>
    /// <returns>Operator tokens found in traversal order.</returns>
    private static IEnumerable<string> CollectOperatorTokens(IEnumerable<ParseNode> nodes, HashSet<string> allowedOperators)
    {
        foreach (ParseNode node in nodes)
        {
            if (node is LexerNode lexerNode)
            {
                if (allowedOperators.Contains(lexerNode.Token.Text))
                {
                    yield return lexerNode.Token.Text;
                }

                continue;
            }

            if (node is ParserNode parserNode)
            {
                foreach (string token in CollectOperatorTokens(parserNode.Children, allowedOperators))
                {
                    yield return token;
                }
            }
        }
    }

    /// <summary>
    /// Finds a compiled child expression around the given raw-child index.
    /// </summary>
    /// <param name="rawChildren">Raw parse children list.</param>
    /// <param name="compiledChildren">Compiled child expressions list.</param>
    /// <param name="startIndex">Start index in the raw-children list.</param>
    /// <param name="direction">Search direction: -1 for left, +1 for right.</param>
    /// <returns>The first compiled expression found in the selected direction, or <c>null</c>.</returns>
    private static Expression? FindCompiledChildNear(
        IReadOnlyList<ParseNode> rawChildren,
        IReadOnlyList<Expression?> compiledChildren,
        int startIndex,
        int direction)
    {
        for (int index = startIndex; index >= 0 && index < rawChildren.Count; index += direction)
        {
            if (compiledChildren[index] is Expression expression)
            {
                return expression;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the source substring corresponding to a parse-tree node span.
    /// </summary>
    /// <param name="context">Compilation context containing source text.</param>
    /// <param name="nav">Node navigator.</param>
    /// <returns>Source substring for the node span.</returns>
    private static string GetNodeSourceText(CompilationContext context, ParseTreeNavigator nav)
    {
        if (string.IsNullOrEmpty(context.SourceText))
        {
            return string.Empty;
        }

        int start = nav.Node.Span.Position;
        int length = nav.Node.Span.Length;
        if (start < 0 || length <= 0 || start + length > context.SourceText.Length)
        {
            return string.Empty;
        }

        return context.SourceText.Substring(start, length);
    }

    /// <summary>
    /// Compiles a chained identifier/member/indexer/invocation expression.
    /// </summary>
    /// <param name="expressionText">Expression text to compile.</param>
    /// <param name="context">Current compilation context.</param>
    /// <returns>Compiled expression.</returns>
    private static Expression CompileIdentifierExpression(string expressionText, CompilationContext context)
    {
        int index = 0;
        SkipWhitespace(expressionText, ref index);
        string identifier = ReadIdentifier(expressionText, ref index);
        Type? staticDeclaringType = TryResolveNativeTypeToken(identifier, context.ImportedNamespaces);
        Expression? current = staticDeclaringType is null
            ? ResolveIdentifier(context, identifier)
            : null;

        while (index < expressionText.Length)
        {
            SkipWhitespace(expressionText, ref index);
            if (index >= expressionText.Length)
            {
                break;
            }

            if (expressionText[index] == '.')
            {
                index++;
                string memberName = ReadIdentifier(expressionText, ref index);
                SkipWhitespace(expressionText, ref index);
                if (index < expressionText.Length && expressionText[index] == '(')
                {
                    string argumentText = ReadBalancedContent(expressionText, ref index, '(', ')');
                    Expression[] arguments = [.. ParseInvocationArguments(argumentText, context)];
                    if (staticDeclaringType is null)
                    {
                        current = ExpressionEx.CreateMemberExpression(current!, memberName, BindingFlags.Public | BindingFlags.IgnoreCase, arguments);
                    }
                    else
                    {
                        MethodInfo? method = SelectBestMethod(
                            staticDeclaringType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                .Where(methodInfo => string.Equals(methodInfo.Name, memberName, StringComparison.OrdinalIgnoreCase)),
                            arguments);
                        if (method is null)
                        {
                            throw new MissingMemberException(memberName);
                        }

                        current = Expression.Call(method, ConvertArgumentsForParameters(method.GetParameters(), arguments));
                    }
                    staticDeclaringType = null;
                    continue;
                }

                current = staticDeclaringType is null
                    ? ExpressionEx.CreateMemberExpression(current!, memberName, BindingFlags.Public | BindingFlags.IgnoreCase)
                    : ExpressionEx.CreateStaticExpression(staticDeclaringType, memberName, BindingFlags.Public | BindingFlags.IgnoreCase);
                staticDeclaringType = null;
                continue;
            }

            if (expressionText[index] == '[')
            {
                string argumentText = ReadBalancedContent(expressionText, ref index, '[', ']');
                Expression[] arguments = [.. ParseInvocationArguments(argumentText, context)];
                current = ExpressionEx.CreateMemberExpression(current!, "Item", BindingFlags.Public | BindingFlags.IgnoreCase, arguments);
                continue;
            }

            if (expressionText[index] == '(')
            {
                string argumentText = ReadBalancedContent(expressionText, ref index, '(', ')');
                Expression[] arguments = [.. ParseInvocationArguments(argumentText, context)];

                if (current is ConstantExpression { Value: MethodInfo methodInfo })
                {
                    current = Expression.Call(methodInfo, ConvertArgumentsForParameters(methodInfo.GetParameters(), arguments));
                    continue;
                }

                if (current is ConstantExpression { Value: MethodInfo[] methodGroup } &&
                    TryBuildMethodCall(methodGroup, arguments, out Expression? methodGroupCall))
                {
                    current = methodGroupCall;
                    continue;
                }

                MethodInfo? invokeMethod = current!.Type.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
                if (invokeMethod is null)
                {
                    throw new InvalidOperationException($"Expression '{current}' is not invokable.");
                }

                current = Expression.Invoke(current, ConvertArgumentsForParameters(invokeMethod.GetParameters(), arguments));
                continue;
            }

            break;
        }

        return current ?? throw new InvalidOperationException($"Unable to resolve identifier expression '{expressionText}'.");
    }

    /// <summary>
    /// Tries to resolve a native type token without throwing when the token is unknown.
    /// </summary>
    /// <param name="token">Type token candidate.</param>
    /// <param name="importedNamespaces">Imported namespaces.</param>
    /// <returns>Resolved type when recognized; otherwise <c>null</c>.</returns>
    private static Type? TryResolveNativeTypeToken(string token, IReadOnlyList<string> importedNamespaces)
    {
        try
        {
            return ResolveNativeType(token, importedNamespaces);
        }
        catch (Exception) when (true)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads an identifier token from source text.
    /// </summary>
    /// <param name="text">Source text.</param>
    /// <param name="index">Read index (updated after read).</param>
    /// <returns>Identifier text.</returns>
    private static string ReadIdentifier(string text, ref int index)
    {
        int start = index;
        while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
        {
            index++;
        }

        if (index == start)
        {
            throw new InvalidOperationException("Identifier expected.");
        }

        return text[start..index];
    }

    /// <summary>
    /// Advances an index while whitespace characters are found.
    /// </summary>
    /// <param name="text">Source text.</param>
    /// <param name="index">Index to advance.</param>
    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    /// <summary>
    /// Reads balanced content between delimiters and advances the index past the closing delimiter.
    /// </summary>
    /// <param name="text">Source text.</param>
    /// <param name="index">Current index at opening delimiter.</param>
    /// <param name="openDelimiter">Opening delimiter.</param>
    /// <param name="closeDelimiter">Closing delimiter.</param>
    /// <returns>Inner content between delimiters.</returns>
    private static string ReadBalancedContent(string text, ref int index, char openDelimiter, char closeDelimiter)
    {
        int openIndex = index;
        int closeIndex = FindMatchingDelimiter(text, openIndex, openDelimiter, closeDelimiter);
        index = closeIndex + 1;
        return text[(openIndex + 1)..closeIndex];
    }

    /// <summary>
    /// Parses invocation argument text into compiled expressions.
    /// </summary>
    /// <param name="argumentSegment">Raw argument segment.</param>
    /// <param name="context">Compilation context.</param>
    /// <returns>Compiled argument expressions.</returns>
    private static List<Expression> ParseInvocationArguments(string argumentSegment, CompilationContext context)
    {
        var arguments = new List<Expression>();
        foreach (string chunk in SplitTopLevel(argumentSegment, ','))
        {
            string trimmed = chunk.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            arguments.Add(context.RuntimeContext is null
                ? context.Compiler.Compile(trimmed, context.Symbols)
                : context.Compiler.Compile(trimmed, context.RuntimeContext));
        }

        return arguments;
    }

    /// <summary>
    /// Splits source text on a separator while keeping nested scopes intact.
    /// </summary>
    /// <param name="content">Source content to split.</param>
    /// <param name="separator">Top-level separator character.</param>
    /// <returns>Split chunks preserving nested scopes.</returns>
    private static IEnumerable<string> SplitTopLevel(string content, char separator)
    {
        var builder = new StringBuilder();
        int parenDepth = 0;
        int bracketDepth = 0;
        int braceDepth = 0;
        foreach (char c in content)
        {
            switch (c)
            {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth--;
                    break;
            }

            if (c == separator && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
            {
                yield return builder.ToString();
                builder.Clear();
                continue;
            }

            builder.Append(c);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    /// <summary>
    /// Compiles a source snippet while preserving the current compilation symbols.
    /// </summary>
    /// <param name="source">Source snippet to compile.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="additionalSymbols">Optional symbols that override the current symbol table.</param>
    /// <returns>Compiled expression.</returns>
    private static Expression CompileSubExpression(string source, CompilationContext context, IReadOnlyDictionary<string, Expression>? additionalSymbols)
    {
        if (context.RuntimeContext is null)
        {
            CSyntaxCompilerContext localContext = new();
            foreach (KeyValuePair<string, Expression> symbol in context.Symbols)
            {
                localContext.Set(symbol.Key, symbol.Value);
            }

            if (additionalSymbols is not null)
            {
                foreach (KeyValuePair<string, Expression> symbol in additionalSymbols)
                {
                    localContext.Set(symbol.Key, symbol.Value);
                }
            }

            return context.Compiler.CompileSource(source, localContext);
        }

        CSyntaxCompilerContext derivedContext = new();
        foreach (KeyValuePair<string, object?> symbol in context.RuntimeContext.Symbols)
        {
            derivedContext.Set(symbol.Key, symbol.Value);
        }

        foreach (KeyValuePair<string, Expression> symbol in context.Symbols)
        {
            derivedContext.Set(symbol.Key, symbol.Value);
        }

        if (additionalSymbols is not null)
        {
            foreach (KeyValuePair<string, Expression> symbol in additionalSymbols)
            {
                derivedContext.Set(symbol.Key, symbol.Value);
            }
        }

        return context.Compiler.Compile(source, derivedContext);
    }

    /// <summary>
    /// Merges two symbol dictionaries while letting additional symbols override base symbols.
    /// </summary>
    /// <param name="baseSymbols">Base symbol table.</param>
    /// <param name="additionalSymbols">Additional symbol table.</param>
    /// <returns>Merged symbol dictionary.</returns>
    private static Dictionary<string, Expression> MergeSymbols(
        IReadOnlyDictionary<string, Expression> baseSymbols,
        IReadOnlyDictionary<string, Expression>? additionalSymbols)
    {
        Dictionary<string, Expression> merged = new(baseSymbols, StringComparer.Ordinal);
        if (additionalSymbols is null)
        {
            return merged;
        }

        foreach (KeyValuePair<string, Expression> symbol in additionalSymbols)
        {
            merged[symbol.Key] = symbol.Value;
        }

        return merged;
    }

    /// <summary>
    /// Extracts the parenthesized loop header and body segment from a loop statement.
    /// </summary>
    /// <param name="loopSource">Full loop statement source.</param>
    /// <returns>Tuple of header and body source segments.</returns>
    private static (string Header, string Body) ExtractLoopHeaderAndBody(string loopSource)
    {
        int openParenthesis = loopSource.IndexOf('(');
        if (openParenthesis < 0)
        {
            throw new InvalidOperationException("Loop header is missing.");
        }

        int closeParenthesis = FindMatchingDelimiter(loopSource, openParenthesis, '(', ')');
        string header = loopSource[(openParenthesis + 1)..closeParenthesis];
        string remaining = loopSource[(closeParenthesis + 1)..].TrimStart();
        string body = remaining;
        if (remaining.StartsWith('{'))
        {
            int bodyEnd = FindMatchingDelimiter(remaining, 0, '{', '}');
            body = remaining[..(bodyEnd + 1)];
        }
        else
        {
            int bodyEnd = FindTopLevelStatementTerminator(remaining);
            if (bodyEnd >= 0)
            {
                body = remaining[..(bodyEnd + 1)];
            }
        }

        return (header, body);
    }

    /// <summary>
    /// Finds the first top-level statement terminator index in source.
    /// </summary>
    /// <param name="source">Source to inspect.</param>
    /// <returns>Index of the first top-level semicolon; otherwise <c>-1</c>.</returns>
    private static int FindTopLevelStatementTerminator(string source)
    {
        int parenDepth = 0;
        int bracketDepth = 0;
        int braceDepth = 0;
        for (int i = 0; i < source.Length; i++)
        {
            switch (source[i])
            {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth--;
                    break;
                case ';':
                    if (parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    {
                        return i;
                    }
                    break;
            }
        }

        return -1;
    }

    /// <summary>
    /// Extracts a source slice that starts at a specific instruction keyword.
    /// </summary>
    /// <param name="source">Source that may contain surrounding code (for example a lambda).</param>
    /// <param name="keyword">Instruction keyword to locate.</param>
    /// <returns>Source slice beginning at the requested keyword when found; otherwise original source.</returns>
    private static string ExtractInstructionSource(string source, string keyword)
    {
        Match keywordMatch = Regex.Match(source, $@"\b{Regex.Escape(keyword)}\s*\(");
        if (!keywordMatch.Success)
        {
            return source;
        }

        return source[keywordMatch.Index..];
    }

    /// <summary>
    /// Removes an optional trailing semicolon from loop body source.
    /// </summary>
    /// <param name="bodySource">Raw loop body source.</param>
    /// <returns>Normalized loop body source.</returns>
    private static string NormalizeLoopBodySource(string bodySource)
    {
        string trimmed = bodySource.Trim();
        return trimmed.EndsWith(';') ? trimmed[..^1].TrimEnd() : trimmed;
    }

    /// <summary>
    /// Normalizes common for-loop iterator shorthand forms (<c>i++</c>, <c>--i</c>) into assignable expressions.
    /// </summary>
    /// <param name="stepExpression">Raw step expression text.</param>
    /// <returns>Normalized step expression text.</returns>
    private static string NormalizeForIteratorStep(string stepExpression)
    {
        string trimmed = stepExpression.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        Match postfix = Regex.Match(trimmed, @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?<op>\+\+|--)$");
        if (postfix.Success)
        {
            string name = postfix.Groups["name"].Value;
            string arithmeticOperator = postfix.Groups["op"].Value == "++" ? "+" : "-";
            return $"{name} = {name} {arithmeticOperator} 1";
        }

        Match prefix = Regex.Match(trimmed, @"^(?<op>\+\+|--)\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)$");
        if (prefix.Success)
        {
            string name = prefix.Groups["name"].Value;
            string arithmeticOperator = prefix.Groups["op"].Value == "++" ? "+" : "-";
            return $"{name} = {name} {arithmeticOperator} 1";
        }

        return trimmed;
    }

    /// <summary>
    /// Finds the matching closing delimiter for an opening delimiter.
    /// </summary>
    /// <param name="text">Text to inspect.</param>
    /// <param name="openIndex">Opening delimiter index.</param>
    /// <param name="openDelimiter">Opening delimiter.</param>
    /// <param name="closeDelimiter">Closing delimiter.</param>
    /// <returns>Index of the matching closing delimiter.</returns>
    private static int FindMatchingDelimiter(string text, int openIndex, char openDelimiter, char closeDelimiter)
    {
        int depth = 0;
        for (int index = openIndex; index < text.Length; index++)
        {
            if (text[index] == openDelimiter)
            {
                depth++;
            }
            else if (text[index] == closeDelimiter)
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        throw new InvalidOperationException("Unable to find matching delimiter.");
    }

    /// <summary>
    /// Normalizes two numeric operands to a common type: preserves the type when both are
    /// identical, otherwise widens both to <see cref="double"/>.
    /// </summary>
    private static (Expression Left, Expression Right) NormalizeNumericPair(Expression left, Expression right)
    {
        if (!IsNumericType(left.Type))
            throw new NotSupportedException($"Expression '{left}' is not numeric.");
        if (!IsNumericType(right.Type))
            throw new NotSupportedException($"Expression '{right}' is not numeric.");
        if (left.Type == right.Type)
            return (left, right);
        return (EnsureNumeric(left), EnsureNumeric(right));
    }

    /// <summary>
    /// Ensures an expression can be safely used as a void branch.
    /// </summary>
    /// <param name="expression">Branch expression.</param>
    /// <returns>Void-compatible expression.</returns>
    private static Expression EnsureVoidCompatible(Expression expression)
        => expression.Type == typeof(void) ? expression : Expression.Block(expression, Expression.Empty());

    /// <summary>
    /// Resolves the iterator element type for an enumerable type.
    /// </summary>
    /// <param name="enumerableType">Enumerable CLR type.</param>
    /// <returns>Resolved element type.</returns>
    private static Type ResolveEnumerableElementType(Type enumerableType)
    {
        if (enumerableType.IsArray)
        {
            return enumerableType.GetElementType()!;
        }

        Type? genericEnumerable = enumerableType
            .GetInterfaces()
            .FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (genericEnumerable is not null)
        {
            return genericEnumerable.GetGenericArguments()[0];
        }

        return typeof(object);
    }

    /// <summary>
    /// Converts invocation arguments to delegate parameter types.
    /// </summary>
    /// <param name="method">Delegate method signature.</param>
    /// <param name="arguments">Invocation arguments.</param>
    /// <returns>Converted arguments.</returns>
    private static Expression[] ConvertArgumentsForDelegate(MethodInfo method, Expression[] arguments)
        => ConvertArgumentsForParameters(method.GetParameters(), arguments);

    /// <summary>
    /// Converts invocation arguments to lambda parameter types.
    /// </summary>
    /// <param name="parameters">Lambda parameters.</param>
    /// <param name="arguments">Invocation arguments.</param>
    /// <returns>Converted arguments.</returns>
    private static Expression[] ConvertArgumentsForParameters(IReadOnlyList<ParameterExpression> parameters, Expression[] arguments)
    {
        if (parameters.Count != arguments.Length)
        {
            throw new InvalidOperationException("Invocation argument count does not match parameter count.");
        }

        Expression[] converted = new Expression[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            converted[i] = ConvertIfNeeded(arguments[i], parameters[i].Type);
        }

        return converted;
    }

    /// <summary>
    /// Converts invocation arguments to method parameter types.
    /// </summary>
    /// <param name="parameters">Method parameters.</param>
    /// <param name="arguments">Invocation arguments.</param>
    /// <returns>Converted arguments.</returns>
    private static Expression[] ConvertArgumentsForParameters(IReadOnlyList<ParameterInfo> parameters, Expression[] arguments)
    {
        bool hasParamArray = parameters.Count > 0
            && parameters[^1].IsDefined(typeof(ParamArrayAttribute), false);

        if (!hasParamArray && parameters.Count != arguments.Length)
        {
            throw new InvalidOperationException("Invocation argument count does not match parameter count.");
        }

        if (hasParamArray)
        {
            int fixedCount = parameters.Count - 1;
            if (arguments.Length < fixedCount)
            {
                throw new InvalidOperationException("Invocation argument count does not match parameter count.");
            }

            Expression[] convertedArguments = new Expression[parameters.Count];
            for (int i = 0; i < fixedCount; i++)
            {
                convertedArguments[i] = ConvertIfNeeded(arguments[i], parameters[i].ParameterType);
            }

            Type elementType = parameters[^1].ParameterType.GetElementType()
                ?? throw new InvalidOperationException("Invalid params parameter declaration.");
            if (arguments.Length == parameters.Count)
            {
                Expression lastArgument = arguments[^1];
                Type paramArrayType = parameters[^1].ParameterType;
                if (lastArgument.Type != paramArrayType && lastArgument.Type.IsArray)
                {
                    Type sourceElementType = lastArgument.Type.GetElementType()
                        ?? throw new InvalidOperationException("Invalid array argument.");
                    if (elementType.IsAssignableFrom(sourceElementType))
                    {
                        MethodInfo castMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .First(methodInfo => methodInfo.Name == nameof(Enumerable.Cast) && methodInfo.GetParameters().Length == 1)
                            .MakeGenericMethod(elementType);
                        MethodInfo toArrayMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .First(methodInfo => methodInfo.Name == nameof(Enumerable.ToArray) && methodInfo.GetParameters().Length == 1)
                            .MakeGenericMethod(elementType);
                        Expression castExpression = Expression.Call(castMethod, lastArgument);
                        convertedArguments[^1] = Expression.Call(toArrayMethod, castExpression);
                        return convertedArguments;
                    }
                }
            }

            Expression[] paramsArguments = arguments
                .Skip(fixedCount)
                .Select(argument => ConvertIfNeeded(argument, elementType))
                .ToArray();
            convertedArguments[^1] = Expression.NewArrayInit(elementType, paramsArguments);
            return convertedArguments;
        }

        Expression[] converted = new Expression[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            converted[i] = ConvertIfNeeded(arguments[i], parameters[i].ParameterType);
        }

        return converted;
    }

    /// <summary>
    /// Tries to resolve a static method call from a qualified source name.
    /// </summary>
    /// <param name="calleeName">Qualified callee name (for example, <c>Math.Max</c>).</param>
    /// <param name="arguments">Invocation arguments.</param>
    /// <param name="callExpression">Resolved call expression.</param>
    /// <returns><c>true</c> when a compatible static method is found; otherwise <c>false</c>.</returns>
    private static bool TryResolveStaticMethodCall(string calleeName, Expression[] arguments, out Expression? callExpression)
    {
        callExpression = null;
        int separatorIndex = calleeName.LastIndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= calleeName.Length - 1)
        {
            return false;
        }

        string typeName = calleeName[..separatorIndex].Trim();
        string methodName = calleeName[(separatorIndex + 1)..].Trim();
        Type? type = Type.GetType(typeName, false)
            ?? AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(typeName, false)).FirstOrDefault(static t => t is not null);
        if (type is null)
        {
            return false;
        }

        MethodInfo? method = SelectBestMethod(
            type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(methodInfo => methodInfo.Name == methodName),
            arguments);
        if (method is null)
        {
            return false;
        }

        callExpression = Expression.Call(method, ConvertArgumentsForParameters(method.GetParameters(), arguments));
        return true;
    }

    /// <summary>
    /// Tries to build a method call for an overload set.
    /// </summary>
    /// <param name="methods">Candidate methods sharing the same source symbol.</param>
    /// <param name="arguments">Invocation arguments.</param>
    /// <param name="callExpression">Resolved call expression.</param>
    /// <returns><c>true</c> when a compatible method was selected; otherwise <c>false</c>.</returns>
    private static bool TryBuildMethodCall(IReadOnlyList<MethodInfo> methods, Expression[] arguments, out Expression? callExpression)
    {
        callExpression = null;
        MethodInfo? selectedMethod = SelectBestMethod(methods, arguments);
        if (selectedMethod is null)
        {
            return false;
        }

        callExpression = Expression.Call(selectedMethod, ConvertArgumentsForParameters(selectedMethod.GetParameters(), arguments));
        return true;
    }

    /// <summary>
    /// Selects the best method candidate for provided invocation arguments.
    /// </summary>
    /// <param name="candidates">Method candidates.</param>
    /// <param name="arguments">Invocation arguments.</param>
    /// <returns>Best method candidate or <c>null</c> when no compatible candidate exists.</returns>
    private static MethodInfo? SelectBestMethod(IEnumerable<MethodInfo> candidates, Expression[] arguments)
    {
        MethodInfo? bestMethod = null;
        int bestScore = int.MaxValue;
        foreach (MethodInfo candidate in candidates)
        {
            MethodInfo method = candidate;
            if (candidate.IsGenericMethodDefinition)
            {
                if (!TryCloseGenericMethod(candidate, arguments, out MethodInfo? closedMethod))
                {
                    continue;
                }

                method = closedMethod;
            }

            ParameterInfo[] parameters = method.GetParameters();
            int? score = CalculateMethodCompatibilityScore(parameters, arguments);
            if (score is null)
            {
                continue;
            }

            if (score.Value < bestScore)
            {
                bestScore = score.Value;
                bestMethod = method;
            }
        }

        return bestMethod;
    }

    /// <summary>
    /// Tries to construct a generic method from invocation argument types.
    /// </summary>
    /// <param name="genericMethod">Generic method definition.</param>
    /// <param name="arguments">Invocation arguments.</param>
    /// <param name="closedMethod">Constructed generic method when inference succeeds.</param>
    /// <returns><c>true</c> when inference succeeds; otherwise <c>false</c>.</returns>
    private static bool TryCloseGenericMethod(MethodInfo genericMethod, IReadOnlyList<Expression> arguments, out MethodInfo? closedMethod)
    {
        closedMethod = null;
        ParameterInfo[] parameters = genericMethod.GetParameters();
        bool hasParamArray = parameters.Length > 0 && parameters[^1].IsDefined(typeof(ParamArrayAttribute), false);
        if (!hasParamArray && parameters.Length != arguments.Count)
        {
            return false;
        }

        if (hasParamArray && arguments.Count < parameters.Length - 1)
        {
            return false;
        }

        Dictionary<Type, Type> inferredTypes = new Dictionary<Type, Type>();
        int fixedCount = hasParamArray ? parameters.Length - 1 : parameters.Length;
        for (int i = 0; i < fixedCount; i++)
        {
            if (!TryInferGenericType(parameters[i].ParameterType, arguments[i].Type, inferredTypes))
            {
                return false;
            }
        }

        if (hasParamArray)
        {
            Type paramsElementType = parameters[^1].ParameterType.GetElementType()
                ?? throw new InvalidOperationException("Invalid params parameter declaration.");
            for (int i = fixedCount; i < arguments.Count; i++)
            {
                if (!TryInferGenericType(paramsElementType, arguments[i].Type, inferredTypes))
                {
                    return false;
                }
            }
        }

        Type[] genericParameters = genericMethod.GetGenericArguments();
        Type[] resolved = new Type[genericParameters.Length];
        for (int i = 0; i < genericParameters.Length; i++)
        {
            if (!inferredTypes.TryGetValue(genericParameters[i], out Type? inferredType))
            {
                return false;
            }

            resolved[i] = inferredType;
        }

        closedMethod = genericMethod.MakeGenericMethod(resolved);
        return true;
    }

    /// <summary>
    /// Tries to infer generic type arguments from a parameter/argument pair.
    /// </summary>
    /// <param name="parameterType">Parameter type (possibly containing generic parameters).</param>
    /// <param name="argumentType">Argument type.</param>
    /// <param name="inferredTypes">Accumulated inferred generic type arguments.</param>
    /// <returns><c>true</c> when inference is compatible; otherwise <c>false</c>.</returns>
    private static bool TryInferGenericType(Type parameterType, Type argumentType, IDictionary<Type, Type> inferredTypes)
    {
        if (parameterType.IsGenericParameter)
        {
            if (inferredTypes.TryGetValue(parameterType, out Type? current))
            {
                return current == argumentType;
            }

            inferredTypes[parameterType] = argumentType;
            return true;
        }

        if (parameterType.IsArray)
        {
            if (!argumentType.IsArray)
            {
                return false;
            }

            Type parameterElementType = parameterType.GetElementType()!;
            Type argumentElementType = argumentType.GetElementType()!;
            return TryInferGenericType(parameterElementType, argumentElementType, inferredTypes);
        }

        if (parameterType.IsGenericType)
        {
            Type parameterGenericDefinition = parameterType.GetGenericTypeDefinition();
            Type[] parameterGenericArguments = parameterType.GetGenericArguments();
            Type[]? argumentGenericArguments = TryGetGenericArguments(argumentType, parameterGenericDefinition);
            if (argumentGenericArguments is null || argumentGenericArguments.Length != parameterGenericArguments.Length)
            {
                return false;
            }

            for (int i = 0; i < parameterGenericArguments.Length; i++)
            {
                if (!TryInferGenericType(parameterGenericArguments[i], argumentGenericArguments[i], inferredTypes))
                {
                    return false;
                }
            }

            return true;
        }

        return parameterType.IsAssignableFrom(argumentType);
    }

    /// <summary>
    /// Gets generic arguments for a target generic definition from a type, its interfaces, or its base types.
    /// </summary>
    /// <param name="sourceType">Source type to inspect.</param>
    /// <param name="genericDefinition">Generic type definition to match.</param>
    /// <returns>Generic arguments when a match is found; otherwise <c>null</c>.</returns>
    private static Type[]? TryGetGenericArguments(Type sourceType, Type genericDefinition)
    {
        if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == genericDefinition)
        {
            return sourceType.GetGenericArguments();
        }

        foreach (Type interfaceType in sourceType.GetInterfaces())
        {
            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == genericDefinition)
            {
                return interfaceType.GetGenericArguments();
            }
        }

        Type? baseType = sourceType.BaseType;
        while (baseType is not null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == genericDefinition)
            {
                return baseType.GetGenericArguments();
            }

            baseType = baseType.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Calculates a compatibility score for a method candidate against provided arguments.
    /// </summary>
    /// <param name="parameters">Method parameters.</param>
    /// <param name="arguments">Invocation arguments.</param>
    /// <returns>Compatibility score (lower is better) or <c>null</c> when incompatible.</returns>
    private static int? CalculateMethodCompatibilityScore(IReadOnlyList<ParameterInfo> parameters, IReadOnlyList<Expression> arguments)
    {
        bool hasParamArray = parameters.Count > 0
            && parameters[^1].IsDefined(typeof(ParamArrayAttribute), false);
        if (!hasParamArray && parameters.Count != arguments.Count)
        {
            return null;
        }
        if (hasParamArray && arguments.Count < parameters.Count - 1)
        {
            return null;
        }

        int score = hasParamArray ? 5 : 0;
        int fixedCount = hasParamArray ? parameters.Count - 1 : parameters.Count;
        for (int i = 0; i < fixedCount; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            Type argumentType = arguments[i].Type;

            if (argumentType == parameterType)
            {
                continue;
            }

            if (parameterType.IsAssignableFrom(argumentType))
            {
                score += 1;
                if (parameterType == typeof(object) && argumentType.IsArray)
                {
                    score += 10;
                }
                continue;
            }

            if (IsNumericType(parameterType) && IsNumericType(argumentType))
            {
                score += 2;
                continue;
            }

            if (arguments[i] is ConstantExpression { Value: null } && !parameterType.IsValueType)
            {
                score += 3;
                continue;
            }

            return null;
        }

        if (hasParamArray)
        {
            Type elementType = parameters[^1].ParameterType.GetElementType()
                ?? throw new InvalidOperationException("Invalid params parameter declaration.");
            for (int i = fixedCount; i < arguments.Count; i++)
            {
                Type argumentType = arguments[i].Type;
                if (argumentType == elementType)
                {
                    continue;
                }

                if (elementType.IsAssignableFrom(argumentType))
                {
                    score += 1;
                    continue;
                }

                if (IsNumericType(elementType) && IsNumericType(argumentType))
                {
                    score += 2;
                    continue;
                }

                if (arguments[i] is ConstantExpression { Value: null } && !elementType.IsValueType)
                {
                    score += 3;
                    continue;
                }

                return null;
            }
        }

        return score;
    }

    /// <summary>
    /// Parses a method declaration signature from the parse tree.
    /// </summary>
    /// <param name="nav">Method declaration navigator.</param>
    /// <param name="context">Compilation context.</param>
    /// <returns>Parsed method signature model.</returns>
    private static MethodSignature ParseMethodSignature(ParseTreeNavigator nav, CompilationContext context)
    {
        string declarationText = GetNodeSourceText(context, nav);
        string isolatedDeclaration = ExtractMethodDeclarationText(declarationText);
        Match regexMatch = Regex.Match(
            isolatedDeclaration,
            @"^\s*(?<mods>(?:public|private|protected|internal|static|virtual|override|abstract|async)\s+)*(?<ret>[A-Za-z_][A-Za-z0-9_\.]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<params>[^)]*)\)\s*\{(?<body>.*)\}\s*$",
            RegexOptions.Singleline);
        if (regexMatch.Success)
        {
            string modifiers = regexMatch.Groups["mods"].Value;
            bool isPublicFromRegex = modifiers.Contains("public", StringComparison.Ordinal);
            string returnTypeNameFromRegex = regexMatch.Groups["ret"].Value;
            string nameFromRegex = regexMatch.Groups["name"].Value;
            string parametersSegment = regexMatch.Groups["params"].Value;
            string bodyFromRegex = regexMatch.Groups["body"].Value.Trim();

            List<ParameterExpression> parametersFromRegex = new();
            Dictionary<string, Expression> symbolsFromRegex = new(StringComparer.Ordinal);
            foreach (string parameterText in SplitTopLevel(parametersSegment, ',').Select(static s => s.Trim()).Where(static s => s.Length > 0))
            {
                string[] pieces = parameterText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (pieces.Length < 2)
                {
                    continue;
                }

                Type parameterType = ResolveNativeType(pieces[0]);
                string parameterName = pieces[1];
                ParameterExpression parameter = Expression.Parameter(parameterType, parameterName);
                parametersFromRegex.Add(parameter);
                symbolsFromRegex[parameterName] = parameter;
            }

            return new MethodSignature(
                nameFromRegex,
                isPublicFromRegex,
                ResolveNativeType(returnTypeNameFromRegex),
                parametersFromRegex,
                symbolsFromRegex,
                bodyFromRegex);
        }

        if (nav.RawChildren is null)
        {
            return ParseMethodSignatureFromWholeSource(context.SourceText);
        }

        bool isPublic = nav.Descendants().Any(static n => n.RuleName == "PUBLIC");

        ParseTreeNavigator? returnTypeNode = nav.Children().FirstOrDefault(static n => n.RuleName == "type_reference");
        ParseTreeNavigator? nameNode = nav.Children().FirstOrDefault(static n => n.RuleName == "identifier");
        ParseTreeNavigator? bodyNode = nav.Children().FirstOrDefault(static n => n.RuleName == "block_instruction");
        if (returnTypeNode is null || nameNode is null || bodyNode is null)
        {
            return ParseMethodSignatureFromWholeSource(context.SourceText);
        }

        string returnTypeName = FlattenNodeText(returnTypeNode.Node);
        string name = FlattenNodeText(nameNode.Node);
        string body = GetNodeSourceText(context, bodyNode);
        if (body.StartsWith("{", StringComparison.Ordinal) && body.EndsWith("}", StringComparison.Ordinal) && body.Length >= 2)
        {
            body = body[1..^1].Trim();
        }

        List<ParameterExpression> parameters = new();
        Dictionary<string, Expression> parameterSymbols = new(StringComparer.Ordinal);
        foreach (ParseTreeNavigator parameterNode in nav.Descendants("parameter"))
        {
            ParseTreeNavigator? parameterTypeNode = parameterNode.Children().FirstOrDefault(static n => n.RuleName == "type_reference");
            ParseTreeNavigator? parameterNameNode = parameterNode.Children().FirstOrDefault(static n => n.RuleName == "identifier");
            if (parameterTypeNode is null || parameterNameNode is null)
            {
                continue;
            }

            Type parameterType = ResolveNativeType(FlattenNodeText(parameterTypeNode.Node));
            string parameterName = FlattenNodeText(parameterNameNode.Node);
            ParameterExpression parameter = Expression.Parameter(parameterType, parameterName);
            parameters.Add(parameter);
            parameterSymbols[parameterName] = parameter;
        }

        Type returnType = ResolveNativeType(returnTypeName);
        if (parameters.Count == 0 && context.SourceText.Length > 0)
        {
            return ParseMethodSignatureFromWholeSource(context.SourceText);
        }

        return new MethodSignature(name, isPublic, returnType, parameters, parameterSymbols, body);
    }

    /// <summary>
    /// Parses the first method declaration found in a full source text.
    /// </summary>
    /// <param name="sourceText">Full source text.</param>
    /// <returns>Parsed method signature model.</returns>
    private static MethodSignature ParseMethodSignatureFromWholeSource(string sourceText)
    {
        Match match = Regex.Match(
            sourceText,
            @"(?<mods>(?:public|private|protected|internal|static|virtual|override|abstract|async)\s+)*(?<ret>[A-Za-z_][A-Za-z0-9_\.]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<params>[^)]*)\)\s*\{(?<body>[^}]*)\}",
            RegexOptions.Singleline);
        if (!match.Success)
        {
            throw new NotSupportedException("Unable to parse method declaration signature.");
        }

        string modifiers = match.Groups["mods"].Value;
        bool isPublic = modifiers.Contains("public", StringComparison.Ordinal);
        string returnTypeName = match.Groups["ret"].Value;
        string name = match.Groups["name"].Value;
        string body = match.Groups["body"].Value.Trim();

        List<ParameterExpression> parameters = new();
        Dictionary<string, Expression> parameterSymbols = new(StringComparer.Ordinal);
        foreach (string parameterText in SplitTopLevel(match.Groups["params"].Value, ',').Select(static s => s.Trim()).Where(static s => s.Length > 0))
        {
            string[] pieces = parameterText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length < 2)
            {
                continue;
            }

            Type parameterType = ResolveNativeType(pieces[0]);
            string parameterName = pieces[1];
            ParameterExpression parameter = Expression.Parameter(parameterType, parameterName);
            parameters.Add(parameter);
            parameterSymbols[parameterName] = parameter;
        }

        return new MethodSignature(name, isPublic, ResolveNativeType(returnTypeName), parameters, parameterSymbols, body);
    }

    /// <summary>
    /// Extracts the declaration part for a method source snippet.
    /// </summary>
    /// <param name="text">Raw method snippet.</param>
    /// <returns>Method declaration without trailing source text.</returns>
    private static string ExtractMethodDeclarationText(string text)
    {
        int openBrace = text.IndexOf('{');
        if (openBrace < 0)
        {
            return text;
        }

        int depth = 0;
        for (int i = openBrace; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return text[..(i + 1)];
                }
            }
        }

        return text;
    }

    /// <summary>
    /// Flattens token text under a parse node.
    /// </summary>
    /// <param name="node">Parse node to flatten.</param>
    /// <returns>Concatenated token text.</returns>
    private static string FlattenNodeText(ParseNode node)
    {
        return node switch
        {
            LexerNode lexerNode => lexerNode.Token.Text,
            ParserNode parserNode => string.Concat(parserNode.Children.Select(FlattenNodeText)),
            _ => string.Empty,
        };
    }

    /// <summary>Known namespace prefixes tried during unqualified type resolution.</summary>
    private static readonly string[] DefaultNamespaces =
    [
        "System", "System.Collections", "System.Collections.Generic",
        "System.Linq", "System.Text",
    ];

    /// <summary>Bidirectional mapping between CLR primitive types and C# keyword aliases.</summary>
    private static readonly DoubleIndexedDictionary<Type, string> TypeAliases = new(
    [
        (typeof(void), "void"),
        (typeof(bool), "bool"),
        (typeof(byte), "byte"),
        (typeof(char), "char"),
        (typeof(decimal), "decimal"),
        (typeof(double), "double"),
        (typeof(float), "float"),
        (typeof(int), "int"),
        (typeof(long), "long"),
        (typeof(object), "object"),
        (typeof(sbyte), "sbyte"),
        (typeof(short), "short"),
        (typeof(string), "string"),
        (typeof(uint), "uint"),
        (typeof(ulong), "ulong"),
        (typeof(ushort), "ushort"),
    ]);

    /// <summary>
    /// Resolves a native CLR type from a C-style type token.
    /// </summary>
    /// <param name="typeName">Type token, may include generic syntax (e.g. <c>IEnumerable&lt;int&gt;</c>).</param>
    /// <param name="importedNamespaces">Optional extra namespaces to search.</param>
    /// <returns>Resolved CLR type.</returns>
    private static Type ResolveNativeType(string typeName, IReadOnlyList<string>? importedNamespaces = null)
    {
        int arrayRank = 0;
        while (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            arrayRank++;
            typeName = typeName[..^2].TrimEnd();
        }

        Type resolvedType = TypeAliases.Right.TryGetValue(typeName, out Type? aliasType)
            ? aliasType
            : ResolveComplexType(typeName, importedNamespaces);
        while (arrayRank-- > 0)
        {
            resolvedType = resolvedType.MakeArrayType();
        }

        return resolvedType;
    }

    /// <summary>
    /// Resolves complex CLR type names (generic or qualified).
    /// </summary>
    /// <param name="typeName">Type token to resolve.</param>
    /// <param name="importedNamespaces">Optional extra namespaces to search.</param>
    /// <returns>Resolved CLR type.</returns>
    private static Type ResolveComplexType(string typeName, IReadOnlyList<string>? importedNamespaces)
    {
        int lt = typeName.IndexOf('<');
        if (lt > 0 && typeName.EndsWith(">", StringComparison.Ordinal))
        {
            string baseName = typeName[..lt].Trim();
            string argsText = typeName[(lt + 1)..^1].Trim();
            List<Type> typeArgs = [.. SplitTopLevel(argsText, ',').Select(a => ResolveNativeType(a.Trim(), importedNamespaces))];
            Type? genericDef = FindTypeByName(baseName + "`" + typeArgs.Count, importedNamespaces);
            if (genericDef is not null)
            {
                return genericDef.MakeGenericType([.. typeArgs]);
            }
        }

        return FindTypeByName(typeName, importedNamespaces)
            ?? throw new NotSupportedException($"Unsupported type '{typeName}'.");
    }

    /// <summary>
    /// Searches all loaded assemblies for a type by simple or qualified name,
    /// trying both the name directly and with each known/imported namespace prefix.
    /// </summary>
    private static Type? FindTypeByName(string typeName, IReadOnlyList<string>? importedNamespaces)
    {
        Type? t = Type.GetType(typeName, false)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(typeName, false))
                .FirstOrDefault(static x => x is not null);
        if (t is not null) return t;

        IEnumerable<string> namespaces = (importedNamespaces ?? []).Concat(DefaultNamespaces);
        foreach (string ns in namespaces)
        {
            string qualified = ns + "." + typeName;
            t = Type.GetType(qualified, false)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(qualified, false))
                    .FirstOrDefault(static x => x is not null);
            if (t is not null) return t;
        }

        return null;
    }

    /// <summary>
    /// Converts string literal token text into CLR string content.
    /// </summary>
    /// <param name="tokenText">Raw token text.</param>
    /// <returns>Decoded string content.</returns>
    private static string ParseStringLiteral(string tokenText)
    {
        if (tokenText.StartsWith("@\"", StringComparison.Ordinal))
        {
            return tokenText[2..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        }

        if (tokenText.Length < 2)
        {
            return tokenText;
        }

        string value = tokenText[1..^1];
        return value
            .Replace("\\\\", "\\", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);
    }

    /// <summary>
    /// Normalizes operands for equality comparison.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Normalized operands with compatible types.</returns>
    private static (Expression Left, Expression Right) NormalizeForComparison(Expression left, Expression right)
    {
        if (IsNumericType(left.Type) || IsNumericType(right.Type))
        {
            return (EnsureNumeric(left), EnsureNumeric(right));
        }

        if (left.Type == right.Type)
        {
            return (left, right);
        }

        return (Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
    }

    /// <summary>
    /// Ensures that an expression is non-null.
    /// </summary>
    /// <param name="expression">Expression candidate.</param>
    /// <param name="label">Semantic label used in error messages.</param>
    /// <returns>Non-null expression.</returns>
    private static Expression RequireExpression(Expression? expression, string label)
    {
        return expression ?? throw new InvalidOperationException($"Missing {label}.");
    }

    /// <summary>
    /// Ensures that an expression is numeric and converts it to <see cref="double"/>.
    /// </summary>
    /// <param name="expression">Input expression.</param>
    /// <returns>Numeric expression converted to double when required.</returns>
    private static Expression EnsureNumeric(Expression expression)
    {
        if (!IsNumericType(expression.Type))
        {
            throw new NotSupportedException($"Expression '{expression}' is not numeric.");
        }

        return expression.Type == typeof(double)
            ? expression
            : Expression.Convert(expression, typeof(double));
    }

    /// <summary>
    /// Ensures that an expression is boolean.
    /// </summary>
    /// <param name="expression">Input expression.</param>
    /// <returns>Boolean expression.</returns>
    private static Expression EnsureBoolean(Expression expression)
    {
        if (expression.Type != typeof(bool))
        {
            throw new NotSupportedException($"Expression '{expression}' is not boolean.");
        }

        return expression;
    }

    /// <summary>
    /// Ensures that an expression is integer and converts it to <see cref="int"/>.
    /// </summary>
    /// <param name="expression">Input expression.</param>
    /// <returns>Integer expression converted to int when required.</returns>
    private static Expression EnsureInteger(Expression expression)
    {
        if (!IsIntegerType(expression.Type))
        {
            throw new NotSupportedException($"Expression '{expression}' is not an integer type.");
        }

        return expression.Type == typeof(int)
            ? expression
            : Expression.Convert(expression, typeof(int));
    }

    /// <summary>
    /// Converts an expression to a target type when needed.
    /// </summary>
    /// <param name="expression">Source expression.</param>
    /// <param name="targetType">Target CLR type.</param>
    /// <returns>Converted or original expression.</returns>
    private static Expression ConvertIfNeeded(Expression expression, Type targetType)
    {
        if (expression.Type == targetType)
        {
            return expression;
        }

        if (ExpressionEx.TryGetConverter(targetType, expression, out Expression? converter))
        {
            return converter;
        }

        return Expression.Convert(expression, targetType);
    }

    /// <summary>
    /// Indicates whether a CLR type is numeric.
    /// </summary>
    /// <param name="type">CLR type.</param>
    /// <returns><c>true</c> when numeric; otherwise <c>false</c>.</returns>
    private static bool IsNumericType(Type type) => NumberUtils.IsNativeNumericType(type);

    /// <summary>
    /// Indicates whether a CLR type is an integer type.
    /// </summary>
    /// <param name="type">CLR type.</param>
    /// <returns><c>true</c> when integer; otherwise <c>false</c>.</returns>
    private static bool IsIntegerType(Type type) => NumberUtils.IsNativeIntegerType(type);

    /// <summary>
    /// Indicates whether a source fragment likely represents a method declaration context.
    /// </summary>
    /// <param name="sourceText">Source text being compiled.</param>
    /// <returns><c>true</c> when method declarations are detected; otherwise <c>false</c>.</returns>
    private static bool LooksLikeMethodDeclarationSource(string sourceText)
        => sourceText.Contains("public", StringComparison.Ordinal)
           && sourceText.Contains("{", StringComparison.Ordinal)
           && sourceText.Contains("(", StringComparison.Ordinal);

    /// <summary>
    /// Internal compilation context used by parse-tree handlers.
    /// </summary>
    /// <param name="Symbols">Expression symbols for identifier resolution.</param>
    /// <param name="RuntimeContext">Optional mutable runtime symbol context.</param>
    /// <param name="SourceText">Current source text.</param>
    /// <param name="Compiler">Owning compiler instance.</param>
    /// <param name="ImportedNamespaces">Namespaces imported by <c>using</c> directives in the source.</param>
    /// <param name="BlockScope">Per-node variable lists populated by descent handlers.</param>
    private sealed record CompilationContext(
        IReadOnlyDictionary<string, Expression> Symbols,
        CSyntaxCompilerContext? RuntimeContext,
        string SourceText,
        CSyntaxExpressionCompiler Compiler,
        IReadOnlyList<string> ImportedNamespaces,
        Dictionary<ParseNode, List<ParameterExpression>> BlockScope);

    /// <summary>
    /// Represents a parsed method declaration signature.
    /// </summary>
    /// <param name="Name">Method name.</param>
    /// <param name="IsPublic">Public visibility flag.</param>
    /// <param name="ReturnType">CLR return type.</param>
    /// <param name="Parameters">Lambda parameters.</param>
    /// <param name="ParameterSymbols">Parameter symbols exposed to method-body compilation.</param>
    /// <param name="BodySource">Method body source expression.</param>
    private sealed record MethodSignature(
        string Name,
        bool IsPublic,
        Type ReturnType,
        IReadOnlyList<ParameterExpression> Parameters,
        IReadOnlyDictionary<string, Expression> ParameterSymbols,
        string BodySource);

    /// <summary>
    /// Mutable holder used by deferred delegates to enable forward method references.
    /// </summary>
    private sealed class DeferredDelegateHolder
    {
        /// <summary>
        /// Gets or sets the compiled delegate target.
        /// </summary>
        public Delegate? Target { get; set; }
    }

    /// <summary>
    /// Invokes the delegate currently stored in a deferred holder.
    /// </summary>
    /// <param name="holder">Holder containing the target delegate.</param>
    /// <param name="arguments">Invocation arguments.</param>
    /// <returns>Delegate invocation result.</returns>
    private static object? InvokeDeferred(DeferredDelegateHolder holder, object?[] arguments)
    {
        if (holder.Target is null)
        {
            throw new InvalidOperationException("The method body is not compiled yet.");
        }

        return holder.Target.DynamicInvoke(arguments);
    }

    /// <summary>
    /// Creates a typed deferred delegate that dispatches to a holder target.
    /// </summary>
    /// <param name="delegateType">Delegate CLR type to create.</param>
    /// <param name="parameters">Delegate parameters.</param>
    /// <param name="holder">Deferred holder storing the final target.</param>
    /// <param name="returnType">Delegate return type.</param>
    /// <returns>Deferred delegate instance.</returns>
    private static Delegate CreateDeferredDelegate(
        Type delegateType,
        IReadOnlyList<ParameterExpression> parameters,
        DeferredDelegateHolder holder,
        Type returnType)
    {
        Expression[] boxedArguments = [.. parameters.Select(static p => Expression.Convert(p, typeof(object)))];
        Expression invoke = Expression.Call(
            typeof(CSyntaxExpressionCompiler).GetMethod(nameof(InvokeDeferred), BindingFlags.NonPublic | BindingFlags.Static)!,
            Expression.Constant(holder),
            Expression.NewArrayInit(typeof(object), boxedArguments));
        Expression body = returnType == typeof(void)
            ? Expression.Block(invoke, Expression.Empty())
            : ConvertIfNeeded(invoke, returnType);
        return Expression.Lambda(delegateType, body, parameters).Compile();
    }

    /// <summary>
    /// Registers public methods found in source as deferred delegates to support forward references.
    /// </summary>
    /// <param name="source">Source text to inspect.</param>
    /// <param name="context">Runtime context receiving deferred symbols.</param>
    private static void RegisterDeferredPublicMethods(string source, CSyntaxCompilerContext context)
    {
        foreach (Match match in Regex.Matches(
                     source,
                     @"(?<mods>(?:public|private|protected|internal|static|virtual|override|abstract|async)\s+)*(?<ret>[A-Za-z_][A-Za-z0-9_\.]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<params>[^)]*)\)\s*\{",
                     RegexOptions.Singleline))
        {
            string modifiers = match.Groups["mods"].Value;
            if (!modifiers.Contains("public", StringComparison.Ordinal))
            {
                continue;
            }

            string methodName = match.Groups["name"].Value;
            if (context.TryGet(methodName, out _))
            {
                continue;
            }

            Type returnType = ResolveNativeType(match.Groups["ret"].Value);
            List<ParameterExpression> parameters = new();
            foreach (string parameterText in SplitTopLevel(match.Groups["params"].Value, ',').Select(static s => s.Trim()).Where(static s => s.Length > 0))
            {
                string[] parts = parameterText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                parameters.Add(Expression.Parameter(ResolveNativeType(parts[0]), parts[1]));
            }

            Type delegateType = Expression.GetDelegateType([.. parameters.Select(static p => p.Type), returnType]);
            DeferredDelegateHolder holder = new();
            Delegate deferredDelegate = CreateDeferredDelegate(delegateType, parameters, holder, returnType);
            context.Set(methodName, deferredDelegate);
            context.Set(GetDeferredHolderSymbolName(methodName), holder);
        }
    }

    /// <summary>
    /// Builds the internal symbol name used to store a deferred holder.
    /// </summary>
    /// <param name="methodName">Method name.</param>
    /// <returns>Internal deferred-holder symbol name.</returns>
    private static string GetDeferredHolderSymbolName(string methodName)
        => "__deferred__" + methodName;

    // ── Lambda expression handlers ────────────────────────────────────────────

    /// <summary>
    /// Descent handler for <c>lambda_expression</c>: extracts typed parameters,
    /// registers them as symbols for children, and stashes them in the block scope.
    /// </summary>
    private static CompilationContext DescentLambdaExpression(ParseTreeNavigator nav, CompilationContext context)
    {
        var parameters = new List<ParameterExpression>();
        var symbols = new Dictionary<string, Expression>(context.Symbols, StringComparer.Ordinal);

        // Only look at the direct lambda_parameters child to avoid picking up params
        // from nested lambdas in the body.
        ParseTreeNavigator? lambdaParamsNode = nav.Children()
            .FirstOrDefault(static n => n.RuleName == "lambda_parameters");

        if (lambdaParamsNode is not null)
        {
            foreach (ParseTreeNavigator paramNav in lambdaParamsNode.Descendants("parameter"))
            {
                ParseTreeNavigator? typeNav = paramNav.Children().FirstOrDefault(static n => n.RuleName == "type_reference");
                ParseTreeNavigator? nameNav = paramNav.Children().FirstOrDefault(static n => n.RuleName == "identifier");
                if (typeNav is null || nameNav is null) continue;

                string typeName = FlattenNodeText(typeNav.Node);
                string paramName = FlattenNodeText(nameNav.Node);
                Type paramType = ResolveNativeType(typeName, context.ImportedNamespaces);
                ParameterExpression param = Expression.Parameter(paramType, paramName);
                parameters.Add(param);
                symbols[paramName] = param;
            }
        }

        context.BlockScope[nav.Node] = parameters;
        return context with { Symbols = symbols };
    }

    /// <summary>
    /// Descent handler for <c>foreach_instruction</c>: pre-registers iterator symbol so child
    /// nodes in the loop body can compile before the foreach node is recompiled from source.
    /// </summary>
    /// <param name="nav">Current foreach navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <returns>Updated context with inferred iterator symbol when available.</returns>
    private static CompilationContext DescentForeachInstruction(ParseTreeNavigator nav, CompilationContext context)
    {
        string source = GetNodeSourceText(context, nav);
        if (string.IsNullOrWhiteSpace(source))
        {
            source = FlattenNodeText(nav.Node);
        }
        if (!source.Contains('('))
        {
            source = context.SourceText;
        }

        source = ExtractInstructionSource(source, "foreach");
        (string header, _) = ExtractLoopHeaderAndBody(source);
        Match inKeyword = Regex.Match(header, @"\bin\b");
        if (!inKeyword.Success)
        {
            return context;
        }

        string leftPart = header[..inKeyword.Index].Trim();
        string[] iteratorParts = leftPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (iteratorParts.Length == 0)
        {
            return context;
        }

        string iteratorName = iteratorParts[^1];
        Type iteratorType = typeof(double);
        if (iteratorParts.Length > 1 && !string.Equals(iteratorParts[0], "var", StringComparison.Ordinal))
        {
            iteratorType = ResolveNativeType(iteratorParts[0], context.ImportedNamespaces);
        }

        var symbols = new Dictionary<string, Expression>(context.Symbols, StringComparer.Ordinal)
        {
            [iteratorName] = Expression.Parameter(iteratorType, iteratorName),
        };
        return context with { Symbols = symbols };
    }

    /// <summary>
    /// Rewrites simple interpolated strings (<c>$"...{x}..."</c>) into concatenation expressions.
    /// </summary>
    /// <param name="source">Source expression text.</param>
    /// <returns>Source with simple interpolated strings expanded.</returns>
    private static string PreprocessSimpleInterpolatedStrings(string source)
    {
        return Regex.Replace(source, "\\$\\\"(?<inner>(?:[^\\\"\\\\]|\\\\.)*)\\\"", match =>
        {
            string inner = match.Groups["inner"].Value;
            List<string> parts = new();
            int index = 0;
            while (index < inner.Length)
            {
                int openBrace = inner.IndexOf('{', index);
                if (openBrace < 0)
                {
                    string trailing = inner[index..];
                    if (trailing.Length > 0)
                    {
                        parts.Add(ToQuotedLiteral(trailing));
                    }

                    break;
                }

                if (openBrace > index)
                {
                    parts.Add(ToQuotedLiteral(inner[index..openBrace]));
                }

                int closeBrace = inner.IndexOf('}', openBrace + 1);
                if (closeBrace < 0)
                {
                    return match.Value;
                }

                string expression = inner[(openBrace + 1)..closeBrace].Trim();
                if (expression.Length == 0)
                {
                    return match.Value;
                }

                parts.Add(expression);
                index = closeBrace + 1;
            }

            if (parts.Count == 0)
            {
                return "\"\"";
            }

            return "(" + string.Join(" + ", parts) + ")";
        });
    }

    /// <summary>
    /// Converts text content into a quoted literal suitable for source rewriting.
    /// </summary>
    /// <param name="text">Literal content.</param>
    /// <returns>Quoted source literal.</returns>
    private static string ToQuotedLiteral(string text)
        => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    /// <summary>
    /// Ascent handler for <c>lambda_expression</c>: wraps the compiled body with the
    /// parameters previously stored during descent.
    /// </summary>
    private static Expression? AscentLambdaExpression(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        Expression? body = children.LastOrDefault(static c => c is not null);
        if (body is null) return null;

        context.BlockScope.TryGetValue(nav.Node, out List<ParameterExpression>? parameters);
        return Expression.Lambda(body, parameters ?? []);
    }

    // ── Block variable scoping handlers ──────────────────────────────────────

    /// <summary>
    /// Descent handler for <c>block_instruction</c>: pre-declares all local variables
    /// found at the top level of this block so they are in scope for every child.
    /// </summary>
    private static CompilationContext DescentBlockInstruction(ParseTreeNavigator nav, CompilationContext context)
    {
        var variables = new List<ParameterExpression>();
        var symbols = new Dictionary<string, Expression>(context.Symbols, StringComparer.Ordinal);

        foreach (ParseTreeNavigator declNav in FindVarDeclarationsShallow(nav))
        {
            ParseTreeNavigator? typeNav = declNav.Children().FirstOrDefault(static n => n.RuleName == "type_reference");
            ParseTreeNavigator? nameNav = declNav.Children().FirstOrDefault(static n => n.RuleName == "identifier");
            if (typeNav is null || nameNav is null) continue;

            string varName = FlattenNodeText(nameNav.Node);
            if (symbols.ContainsKey(varName)) continue;

            string typeName = FlattenNodeText(typeNav.Node);
            Type varType = ResolveNativeType(typeName, context.ImportedNamespaces);
            ParameterExpression variable = Expression.Variable(varType, varName);
            variables.Add(variable);
            symbols[varName] = variable;
        }

        context.BlockScope[nav.Node] = variables;
        return context with { Symbols = symbols };
    }

    /// <summary>
    /// Enumerates <c>variable_declaration_assignment</c> nodes that are direct logical children
    /// of a block, without descending into nested blocks.
    /// </summary>
    private static IEnumerable<ParseTreeNavigator> FindVarDeclarationsShallow(ParseTreeNavigator blockNav)
    {
        foreach (ParseTreeNavigator child in blockNav.Children())
        {
            foreach (ParseTreeNavigator decl in FindVarDeclarationsShallowInNode(child))
            {
                yield return decl;
            }
        }
    }

    private static IEnumerable<ParseTreeNavigator> FindVarDeclarationsShallowInNode(ParseTreeNavigator nav)
    {
        if (nav.RuleName == "variable_declaration_assignment")
        {
            yield return nav;
            yield break;
        }

        if (nav.RuleName == "block_instruction")
        {
            yield break;
        }

        foreach (ParseTreeNavigator child in nav.Children())
        {
            foreach (ParseTreeNavigator decl in FindVarDeclarationsShallowInNode(child))
            {
                yield return decl;
            }
        }
    }

    /// <summary>
    /// Ascent handler for <c>variable_declaration_assignment</c>: produces an assignment
    /// expression using the pre-declared variable from the enclosing block scope.
    /// </summary>
    private static Expression? CompileVariableDeclaration(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        List<Expression> nonNull = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (nonNull.Count < 2) return nonNull.FirstOrDefault() ?? Expression.Empty();

        Expression target = nonNull[0];
        Expression value = nonNull[^1];
        if (ReferenceEquals(target, value)) return target;

        return Expression.Assign(target, ConvertIfNeeded(value, target.Type));
    }

}
