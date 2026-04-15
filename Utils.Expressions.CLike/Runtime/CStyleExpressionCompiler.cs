using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Objects;
using Utils.Parser.Runtime;

namespace Utils.Expressions.CLike.Runtime;

/// <summary>
/// Compiles C-like parse trees into LINQ expression trees by using
/// <see cref="ParseTreeCompiler{TContext, TResult}"/>.
/// </summary>
public sealed class CStyleExpressionCompiler : IExpressionCompiler
{
    private readonly CStyleTokenParser _parser = new();

    /// <summary>
    /// Parses and compiles a C-like source expression into a LINQ expression tree.
    /// </summary>
    /// <param name="content">C-like content to parse and compile.</param>
    /// <param name="symbols">
    /// Optional symbol table used to resolve identifiers to existing expressions
    /// (typically <see cref="ParameterExpression"/> instances).
    /// </param>
    /// <returns>Compiled expression tree.</returns>
    public Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ParseNode root = _parser.Parse(content);
        return Compile(root, symbols, content, null);
    }

    /// <summary>
    /// Parses and compiles C-like source while using and mutating a rich runtime context.
    /// </summary>
    /// <param name="content">C-like source content.</param>
    /// <param name="context">Runtime compilation context.</param>
    /// <returns>Compiled expression tree.</returns>
    public Expression Compile(string content, CStyleCompilerContext context)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(context);
        ParseNode root = _parser.Parse(content);
        return Compile(root, null, content, context);
    }

    /// <summary>
    /// Compiles a full source unit by parsing it as a block so declarations and expressions
    /// can reference each other in source order.
    /// </summary>
    /// <param name="source">Source unit containing one or many C-like instructions.</param>
    /// <param name="context">Runtime compilation context.</param>
    /// <returns>Compiled expression for the full source unit.</returns>
    public Expression CompileSource(string source, CStyleCompilerContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        string sourceToParse = source.TrimStart().StartsWith("{", StringComparison.Ordinal) ? source : "{ " + source + " }";
        ParseNode root = _parser.Parse(sourceToParse);
        return Compile(root, null, sourceToParse, context);
    }

    /// <summary>
    /// Compiles a parsed C-like tree into a LINQ expression tree.
    /// </summary>
    /// <param name="root">Parse tree root to compile.</param>
    /// <param name="symbols">
    /// Optional symbol table used to resolve identifiers to existing expressions
    /// (typically <see cref="ParameterExpression"/> instances).
    /// </param>
    /// <returns>Compiled expression tree.</returns>
    public Expression Compile(ParseNode root, IReadOnlyDictionary<string, Expression>? symbols = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Compile(root, symbols, string.Empty, null);
    }

    /// <summary>
    /// Creates and configures the parse-tree compiler used for C-like expression compilation.
    /// </summary>
    /// <returns>Configured parse-tree compiler.</returns>
    private Expression Compile(
        ParseNode root,
        IReadOnlyDictionary<string, Expression>? symbols,
        string sourceText,
        CStyleCompilerContext? runtimeContext)
    {
        var compiler = CreateCompiler();
        CompilationContext context = new(
            symbols ?? new Dictionary<string, Expression>(StringComparer.Ordinal),
            runtimeContext,
            sourceText,
            this);
        Expression? compiled = compiler.Compile(root, context);
        return compiled ?? throw new InvalidOperationException("Unable to compile the provided C-like parse tree.");
    }

    private static ParseTreeCompiler<CompilationContext, Expression> CreateCompiler()
    {
        return new ParseTreeCompiler<CompilationContext, Expression>()
            .OnError((nav, _) => throw new InvalidOperationException($"Parser error: {((ErrorNode)nav.Node).Message}"))
            .OnAscend("NUMBER", (nav, _) => CompileNumberLiteral(nav))
            .OnAscend("TRUE", (_, _) => Expression.Constant(true))
            .OnAscend("FALSE", (_, _) => Expression.Constant(false))
            .OnAscend("NULL", (_, _) => Expression.Constant(null))
            .OnAscend("STRING_LITERAL", (nav, _) => Expression.Constant(ParseStringLiteral(nav.Token!.Text)))
            .OnAscend("IDENTIFIER", (nav, ctx) => ResolveIdentifier(ctx, nav.Token!.Text))
            .OnAscend("identifier_part", (nav, _, children) => CompileIdentifierPart(nav, children))
            .OnAscend("operation_or", CompileLogicalOr)
            .OnAscend("operation_and", CompileLogicalAnd)
            .OnAscend("operation_equality", CompileEquality)
            .OnAscend("operation_relational", CompileRelational)
            .OnAscend("operation_shift", CompileShift)
            .OnAscend("operation_plus", CompileAddSub)
            .OnAscend("operation_mul", CompileMulDivMod)
            .OnAscend("operation_pow", CompilePower)
            .OnAscend("operation_unary", CompileUnary)
            .OnAscend("assignment_instruction", CompileAssignment)
            .OnAscend("invocation_instruction", CompileInvocation)
            .OnAscend("method_declaration", CompileMethodDeclaration)
            .OnAscend("block_instruction", CompileBlock)
            .DefaultAscend((_, _, children) => children.FirstOrDefault(static child => child is not null));
    }

    /// <summary>
    /// Compiles a number token into a numeric constant expression.
    /// </summary>
    /// <param name="nav">Current lexer navigator.</param>
    /// <returns>Constant expression representing the number.</returns>
    private static Expression CompileNumberLiteral(ParseTreeNavigator nav)
    {
        string text = nav.Token!.Text;
        string normalized = text.TrimEnd('f', 'F', 'd', 'D', 'm', 'M', 'u', 'U', 'l', 'L');
        if (normalized.Contains('.') || normalized.Contains('e', StringComparison.OrdinalIgnoreCase))
        {
            double value = double.Parse(normalized, CultureInfo.InvariantCulture);
            return Expression.Constant(value);
        }

        int integer = int.Parse(normalized, CultureInfo.InvariantCulture);
        return Expression.Constant(integer);
    }

    /// <summary>
    /// Resolves an identifier from the compilation symbol table.
    /// </summary>
    /// <param name="context">Compilation context.</param>
    /// <param name="identifier">Identifier text.</param>
    /// <returns>Resolved symbol expression.</returns>
    private static Expression ResolveIdentifier(CompilationContext context, string identifier)
    {
        if (context.Symbols.TryGetValue(identifier, out Expression? expression))
        {
            return expression;
        }

        if (context.RuntimeContext is not null && context.RuntimeContext.TryGet(identifier, out object? value))
        {
            return value switch
            {
                Expression valueExpression => valueExpression,
                Delegate valueDelegate => Expression.Constant(valueDelegate),
                _ => Expression.Constant(value, value?.GetType() ?? typeof(object)),
            };
        }

        throw new InvalidOperationException($"Unknown identifier '{identifier}'.");
    }

    /// <summary>
    /// Compiles an identifier-part rule. Only simple identifiers are supported.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled identifier expression.</returns>
    private static Expression? CompileIdentifierPart(ParseTreeNavigator nav, IReadOnlyList<Expression?> children)
    {
        // identifier_part := identifier_atom identifier_suffix*
        return children.FirstOrDefault(static child => child is not null);
    }

    /// <summary>
    /// Compiles a logical OR chain.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled logical OR expression.</returns>
    private static Expression? CompileLogicalOr(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
        => FoldBinary(nav, children, ["||"], static (op, left, right) => Expression.OrElse(EnsureBoolean(left), EnsureBoolean(right)));

    /// <summary>
    /// Compiles a logical AND chain.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled logical AND expression.</returns>
    private static Expression? CompileLogicalAnd(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
        => FoldBinary(nav, children, ["&&"], static (op, left, right) => Expression.AndAlso(EnsureBoolean(left), EnsureBoolean(right)));

    /// <summary>
    /// Compiles equality/inequality operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled equality expression.</returns>
    private static Expression? CompileEquality(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            (Expression normalizedLeft, Expression normalizedRight) = NormalizeForComparison(left, right);
            return op switch
            {
                "==" => Expression.Equal(normalizedLeft, normalizedRight),
                "!=" => Expression.NotEqual(normalizedLeft, normalizedRight),
                _ => throw new NotSupportedException($"Unsupported equality operator '{op}'."),
            };
        }, ["==", "!="]);
    }

    /// <summary>
    /// Compiles relational operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled relational expression.</returns>
    private static Expression? CompileRelational(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            Expression l = EnsureNumeric(left);
            Expression r = EnsureNumeric(right);
            return op switch
            {
                "<" => Expression.LessThan(l, r),
                "<=" => Expression.LessThanOrEqual(l, r),
                ">" => Expression.GreaterThan(l, r),
                ">=" => Expression.GreaterThanOrEqual(l, r),
                _ => throw new NotSupportedException($"Unsupported relational operator '{op}'."),
            };
        }, ["<", "<=", ">", ">="]);
    }

    /// <summary>
    /// Compiles bit-shift operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled shift expression.</returns>
    private static Expression? CompileShift(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            Expression l = EnsureInteger(left);
            Expression r = EnsureInteger(right);
            return op switch
            {
                "<<" => Expression.LeftShift(l, r),
                ">>" => Expression.RightShift(l, r),
                _ => throw new NotSupportedException($"Unsupported shift operator '{op}'."),
            };
        }, ["<<", ">>"]);
    }

    /// <summary>
    /// Compiles addition/subtraction operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled additive expression.</returns>
    private static Expression? CompileAddSub(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            Expression l = EnsureNumeric(left);
            Expression r = EnsureNumeric(right);
            return op switch
            {
                "+" => Expression.Add(l, r),
                "-" => Expression.Subtract(l, r),
                _ => throw new NotSupportedException($"Unsupported additive operator '{op}'."),
            };
        }, ["+", "-"]);
    }

    /// <summary>
    /// Compiles multiplication/division/modulo operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled multiplicative expression.</returns>
    private static Expression? CompileMulDivMod(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            Expression l = EnsureNumeric(left);
            Expression r = EnsureNumeric(right);
            return op switch
            {
                "*" => Expression.Multiply(l, r),
                "/" => Expression.Divide(l, r),
                "%" => Expression.Modulo(l, r),
                _ => throw new NotSupportedException($"Unsupported multiplicative operator '{op}'."),
            };
        }, ["*", "/", "%"]);
    }

    /// <summary>
    /// Compiles exponentiation operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled power expression.</returns>
    private static Expression? CompilePower(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            if (op != "**")
            {
                throw new NotSupportedException($"Unsupported power operator '{op}'.");
            }

            return Expression.Call(
                typeof(Math).GetMethod(nameof(Math.Pow), [typeof(double), typeof(double)])!,
                EnsureNumeric(left),
                EnsureNumeric(right));
        }, ["**"]);
    }

    /// <summary>
    /// Compiles unary operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled unary expression.</returns>
    private static Expression? CompileUnary(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        if (nav.RawChildren is null || nav.RawChildren.Count == 0)
        {
            return null;
        }

        if (nav.RawChildren.Count == 1)
        {
            return children[0];
        }

        string op = ((LexerNode)nav.RawChildren[0]).Token.Text;
        Expression operand = RequireExpression(children[1], "unary operand");
        return op switch
        {
            "+" => EnsureNumeric(operand),
            "-" => Expression.Negate(EnsureNumeric(operand)),
            "!" => Expression.Not(EnsureBoolean(operand)),
            "~" => Expression.OnesComplement(EnsureInteger(operand)),
            _ => throw new NotSupportedException($"Unsupported unary operator '{op}'."),
        };
    }

    /// <summary>
    /// Compiles assignment instructions.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled assignment expression.</returns>
    private static Expression? CompileAssignment(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        if (nav.RawChildren is null || nav.RawChildren.Count < 3)
        {
            return children.FirstOrDefault(static child => child is not null);
        }

        int assignmentIndex = nav.RawChildren
            .Select((node, index) => (node, index))
            .FirstOrDefault(tuple => tuple.node is LexerNode lexer && lexer.Token.Text == "=")
            .index;
        if (assignmentIndex <= 0)
        {
            return children.FirstOrDefault(static child => child is not null);
        }

        Expression left = RequireExpression(
            FindCompiledChildNear(nav.RawChildren, children, assignmentIndex - 1, -1),
            "assignment target");
        Expression right = RequireExpression(
            FindCompiledChildNear(nav.RawChildren, children, assignmentIndex + 1, +1),
            "assignment value");
        Expression convertedRight = ConvertIfNeeded(right, left.Type);
        return Expression.Assign(left, convertedRight);
    }

    /// <summary>
    /// Compiles invocation instructions for delegates, lambda expressions, or static methods.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled invocation expression.</returns>
    private static Expression? CompileInvocation(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        string invocationText = GetNodeSourceText(context, nav);
        int openParenIndex = invocationText.IndexOf('(');
        int closeParenIndex = invocationText.LastIndexOf(')');
        if (openParenIndex <= 0 || closeParenIndex <= openParenIndex)
        {
            return children.FirstOrDefault(static child => child is not null);
        }

        string calleeName = invocationText[..openParenIndex].Trim();
        string argsSegment = invocationText[(openParenIndex + 1)..closeParenIndex];
        List<Expression> arguments = ParseInvocationArguments(argsSegment, context);
        Expression[] argumentArray = [.. arguments];

        if (context.RuntimeContext is not null && context.RuntimeContext.TryGet(calleeName, out object? value))
        {
            return value switch
            {
                Delegate d => Expression.Invoke(Expression.Constant(d), ConvertArgumentsForDelegate(d.Method, argumentArray)),
                LambdaExpression lambda => Expression.Invoke(lambda, ConvertArgumentsForParameters(lambda.Parameters, argumentArray)),
                Expression expression when typeof(Delegate).IsAssignableFrom(expression.Type)
                    => Expression.Invoke(expression, argumentArray),
                MethodInfo methodInfo => Expression.Call(methodInfo, ConvertArgumentsForParameters(methodInfo.GetParameters(), argumentArray)),
                _ => throw new NotSupportedException($"Symbol '{calleeName}' is not invokable."),
            };
        }

        if (TryResolveStaticMethodCall(calleeName, argumentArray, out Expression? methodCall))
        {
            return methodCall;
        }

        throw new InvalidOperationException($"Unknown invokable symbol '{calleeName}'.");
    }

    /// <summary>
    /// Compiles method declarations and publishes public methods to runtime context.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>A no-op expression.</returns>
    private static Expression? CompileMethodDeclaration(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        if (context.RuntimeContext is null)
        {
            return Expression.Empty();
        }

        MethodSignature signature = ParseMethodSignature(nav, context);
        if (signature.ParameterSymbols.Count == 0)
        {
            signature = InferParametersFromBody(signature);
        }
        var bodyContext = new CStyleCompilerContext();
        foreach (KeyValuePair<string, Expression> entry in signature.ParameterSymbols)
        {
            bodyContext.Set(entry.Key, entry.Value);
        }

        Expression body = context.Compiler.Compile(signature.BodySource, bodyContext);
        Type delegateType = Expression.GetDelegateType([.. signature.Parameters.Select(static p => p.Type), signature.ReturnType]);
        LambdaExpression lambda = Expression.Lambda(delegateType, ConvertIfNeeded(body, signature.ReturnType), signature.Parameters);
        Delegate compiledDelegate = lambda.Compile();

        if (signature.IsPublic)
        {
            context.RuntimeContext.Set(signature.Name, compiledDelegate);
        }

        return Expression.Empty();
    }

    /// <summary>
    /// Infers missing method parameters from identifier usage in method body.
    /// </summary>
    /// <param name="signature">Method signature candidate.</param>
    /// <returns>Updated signature with inferred parameters when needed.</returns>
    private static MethodSignature InferParametersFromBody(MethodSignature signature)
    {
        var reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            "true", "false", "null", "if", "for", "while", "do", "switch", "case", "default", "return", "var",
        };

        var parameters = new List<ParameterExpression>();
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(signature.BodySource, @"\b[A-Za-z_][A-Za-z0-9_]*\b"))
        {
            string name = match.Value;
            if (reserved.Contains(name) || symbols.ContainsKey(name))
            {
                continue;
            }

            ParameterExpression parameter = Expression.Parameter(typeof(double), name);
            parameters.Add(parameter);
            symbols[name] = parameter;
        }

        return signature with
        {
            Parameters = parameters,
            ParameterSymbols = symbols,
        };
    }

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
        if (expressions.Count == 0)
        {
            return Expression.Empty();
        }

        if (expressions.Count == 1)
        {
            return expressions[0];
        }

        return Expression.Block(expressions);
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
        if (parameters.Count != arguments.Length)
        {
            throw new InvalidOperationException("Invocation argument count does not match parameter count.");
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

        MethodInfo? method = type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(methodInfo => methodInfo.Name == methodName && methodInfo.GetParameters().Length == arguments.Length);
        if (method is null)
        {
            return false;
        }

        callExpression = Expression.Call(method, ConvertArgumentsForParameters(method.GetParameters(), arguments));
        return true;
    }

    /// <summary>
    /// Parses a method declaration signature from the parse tree.
    /// </summary>
    /// <param name="nav">Method declaration navigator.</param>
    /// <param name="context">Compilation context.</param>
    /// <returns>Parsed method signature model.</returns>
    private static MethodSignature ParseMethodSignature(ParseTreeNavigator nav, CompilationContext context)
    {
        return ParseMethodSignatureFromWholeSource(context.SourceText);

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

    /// <summary>
    /// Resolves a native CLR type from a C-style type token.
    /// </summary>
    /// <param name="typeName">Type token.</param>
    /// <returns>Resolved CLR type.</returns>
    private static Type ResolveNativeType(string typeName)
    {
        return typeName switch
        {
            "void" => typeof(void),
            "bool" => typeof(bool),
            "byte" => typeof(byte),
            "char" => typeof(char),
            "decimal" => typeof(decimal),
            "double" => typeof(double),
            "float" => typeof(float),
            "int" => typeof(int),
            "long" => typeof(long),
            "object" => typeof(object),
            "sbyte" => typeof(sbyte),
            "short" => typeof(short),
            "string" => typeof(string),
            "uint" => typeof(uint),
            "ulong" => typeof(ulong),
            "ushort" => typeof(ushort),
            _ => Type.GetType(typeName, false)
                ?? throw new NotSupportedException($"Unsupported type '{typeName}'."),
        };
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
    /// Immutable compilation context used by the parse-tree compiler.
    /// </summary>
    /// <param name="Symbols">Resolved expression symbols by identifier name.</param>
    /// <summary>
    /// Internal compilation context used by parse-tree handlers.
    /// </summary>
    /// <param name="Symbols">Expression symbols for identifier resolution.</param>
    /// <param name="RuntimeContext">Optional mutable runtime symbol context.</param>
    /// <param name="SourceText">Current source text.</param>
    /// <param name="Compiler">Owning compiler instance.</param>
    private sealed record CompilationContext(
        IReadOnlyDictionary<string, Expression> Symbols,
        CStyleCompilerContext? RuntimeContext,
        string SourceText,
        CStyleExpressionCompiler Compiler);

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
}
