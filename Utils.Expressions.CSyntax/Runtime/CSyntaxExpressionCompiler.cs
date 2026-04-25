using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Dynamic;
using Utils.Expressions;
using Utils.Collections;
using Utils.Objects;
using Utils.Parser.Runtime;

namespace Utils.Expressions.CSyntax.Runtime;

/// <summary>
/// Compiles C-like parse trees into LINQ expression trees by using
/// <see cref="ParseTreeCompiler{TContext, TResult}"/>.
/// </summary>
public sealed partial class CSyntaxExpressionCompiler : IExpressionCompiler
{
    private readonly CSyntaxTokenParser _parser = new();

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
        content = PreprocessCompoundAssignments(content);
        content = PreprocessSimpleInterpolatedStrings(content);
        List<string> importedNamespaces = ExtractUsingDirectives(content);
        content = StripUsingDirectives(content);
        ParseNode root = _parser.Parse(content);
        return Compile(root, symbols, content, null, importedNamespaces);
    }

    /// <summary>
    /// Parses and compiles C-like source while using and mutating a rich runtime context.
    /// </summary>
    /// <param name="content">C-like source content.</param>
    /// <param name="context">Runtime compilation context.</param>
    /// <returns>Compiled expression tree.</returns>
    public Expression Compile(string content, ExpressionCompilerContext context)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(context);
        content = PreprocessCompoundAssignments(content);
        content = PreprocessSimpleInterpolatedStrings(content);
        List<string> importedNamespaces = ExtractUsingDirectives(content);
        content = StripUsingDirectives(content);
        ParseNode root = _parser.Parse(content);
        return Compile(root, null, content, context, importedNamespaces);
    }

    /// <summary>
    /// Compiles a full source unit by parsing it as a block so declarations and expressions
    /// can reference each other in source order.
    /// </summary>
    /// <param name="source">Source unit containing one or many C-like instructions.</param>
    /// <param name="context">Runtime compilation context.</param>
    /// <returns>Compiled expression for the full source unit.</returns>
    public Expression CompileSource(string source, ExpressionCompilerContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        source = PreprocessCompoundAssignments(source);
        source = PreprocessSimpleInterpolatedStrings(source);
        List<string> importedNamespaces = ExtractUsingDirectives(source);
        source = StripUsingDirectives(source);
        RegisterDeferredPublicMethods(source, context);
        string sourceToParse = source.TrimStart().StartsWith("{", StringComparison.Ordinal) ? source : "{ " + source + " }";
        ParseNode root = _parser.Parse(sourceToParse);
        return Compile(root, null, sourceToParse, context, importedNamespaces);
    }

    /// <summary>
    /// Compiles a C-like lambda source into a typed delegate expression.
    /// Untyped parameters are resolved from the delegate type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Target delegate type.</typeparam>
    /// <param name="content">C-like lambda source.</param>
    /// <returns>Typed lambda expression.</returns>
    public Expression<T> Compile<T>(string content) where T : Delegate
    {
        ArgumentNullException.ThrowIfNull(content);
        MethodInfo invokeMethod = typeof(T).GetMethod("Invoke")!;
        ParameterInfo[] invokeParams = invokeMethod.GetParameters();

        // Detect untyped lambda "(a, b) => ..." and inject types from T
        Match m = Regex.Match(content.Trim(), @"^\(\s*([\w\s,]*?)\s*\)\s*=>");
        if (m.Success)
        {
            string[] paramNames = m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            bool allUntyped = paramNames.Length > 0 && paramNames.All(static n => !n.Contains(' '));
            if (allUntyped && paramNames.Length == invokeParams.Length)
            {
                string typedParams = string.Join(", ", paramNames.Zip(invokeParams, static (name, p) => $"{GetCSharpTypeName(p.ParameterType)} {name}"));
                int arrowIndex = content.IndexOf("=>", StringComparison.Ordinal);
                content = "(" + typedParams + ") => " + content[(arrowIndex + 2)..].TrimStart();
            }
        }

        Expression result = Compile(content);
        if (result is Expression<T> typed) return typed;
        if (result is LambdaExpression lambda) return Expression.Lambda<T>(lambda.Body, lambda.Parameters);
        throw new InvalidOperationException($"Compiled expression cannot be converted to {typeof(T).Name}.");
    }

    /// <summary>
    /// Compiles a C-like expression body using explicit parameters and infers the return type
    /// from the provided delegate type.
    /// </summary>
    /// <typeparam name="T">Target delegate type.</typeparam>
    /// <param name="content">Expression body source (no lambda syntax).</param>
    /// <param name="parameters">Lambda parameters to bind as symbols.</param>
    /// <returns>Lambda expression compatible with <typeparamref name="T"/>.</returns>
    public LambdaExpression Compile<T>(string content, ParameterExpression[] parameters) where T : Delegate
        => Compile<T>(content, parameters, null, strictTypes: false);

    /// <summary>
    /// Compiles a C-like expression body using explicit parameters and optional static member imports.
    /// </summary>
    /// <typeparam name="T">Target delegate type.</typeparam>
    /// <param name="content">Expression body source (no lambda syntax).</param>
    /// <param name="parameters">Lambda parameters to bind as symbols.</param>
    /// <param name="importType">
    /// Optional type whose compatible public static methods are imported as callable symbols.
    /// </param>
    /// <param name="strictTypes">Reserved; currently ignored.</param>
    /// <returns>Lambda expression compatible with <typeparamref name="T"/>.</returns>
    public LambdaExpression Compile<T>(string content, ParameterExpression[] parameters, Type? importType, bool strictTypes) where T : Delegate
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(parameters);

        MethodInfo invokeMethod = typeof(T).GetMethod("Invoke")
            ?? throw new InvalidOperationException($"Delegate type '{typeof(T).Name}' has no Invoke method.");
        Type returnType = invokeMethod.ReturnType;
        ParameterInfo[] delegateParameters = invokeMethod.GetParameters();
        if (delegateParameters.Length != parameters.Length)
        {
            throw new ArgumentException(
                $"Delegate '{typeof(T).Name}' expects {delegateParameters.Length} parameters, but {parameters.Length} were provided.",
                nameof(parameters));
        }

        var symbols = parameters.ToDictionary(static p => p.Name!, static p => (Expression)p, StringComparer.Ordinal);
        if (importType is not null)
        {
            RegisterStaticCallableSymbols(importType, symbols);
        }

        Expression body = Compile(content, symbols);
        Expression convertedBody = ConvertIfNeeded(body, returnType);
        return Expression.Lambda(convertedBody, parameters);
    }

    /// <summary>
    /// Compiles a C-like expression body with explicit parameters and return type,
    /// returning a typed lambda expression.
    /// </summary>
    /// <param name="content">Expression body source (no lambda syntax).</param>
    /// <param name="parameters">Lambda parameters to bind as symbols.</param>
    /// <param name="returnType">Expected return type; the body is converted if needed.</param>
    /// <param name="strictTypes">Reserved; currently ignored.</param>
    /// <returns>Lambda expression with the specified parameters and return type.</returns>
    public LambdaExpression Compile(string content, ParameterExpression[] parameters, Type returnType, bool strictTypes)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(returnType);
        Dictionary<string, Expression> symbols = parameters.ToDictionary(static p => p.Name!, static p => (Expression)p, StringComparer.Ordinal);
        Expression body = Compile(content, symbols);
        Expression convertedBody = ConvertIfNeeded(body, returnType);
        return Expression.Lambda(convertedBody, parameters);
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
        ExpressionCompilerContext? runtimeContext,
        List<string>? importedNamespaces = null)
    {
        importedNamespaces ??= ExtractUsingDirectives(sourceText);
        var blockScope = new Dictionary<ParseNode, List<ParameterExpression>>(ReferenceEqualityComparer.Instance);
        var compiler = CreateCompiler();
        CompilationContext context = new(
            symbols ?? new Dictionary<string, Expression>(StringComparer.Ordinal),
            runtimeContext,
            sourceText,
            this,
            importedNamespaces,
            blockScope);
        Expression? compiled = compiler.Compile(root, context);
        return compiled ?? throw new InvalidOperationException("Unable to compile the provided C-like parse tree.");
    }

    /// <summary>
    /// Registers compatible public static methods from a type as callable symbols.
    /// </summary>
    /// <param name="importType">Type providing static methods.</param>
    /// <param name="symbols">Destination symbol table.</param>
    private static void RegisterStaticCallableSymbols(Type importType, IDictionary<string, Expression> symbols)
    {
        ArgumentNullException.ThrowIfNull(importType);
        ArgumentNullException.ThrowIfNull(symbols);

        IEnumerable<IGrouping<string, MethodInfo>> methodsByName = importType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(static method => !method.ContainsGenericParameters && method.ReturnType != typeof(void))
            .GroupBy(static method => method.Name, StringComparer.Ordinal);

        foreach (IGrouping<string, MethodInfo> group in methodsByName)
        {
            if (symbols.ContainsKey(group.Key))
            {
                continue;
            }

            List<MethodInfo> supportedMethods = [];
            foreach (MethodInfo method in group)
            {
                Type[] signature = method
                    .GetParameters()
                    .Select(static parameter => parameter.ParameterType)
                    .Append(method.ReturnType)
                    .ToArray();

                try
                {
                    _ = Expression.GetDelegateType(signature);
                    supportedMethods.Add(method);
                }
                catch (ArgumentException)
                {
                    // Skip unsupported signatures.
                }
            }

            if (supportedMethods.Count > 0)
            {
                symbols[group.Key] = Expression.Constant(supportedMethods.ToArray(), typeof(MethodInfo[]));
            }
        }
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
            .OnAscend("INTERPOLATED_TEXT", (nav, _) => Expression.Constant(nav.Token!.Text))
            .OnAscend("INTERPOLATED_ESCAPED_OPEN", (_, _) => Expression.Constant("{"))
            .OnAscend("INTERPOLATED_ESCAPED_CLOSE", (_, _) => Expression.Constant("}"))
            .OnAscend("interpolated_string", CompileInterpolatedString)
            .OnAscend("identifier_part", CompileIdentifierPart)
            .OnAscend("operation_or", CompileLogicalOr)
            .OnAscend("operation_and", CompileLogicalAnd)
            .OnAscend("operation_equality", CompileEquality)
            .OnAscend("operation_relational", CompileRelational)
            .OnAscend("operation_shift", CompileShift)
            .OnAscend("operation_plus", CompileAddSub)
            .OnAscend("operation_mul", CompileMulDivMod)
            .OnAscend("operation_pow", CompilePower)
            .OnAscend("operation_unary", CompileUnary)
            .OnAscend("object_creation_expression", CompileObjectCreationExpression)
            .OnAscend("assignment_instruction", CompileAssignment)
            .OnAscend("variable_declaration_assignment", CompileVariableDeclaration)
            .OnAscend("using_instruction", static (_, _) => Expression.Empty())
            .OnAscend("invocation_instruction", CompileInvocation)
            .OnAscend("if_instruction", CompileIfInstruction)
            .OnAscend("for_instruction", CompileForInstruction)
            .OnAscend("foreach_instruction", CompileForeachInstruction)
            .OnAscend("while_instruction", CompileWhileInstruction)
            .OnAscend("do_while_instruction", CompileDoWhileInstruction)
            .OnAscend("method_declaration", CompileMethodDeclaration)
            .OnDescend("lambda_expression", DescentLambdaExpression)
            .OnDescend("foreach_instruction", DescentForeachInstruction)
            .OnAscend("lambda_expression", AscentLambdaExpression)
            .OnDescend("block_instruction", DescentBlockInstruction)
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
        if (context.RuntimeContext is not null && context.RuntimeContext.TryGet(identifier, out object? value))
        {
            return value switch
            {
                Expression valueExpression => valueExpression,
                Delegate valueDelegate => Expression.Constant(valueDelegate),
                _ => Expression.Constant(value, value?.GetType() ?? typeof(object)),
            };
        }

        if (context.Symbols.TryGetValue(identifier, out Expression? expression))
        {
            return expression;
        }

        if (LooksLikeMethodDeclarationSource(context.SourceText))
        {
            return Expression.Parameter(typeof(double), identifier);
        }

        if (context.RuntimeContext is not null)
        {
            return Expression.Parameter(typeof(double), identifier);
        }

        throw new InvalidOperationException($"Unknown identifier '{identifier}'.");
    }

    /// <summary>
    /// Compiles an identifier-part rule. Only simple identifiers are supported.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled identifier expression.</returns>
    private static Expression? CompileIdentifierPart(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        string expressionText = GetNodeSourceText(context, nav).Trim();
        if (string.IsNullOrWhiteSpace(expressionText))
        {
            return children.FirstOrDefault(static child => child is not null);
        }

        char firstCharacter = expressionText[0];
        if (!(char.IsLetter(firstCharacter) || firstCharacter == '_'))
        {
            return children.FirstOrDefault(static child => child is not null);
        }

        if (string.Equals(expressionText, "var", StringComparison.Ordinal) && !context.Symbols.ContainsKey("var"))
        {
            return children.FirstOrDefault(static child => child is not null);
        }

        return CompileIdentifierExpression(expressionText, context);
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
        Expression? directInvocation = children.FirstOrDefault(static child => child is not null);
        if (directInvocation is not null)
        {
            return directInvocation;
        }

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
                MethodInfo[] methods when TryBuildMethodCall(methods, argumentArray, out Expression? overloadCall) => overloadCall,
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
    /// Compiles object creation syntax such as <c>new()</c>, <c>new dynamic</c>, and <c>new Type(...)</c>.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled constructor call expression.</returns>
    private static Expression? CompileObjectCreationExpression(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        string source = GetNodeSourceText(context, nav).Trim();
        if (!source.StartsWith("new", StringComparison.Ordinal))
        {
            return children.FirstOrDefault(static child => child is not null);
        }

        string creationBody = source[3..].Trim();
        if (string.IsNullOrEmpty(creationBody))
        {
            return Expression.New(typeof(ExpandoObject));
        }

        bool hasParentheses = creationBody.Contains('(', StringComparison.Ordinal);
        if (string.Equals(creationBody, "()", StringComparison.Ordinal) ||
            string.Equals(creationBody, "dynamic", StringComparison.Ordinal) ||
            string.Equals(creationBody, "dynamic()", StringComparison.Ordinal))
        {
            return Expression.New(typeof(ExpandoObject));
        }

        string typeName = creationBody;
        Expression[] arguments = [];
        if (hasParentheses)
        {
            int openParen = creationBody.IndexOf('(');
            int closeParen = creationBody.LastIndexOf(')');
            if (openParen < 0 || closeParen <= openParen)
            {
                throw new InvalidOperationException("Invalid object creation syntax.");
            }

            typeName = creationBody[..openParen].Trim();
            if (string.IsNullOrEmpty(typeName))
            {
                string argumentSegment = creationBody[(openParen + 1)..closeParen];
                arguments = [.. ParseInvocationArguments(argumentSegment, context)];
                if (arguments.Length > 0)
                {
                    throw new InvalidOperationException("Target-typed new expressions with arguments are not supported.");
                }

                return Expression.New(typeof(ExpandoObject));
            }

            string typedArgumentSegment = creationBody[(openParen + 1)..closeParen];
            arguments = [.. ParseInvocationArguments(typedArgumentSegment, context)];
        }

        if (string.Equals(typeName, "dynamic", StringComparison.Ordinal))
        {
            return Expression.New(typeof(ExpandoObject));
        }

        Type targetType = ResolveNativeType(typeName, context.ImportedNamespaces);
        return BuildObjectCreation(targetType, arguments);
    }

    // ── Interpolated string handler ───────────────────────────────────────────

    /// <summary>
    /// Ascent handler for <c>interpolated_string</c>: concatenates text segments and
    /// compiled interpolation expressions into a single string.
    /// </summary>
    private static Expression? CompileInterpolatedString(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        List<Expression> parts = children
            .Where(static c => c is not null)
            .Cast<Expression>()
            .Select(static e => e.Type == typeof(string)
                ? e
                : Expression.Call(e, typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!))
            .ToList();

        if (parts.Count == 0) return Expression.Constant(string.Empty);
        if (parts.Count == 1) return parts[0];

        return Expression.Call(
            typeof(string).GetMethod(nameof(string.Concat), [typeof(string[])])!,
            Expression.NewArrayInit(typeof(string), parts));
    }

    // ── Preprocessing helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Replaces compound assignment operators (<c>+=</c>, <c>-=</c>, <c>*=</c>, <c>/=</c>,
    /// <c>%=</c>) and increment/decrement operators (<c>++</c>, <c>--</c>)
    /// with their equivalent simple-assignment + operation forms.
    /// </summary>
    private static string PreprocessCompoundAssignments(string content)
    {
        // Postfix increment/decrement: i++ → i = i + 1 , i-- → i = i - 1
        content = Regex.Replace(
            content,
            @"(\b[A-Za-z_][A-Za-z0-9_]*)\s*(\+\+|--)",
            static m =>
            {
                string name = m.Groups[1].Value;
                string op = m.Groups[2].Value == "++" ? "+" : "-";
                return $"{name} = {name} {op} 1";
            });

        // Prefix increment/decrement: ++i → i = i + 1 , --i → i = i - 1
        content = Regex.Replace(
            content,
            @"(\+\+|--)\s*([A-Za-z_][A-Za-z0-9_]*\b)",
            static m =>
            {
                string op = m.Groups[1].Value == "++" ? "+" : "-";
                string name = m.Groups[2].Value;
                return $"{name} = {name} {op} 1";
            });

        // Compound assignment: x += y → x = x + y
        content = Regex.Replace(
            content,
            @"(\b[A-Za-z_][A-Za-z0-9_.]*)\s*([+\-*/%])\s*=(?!=)",
            static m => $"{m.Groups[1].Value} = {m.Groups[1].Value} {m.Groups[2].Value} ");

        return content;
    }

    /// <summary>
    /// Removes <c>using Namespace.Name;</c> directives from source text.
    /// The namespace names should be extracted with <see cref="ExtractUsingDirectives"/> before calling this.
    /// </summary>
    private static string StripUsingDirectives(string content)
        => Regex.Replace(content, @"\busing\s+[\w.]+\s*;\s*", string.Empty);

    /// <summary>
    /// Extracts namespace names from <c>using Namespace.Name;</c> directives in source text.
    /// </summary>
    private static List<string> ExtractUsingDirectives(string content)
    {
        var namespaces = new List<string>();
        foreach (Match m in Regex.Matches(content, @"\busing\s+([\w.]+)\s*;"))
        {
            namespaces.Add(m.Groups[1].Value);
        }

        return namespaces;
    }

    /// <summary>
    /// Returns the C# keyword alias for a primitive type, or the full CLR type name.
    /// </summary>
    private static string GetCSharpTypeName(Type type)
    {
        return TypeAliases.Left.TryGetValue(type, out string? alias)
            ? alias
            : type.FullName ?? type.Name;
    }
}
