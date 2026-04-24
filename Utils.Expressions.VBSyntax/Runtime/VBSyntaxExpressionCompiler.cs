using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Expressions;
using Utils.Parser.Runtime;

namespace Utils.Expressions.VBSyntax.Runtime;

/// <summary>
/// Compiles VB-like source text into LINQ expression trees.
/// Supports arithmetic, logical and comparison operators, control flow
/// (If/For/For Each/While/Do), variable declarations, and function definitions.
/// </summary>
public sealed partial class VBSyntaxExpressionCompiler : IExpressionCompiler
{
    private readonly VBSyntaxTokenParser _parser = new();

    // ── Maps VB predefined type names to CLR types ────────────────────────────

    private static readonly IReadOnlyDictionary<string, Type> VBTypeMap =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["Boolean"] = typeof(bool),
            ["Byte"]    = typeof(byte),
            ["Char"]    = typeof(char),
            ["Decimal"] = typeof(decimal),
            ["Double"]  = typeof(double),
            ["Integer"] = typeof(int),
            ["Long"]    = typeof(long),
            ["Object"]  = typeof(object),
            ["SByte"]   = typeof(sbyte),
            ["Short"]   = typeof(short),
            ["Single"]  = typeof(float),
            ["String"]  = typeof(string),
            ["UInteger"]= typeof(uint),
            ["ULong"]   = typeof(ulong),
            ["UShort"]  = typeof(ushort),
        };

    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>
    /// Parses and compiles a VB-like expression into a LINQ expression tree.
    /// </summary>
    /// <param name="content">VB-like source text.</param>
    /// <param name="symbols">
    /// Optional symbol table mapping identifier names to existing expressions.
    /// </param>
    /// <returns>Compiled expression tree.</returns>
    public Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ParseNode root = _parser.Parse(content);
        return Compile(root, symbols, content, null);
    }

    /// <summary>
    /// Parses and compiles a VB-like expression using a rich runtime context.
    /// </summary>
    /// <param name="content">VB-like source text.</param>
    /// <param name="context">Runtime context providing and receiving symbols.</param>
    /// <returns>Compiled expression tree.</returns>
    public Expression Compile(string content, VBSyntaxCompilerContext context)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(context);
        ParseNode root = _parser.Parse(content);
        return Compile(root, null, content, context);
    }

    /// <summary>
    /// Compiles a full VB-like source block (multiple statements / declarations).
    /// Public <c>Function</c> and <c>Sub</c> declarations are registered as delegates
    /// in <paramref name="context"/> so they can be retrieved after compilation.
    /// </summary>
    /// <param name="source">VB-like source block.</param>
    /// <param name="context">Runtime context used to store compiled functions.</param>
    /// <returns>Compiled expression for the source block.</returns>
    public Expression CompileSource(string source, VBSyntaxCompilerContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        RegisterDeferredPublicMethods(source, context);
        string wrapped = source.TrimStart().StartsWith("If ", StringComparison.OrdinalIgnoreCase)
                         || source.TrimStart().StartsWith("For ", StringComparison.OrdinalIgnoreCase)
                         || source.TrimStart().StartsWith("While ", StringComparison.OrdinalIgnoreCase)
                         || source.TrimStart().StartsWith("Do", StringComparison.OrdinalIgnoreCase)
                         || source.TrimStart().StartsWith("Function ", StringComparison.OrdinalIgnoreCase)
                         || source.TrimStart().StartsWith("Sub ", StringComparison.OrdinalIgnoreCase)
                         || source.TrimStart().StartsWith("Public ", StringComparison.OrdinalIgnoreCase)
                         || source.TrimStart().StartsWith("Private ", StringComparison.OrdinalIgnoreCase)
                         || source.TrimStart().StartsWith("Dim ", StringComparison.OrdinalIgnoreCase)
            ? source
            : source; // VB has no block delimiters — parse as-is
        ParseNode root = _parser.Parse(wrapped);
        return Compile(root, null, wrapped, context);
    }

    /// <summary>
    /// Compiles a VB-like lambda source into a typed delegate expression.
    /// </summary>
    /// <typeparam name="T">Target delegate type.</typeparam>
    /// <param name="content">VB-like lambda or expression body source.</param>
    /// <returns>Typed lambda expression.</returns>
    public Expression<T> Compile<T>(string content) where T : Delegate
    {
        ArgumentNullException.ThrowIfNull(content);
        Expression result = Compile(content);
        if (result is Expression<T> typed) return typed;
        if (result is LambdaExpression lam) return Expression.Lambda<T>(lam.Body, lam.Parameters);
        throw new InvalidOperationException(
            $"Compiled expression cannot be converted to {typeof(T).Name}.");
    }

    /// <summary>
    /// Compiles a VB-like expression body with explicit parameters into a typed lambda.
    /// </summary>
    /// <typeparam name="T">Target delegate type.</typeparam>
    /// <param name="content">Expression body (no lambda header required).</param>
    /// <param name="parameters">Lambda parameters to bind as symbols.</param>
    /// <returns>Lambda expression compatible with <typeparamref name="T"/>.</returns>
    public LambdaExpression Compile<T>(string content, ParameterExpression[] parameters) where T : Delegate
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(parameters);
        MethodInfo invoke = typeof(T).GetMethod("Invoke")
            ?? throw new InvalidOperationException($"'{typeof(T).Name}' has no Invoke method.");
        Type returnType = invoke.ReturnType;
        var symbols = parameters.ToDictionary(static p => p.Name!, static p => (Expression)p,
            StringComparer.Ordinal);
        Expression body = Compile(content, symbols);
        return Expression.Lambda(ConvertIfNeeded(body, returnType), parameters);
    }

    // ── Internal compilation ──────────────────────────────────────────────────

    /// <summary>
    /// Core compilation entry: builds and runs a <see cref="ParseTreeCompiler{TContext,TResult}"/>
    /// over the provided parse tree.
    /// </summary>
    private Expression Compile(
        ParseNode root,
        IReadOnlyDictionary<string, Expression>? symbols,
        string sourceText,
        VBSyntaxCompilerContext? runtimeContext)
    {
        var blockScope = new Dictionary<ParseNode, List<ParameterExpression>>(
            ReferenceEqualityComparer.Instance);
        var compiler = CreateCompiler();
        var context = new CompilationContext(
            symbols ?? new Dictionary<string, Expression>(StringComparer.Ordinal),
            runtimeContext,
            sourceText,
            this,
            blockScope);
        Expression? compiled = compiler.Compile(root, context);
        return compiled
            ?? throw new InvalidOperationException("Unable to compile the VB-like parse tree.");
    }

    // ── ParseTreeCompiler setup ───────────────────────────────────────────────

    private static ParseTreeCompiler<CompilationContext, Expression> CreateCompiler() =>
        new ParseTreeCompiler<CompilationContext, Expression>()
            .OnError((nav, _) =>
                throw new InvalidOperationException($"Parser error: {((ErrorNode)nav.Node).Message}"))
            // When descending into a method_declaration, mark the context so that child
            // nodes (function body, parameter names, etc.) that reference unknown identifiers
            // do not throw — CompileMethodDeclaration re-compiles the body from source text.
            .OnDescend("method_declaration", (_, ctx) => ctx with { InsideMethodBody = true })
            // Literals
            .OnAscend("NUMBER",         (nav, _)         => CompileNumberLiteral(nav))
            .OnAscend("TRUE",           (_, _)           => Expression.Constant(true))
            .OnAscend("FALSE",          (_, _)           => Expression.Constant(false))
            .OnAscend("NOTHING",        (_, _)           => Expression.Constant(null))
            .OnAscend("STRING_LITERAL", (nav, _)         => Expression.Constant(ParseStringLiteral(nav.Token!.Text)))
            // Identifier / member access / invocation
            .OnAscend("identifier_part", CompileIdentifierPart)
            // Binary operators
            .OnAscend("operation_or",       CompileLogicalOr)
            .OnAscend("operation_and",      CompileLogicalAnd)
            .OnAscend("operation_not",      CompileLogicalNot)
            .OnAscend("operation_equality", CompileEquality)
            .OnAscend("operation_relational", CompileRelational)
            .OnAscend("operation_concat",   CompileConcat)
            .OnAscend("operation_shift",    CompileShift)
            .OnAscend("operation_plus",     CompileAddSub)
            .OnAscend("operation_mul",      CompileMulDivMod)
            .OnAscend("operation_negate",   CompileNegate)
            .OnAscend("operation_power",    CompilePower)
            // Object creation
            .OnAscend("object_creation_expression", CompileObjectCreation)
            // Control flow & declarations
            .OnAscend("assignment_instruction",  CompileAssignment)
            .OnAscend("dim_instruction",         CompileDimInstruction)
            .OnAscend("return_instruction",      CompileReturnInstruction)
            .OnAscend("if_instruction",          CompileIfInstruction)
            .OnAscend("for_instruction",         CompileForInstruction)
            .OnAscend("for_each_instruction",    CompileForEachInstruction)
            .OnAscend("while_instruction",       CompileWhileInstruction)
            .OnAscend("do_loop_instruction",     CompileDoLoopInstruction)
            .OnAscend("method_declaration",      CompileMethodDeclaration)
            // Lambda
            .OnAscend("lambda_expression",       CompileLambdaExpression)
            // Default: pass through the first non-null child
            .DefaultAscend((_, _, children) =>
                children.FirstOrDefault(static c => c is not null));

    // ── Number and string literal helpers ─────────────────────────────────────

    /// <summary>
    /// Compiles a VB number token into a typed constant expression.
    /// </summary>
    private static Expression CompileNumberLiteral(ParseTreeNavigator nav)
    {
        string text = nav.Token!.Text;
        string normalized = text.TrimEnd('f', 'F', 'd', 'D', 'm', 'M', 'u', 'U', 'l', 'L');
        if (normalized.Contains('.') || normalized.Contains('e', StringComparison.OrdinalIgnoreCase))
            return Expression.Constant(double.Parse(normalized, CultureInfo.InvariantCulture));
        return Expression.Constant(int.Parse(normalized, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Parses a VB string literal token, handling doubled-quote escapes.
    /// </summary>
    private static string ParseStringLiteral(string tokenText)
    {
        // Strip surrounding quotes and unescape "" → "
        string inner = tokenText[1..^1];
        return inner.Replace("\"\"", "\"", StringComparison.Ordinal);
    }

    // ── Type resolution ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a VB type name (predefined or qualified) to a CLR <see cref="Type"/>.
    /// </summary>
    /// <param name="typeName">VB type name string.</param>
    /// <returns>Resolved CLR type.</returns>
    internal static Type ResolveVBType(string typeName)
    {
        if (VBTypeMap.TryGetValue(typeName, out Type? mapped))
            return mapped;

        // Strip array suffix
        if (typeName.EndsWith("()", StringComparison.Ordinal))
            return ResolveVBType(typeName[..^2]).MakeArrayType();

        Type? found = Type.GetType(typeName, throwOnError: false)
            ?? Type.GetType("System." + typeName, throwOnError: false);
        return found ?? throw new InvalidOperationException($"Unknown VB type '{typeName}'.");
    }

    // ── Expression conversion helper ──────────────────────────────────────────

    /// <summary>
    /// Converts <paramref name="expression"/> to <paramref name="targetType"/> when types differ.
    /// </summary>
    internal static Expression ConvertIfNeeded(Expression expression, Type targetType)
    {
        if (targetType == typeof(void) || expression.Type == targetType)
            return expression;
        return Expression.Convert(expression, targetType);
    }

    // ── Source-text helper ────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the source text slice covered by <paramref name="nav"/> from the compilation context.
    /// </summary>
    private static string GetNodeSourceText(CompilationContext context, ParseTreeNavigator nav)
    {
        if (nav.Node.Span.Length == 0) return string.Empty;
        int start = nav.Node.Span.Position;
        int length = nav.Node.Span.Length;
        if (start < 0 || start + length > context.SourceText.Length) return string.Empty;
        return context.SourceText.Substring(start, length);
    }

    // ── Internal compilation context ──────────────────────────────────────────

    /// <summary>
    /// Holds all data available during a single compilation pass.
    /// </summary>
    internal sealed record CompilationContext(
        IReadOnlyDictionary<string, Expression> Symbols,
        VBSyntaxCompilerContext? RuntimeContext,
        string SourceText,
        VBSyntaxExpressionCompiler Compiler,
        Dictionary<ParseNode, List<ParameterExpression>> BlockScope,
        bool InsideMethodBody = false);
}
