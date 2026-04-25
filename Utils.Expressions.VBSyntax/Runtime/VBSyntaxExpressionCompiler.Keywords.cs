using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Utils.Expressions;
using Utils.Parser.Runtime;

namespace Utils.Expressions.VBSyntax.Runtime;

public sealed partial class VBSyntaxExpressionCompiler
{
    // ── Dim ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles a <c>Dim x As Type [= initializer]</c> declaration into a variable assignment.
    /// The declared variable is registered in the enclosing block scope.
    /// </summary>
    private static Expression? CompileDimInstruction(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        string source = GetNodeSourceText(context, nav).Trim();

        // Parse: Dim <name> As <type> [= <expr>]
        Match m = Regex.Match(source,
            @"^Dim\s+(?<name>\w+)\s+As\s+(?<type>[\w.()]+)(?:\s*=\s*(?<init>.+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!m.Success)
            return children.FirstOrDefault(static c => c is not null);

        string name = m.Groups["name"].Value;
        string typeName = m.Groups["type"].Value.Trim();
        Type varType = ResolveVBType(typeName);

        ParameterExpression variable = Expression.Variable(varType, name);

        // Variable is tracked in BlockScope when the parent node is registered
        // (block-scope management is handled per-instruction)

        // Build initializer expression
        Expression init;
        if (m.Groups["init"].Success)
        {
            string initSource = m.Groups["init"].Value.Trim();
            Expression raw = context.Compiler.Compile(initSource, BuildChildContext(context, variable, name));
            init = ConvertIfNeeded(raw, varType);
        }
        else
        {
            // Default initializer
            init = varType.IsValueType
                ? Expression.Default(varType)
                : Expression.Constant(null, varType);
        }

        // Register as a mutable symbol in the runtime context
        if (context.RuntimeContext is not null)
            context.RuntimeContext.Set(name, (Expression)variable);

        return Expression.Assign(variable, init);
    }

    // ── Return ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles a <c>Return expression</c> statement.
    /// </summary>
    private static Expression? CompileReturnInstruction(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => children.FirstOrDefault(static c => c is not null);

    // ── Assignment ────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles an assignment <c>target = expression</c>.
    /// </summary>
    private static Expression? CompileAssignment(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        var operands = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (operands.Count < 2)
            return operands.FirstOrDefault();

        Expression target = operands[0];
        Expression value  = ConvertIfNeeded(operands[1], target.Type);

        // VB uses '=' for both assignment and equality.
        // When the LHS is not writeable (e.g. resolved from a read-only symbol),
        // treat '=' as an equality comparison instead.
        bool isWriteable = target is ParameterExpression
            || (target is MemberExpression { Member: System.Reflection.FieldInfo fi } && !fi.IsInitOnly)
            || (target is MemberExpression { Member: System.Reflection.PropertyInfo pi } && pi.CanWrite)
            || target is IndexExpression;

        if (!isWriteable)
            return Expression.Equal(target, value);

        return Expression.Assign(target, value);
    }

    // ── If / ElseIf / Else / End If ──────────────────────────────────────────

    /// <summary>
    /// Compiles an <c>If … Then … ElseIf … Else … End If</c> expression.
    /// </summary>
    private static Expression? CompileIfInstruction(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        string source = GetNodeSourceText(context, nav);

        // Use child expressions: [cond, thenBody..., cond2, body2..., elseBody...]
        // The grammar produces alternating condition / body groups.
        // We rebuild by re-parsing the structure from the child list.
        var parts = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (parts.Count == 0) return Expression.Empty();

        // Walk children to group conditions and bodies
        return BuildIfExpression(source, parts, context);
    }

    /// <summary>
    /// Reassembles an If expression from the flat list of compiled child expressions
    /// by inspecting the source text for If/ElseIf/Else boundaries.
    /// </summary>
    private static Expression BuildIfExpression(
        string source,
        IReadOnlyList<Expression> parts,
        CompilationContext context)
    {
        if (parts.Count < 2) return parts[0];

        // Condition is always first, then one or more body expressions, then optional else
        Expression condition = parts[0];
        Expression thenBody = parts.Count > 1
            ? (parts.Count == 2 ? parts[1] : Expression.Block(parts.Skip(1).SkipLast(
                source.Contains("Else", StringComparison.OrdinalIgnoreCase)
                && !source.Contains("ElseIf", StringComparison.OrdinalIgnoreCase) ? 1 : 0).ToList()))
            : Expression.Empty();

        bool hasElse = Regex.IsMatch(source, @"\bElse\b(?!If)", RegexOptions.IgnoreCase);
        Expression elseBody = hasElse && parts.Count > 2
            ? parts[^1]
            : Expression.Empty();

        // Determine result type
        Type type = thenBody.Type == typeof(void) || elseBody.Type == typeof(void)
            ? typeof(void)
            : thenBody.Type;

        if (type == typeof(void))
        {
            return Expression.IfThenElse(
                ConvertIfNeeded(condition, typeof(bool)),
                ConvertIfNeeded(thenBody, typeof(void)),
                ConvertIfNeeded(elseBody, typeof(void)));
        }

        return Expression.Condition(
            ConvertIfNeeded(condition, typeof(bool)),
            thenBody,
            ConvertIfNeeded(elseBody, thenBody.Type));
    }

    // ── For i = start To end [Step step] … Next ──────────────────────────────

    /// <summary>
    /// Compiles a <c>For variable = start To end [Step step] … Next</c> loop.
    /// </summary>
    private static Expression? CompileForInstruction(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        string source = GetNodeSourceText(context, nav).Trim();

        // Parse header to extract loop variable name, start, end, step (if present)
        Match m = Regex.Match(source,
            @"^For\s+(?<var>\w+)\s*=\s*(?<start>.+?)\s+To\s+(?<end>.+?)(?:\s+Step\s+(?<step>.+?))?\s+(?:(?!\bFor\b|\bWhile\b|\bDo\b|\bIf\b|\bDim\b).)*\s*Next",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Fallback: get children — [start, end, (step)?, body...]
        var parts = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (parts.Count < 3) return Expression.Empty();

        Expression startExpr = parts[0];
        Expression endExpr   = parts[1];
        Expression stepExpr  = parts.Count > 3 && m.Success && m.Groups["step"].Success
            ? parts[2]
            : Expression.Constant(Convert.ChangeType(1, startExpr.Type), startExpr.Type);

        int bodyStart = parts.Count > 3 && m.Success && m.Groups["step"].Success ? 3 : 2;
        List<Expression> bodyExprs = parts.Skip(bodyStart).ToList();

        // Resolve loop variable
        string varName = m.Success ? m.Groups["var"].Value : "i";
        ParameterExpression loopParam = Expression.Variable(startExpr.Type, varName);

        Expression init      = ConvertIfNeeded(startExpr, loopParam.Type);
        Expression condition = Expression.LessThanOrEqual(
            loopParam,
            ConvertIfNeeded(endExpr, loopParam.Type));
        Expression increment = Expression.Assign(
            loopParam,
            Expression.Add(loopParam, ConvertIfNeeded(stepExpr, loopParam.Type)));

        Expression body = bodyExprs.Count == 1
            ? bodyExprs[0]
            : Expression.Block(bodyExprs);

        return ExpressionEx.For(loopParam, init, condition, [increment], body);
    }

    // ── For Each … In … Next ─────────────────────────────────────────────────

    /// <summary>
    /// Compiles a <c>For Each variable [As type] In collection … Next</c> loop.
    /// </summary>
    private static Expression? CompileForEachInstruction(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        string source = GetNodeSourceText(context, nav).Trim();

        Match m = Regex.Match(source,
            @"^For\s+Each\s+(?<var>\w+)(?:\s+As\s+(?<type>[\w.]+))?\s+In\s+",
            RegexOptions.IgnoreCase);

        var parts = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (parts.Count < 2) return Expression.Empty();

        Expression collection = parts[0];
        List<Expression> bodyExprs = parts.Skip(1).ToList();

        string varName = m.Success ? m.Groups["var"].Value : "item";
        Type elemType = m.Success && m.Groups["type"].Success
            ? ResolveVBType(m.Groups["type"].Value)
            : GetElementType(collection.Type);

        ParameterExpression iterator         = Expression.Variable(elemType, varName);
        ParameterExpression collectionParam  = Expression.Variable(collection.Type, "__foreach_source__");
        Expression body = bodyExprs.Count == 1 ? bodyExprs[0] : Expression.Block(bodyExprs);

        Expression foreachExpr = ExpressionEx.ForEach(iterator, collectionParam, body);
        return Expression.Block(
            [collectionParam],
            Expression.Assign(collectionParam, collection),
            foreachExpr);
    }

    // ── While … End While ────────────────────────────────────────────────────

    /// <summary>
    /// Compiles a <c>While condition … End While</c> loop.
    /// </summary>
    private static Expression? CompileWhileInstruction(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        var parts = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (parts.Count < 2) return Expression.Empty();

        Expression condition = ConvertIfNeeded(parts[0], typeof(bool));
        Expression body = parts.Count == 2
            ? parts[1]
            : Expression.Block(parts.Skip(1).ToList());

        return ExpressionEx.While(condition, body);
    }

    // ── Do [While …] … Loop [While …] ────────────────────────────────────────

    /// <summary>
    /// Compiles a <c>Do [While condition] … Loop [While condition]</c> loop.
    /// </summary>
    private static Expression? CompileDoLoopInstruction(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        string source = GetNodeSourceText(context, nav).Trim();
        bool preCondition = Regex.IsMatch(source, @"^Do\s+While\b", RegexOptions.IgnoreCase);

        var parts = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (parts.Count == 0) return Expression.Empty();

        if (preCondition && parts.Count >= 2)
        {
            Expression condition = ConvertIfNeeded(parts[0], typeof(bool));
            Expression body = parts.Count == 2 ? parts[1] : Expression.Block(parts.Skip(1).ToList());
            return ExpressionEx.While(condition, body);
        }

        // Do ... Loop While condition (post-condition)
        if (parts.Count >= 2)
        {
            Expression body = parts.Count == 2
                ? parts[0]
                : Expression.Block(parts.Take(parts.Count - 1).ToList());
            Expression condition = ConvertIfNeeded(parts[^1], typeof(bool));
            return ExpressionEx.Do(condition, body);
        }

        return parts[0];
    }

    // ── Function / Sub declaration ────────────────────────────────────────────

    /// <summary>
    /// Compiles a <c>Function</c> or <c>Sub</c> declaration and registers it in the
    /// runtime context as a compiled delegate.
    /// </summary>
    private static Expression? CompileMethodDeclaration(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        if (context.RuntimeContext is null) return Expression.Empty();

        string source = GetNodeSourceText(context, nav).Trim();

        // Parse: [modifiers] (Function|Sub) name(params) [As returnType] body End (Function|Sub)
        Match m = Regex.Match(source,
            @"(?:Public\s+|Private\s+|Protected\s+|Friend\s+|Shared\s+|Overridable\s+|Overrides\s+)*"
            + @"(?<kind>Function|Sub)\s+(?<name>\w+)\s*"
            + @"\((?<params>[^)]*)\)"
            + @"(?:\s+As\s+(?<ret>[\w.]+))?",
            RegexOptions.IgnoreCase);

        if (!m.Success) return Expression.Empty();

        bool isPublic = Regex.IsMatch(source, @"\bPublic\b", RegexOptions.IgnoreCase)
            || !Regex.IsMatch(source, @"\b(?:Private|Protected|Friend)\b", RegexOptions.IgnoreCase);

        string name      = m.Groups["name"].Value;
        string kind      = m.Groups["kind"].Value;
        string paramsRaw = m.Groups["params"].Value.Trim();
        Type returnType  = kind.Equals("Sub", StringComparison.OrdinalIgnoreCase)
            ? typeof(void)
            : m.Groups["ret"].Success
                ? ResolveVBType(m.Groups["ret"].Value.Trim())
                : typeof(object);

        List<ParameterExpression> parameters = ParseParameterList(paramsRaw);

        // Extract body source (between header and End Function/Sub)
        int bodyStart = source.IndexOf(m.Value, StringComparison.OrdinalIgnoreCase) + m.Value.Length;
        string endToken = kind.Equals("Sub", StringComparison.OrdinalIgnoreCase)
            ? "End Sub"
            : "End Function";
        int bodyEnd = source.LastIndexOf(endToken, StringComparison.OrdinalIgnoreCase);
        if (bodyEnd < bodyStart) bodyEnd = source.Length;
        string bodySource = source[bodyStart..bodyEnd].Trim();

        // Build delegate type
        Type[] paramTypes = [.. parameters.Select(static p => p.Type), returnType];
        Type delegateType = Expression.GetDelegateType(paramTypes.Length == 1
            ? paramTypes
            : paramTypes);

        // Compile body
        var bodyContext = new VBSyntaxCompilerContext();
        foreach (var kv in context.RuntimeContext.Symbols)
            bodyContext.Set(kv.Key, kv.Value);
        foreach (var p in parameters)
            bodyContext.Set(p.Name!, (Expression)p);

        Expression body = context.Compiler.CompileSource(bodySource, bodyContext);
        Expression converted = returnType == typeof(void)
            ? body
            : ConvertIfNeeded(body, returnType);

        LambdaExpression lambda = Expression.Lambda(delegateType, converted, parameters);
        Delegate compiled = lambda.Compile();

        if (isPublic)
            context.RuntimeContext.Set(name, compiled);

        return Expression.Empty();
    }

    // ── Lambda expression: Function(x As Double) x * 2 ───────────────────────

    /// <summary>
    /// Compiles an inline <c>Function(params) expression</c> lambda.
    /// </summary>
    private static Expression? CompileLambdaExpression(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        string source = GetNodeSourceText(context, nav).Trim();

        // Parse: Function(params) body
        Match m = Regex.Match(source,
            @"^Function\s*\((?<params>[^)]*)\)\s*(?<body>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!m.Success)
            return children.FirstOrDefault(static c => c is not null);

        List<ParameterExpression> parameters = ParseParameterList(m.Groups["params"].Value.Trim());
        string bodySource = m.Groups["body"].Value.Trim();

        var lambdaContext = new VBSyntaxCompilerContext();
        if (context.RuntimeContext is not null)
            foreach (var kv in context.RuntimeContext.Symbols)
                lambdaContext.Set(kv.Key, kv.Value);
        foreach (var p in parameters)
            lambdaContext.Set(p.Name!, (Expression)p);

        Expression body = context.Compiler.Compile(bodySource, lambdaContext);
        return Expression.Lambda(body, parameters);
    }

    // ── Deferred public method registration ──────────────────────────────────

    /// <summary>
    /// Pre-scans source for public <c>Function</c>/<c>Sub</c> declarations and registers
    /// placeholder delegates to enable forward references during compilation.
    /// </summary>
    private static void RegisterDeferredPublicMethods(string source, VBSyntaxCompilerContext context)
    {
        foreach (Match m in Regex.Matches(source,
            @"(?:Public\s+)?Function\s+(?<name>\w+)\s*\((?<params>[^)]*)\)\s+As\s+(?<ret>[\w.]+)",
            RegexOptions.IgnoreCase))
        {
            string name = m.Groups["name"].Value;
            if (context.TryGet(name, out _)) continue;

            List<ParameterExpression> parameters = ParseParameterList(m.Groups["params"].Value.Trim());
            Type returnType = ResolveVBType(m.Groups["ret"].Value.Trim());
            Type[] paramTypes = [.. parameters.Select(static p => p.Type), returnType];
            Type delegateType = Expression.GetDelegateType(paramTypes);

            var holder = new DeferredHolder();
            Delegate deferred = CreateDeferredDelegate(delegateType, parameters, holder, returnType);
            context.Set(name, deferred);
            context.Set(DeferredKey(name), holder);
        }
    }

    // ── Parameter list parser ─────────────────────────────────────────────────

    /// <summary>
    /// Parses a VB parameter list string (e.g. <c>x As Integer, y As Double</c>)
    /// into a list of <see cref="ParameterExpression"/> instances.
    /// </summary>
    private static List<ParameterExpression> ParseParameterList(string paramsRaw)
    {
        var result = new List<ParameterExpression>();
        if (string.IsNullOrWhiteSpace(paramsRaw)) return result;

        foreach (string part in paramsRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Match pm = Regex.Match(part.Trim(), @"(?<name>\w+)\s+As\s+(?<type>[\w.]+)", RegexOptions.IgnoreCase);
            if (!pm.Success) continue;
            Type paramType = ResolveVBType(pm.Groups["type"].Value.Trim());
            result.Add(Expression.Parameter(paramType, pm.Groups["name"].Value));
        }

        return result;
    }

    // ── Deferred delegate infrastructure ─────────────────────────────────────

    /// <summary>
    /// Mutable holder used to support forward-reference function calls.
    /// </summary>
    private sealed class DeferredHolder
    {
        /// <summary>Gets or sets the compiled delegate target.</summary>
        public Delegate? Target { get; set; }
    }

    private static string DeferredKey(string name) => $"__vb_deferred_{name}__";

    /// <summary>
    /// Creates a delegate that dispatches to <paramref name="holder"/>'s target at call time.
    /// </summary>
    private static Delegate CreateDeferredDelegate(
        Type delegateType,
        IReadOnlyList<ParameterExpression> parameters,
        DeferredHolder holder,
        Type returnType)
    {
        var holderConst = Expression.Constant(holder);
        var argsArray = Expression.NewArrayInit(typeof(object),
            parameters.Select(static p => (Expression)Expression.Convert(p, typeof(object))));
        var invoke = Expression.Call(
            typeof(VBSyntaxExpressionCompiler).GetMethod(
                nameof(InvokeDeferred),
                BindingFlags.NonPublic | BindingFlags.Static)!,
            holderConst, argsArray);

        Expression body = returnType == typeof(void)
            ? Expression.Block(invoke, Expression.Empty())
            : ConvertIfNeeded(invoke, returnType);

        return Expression.Lambda(delegateType, body, parameters).Compile();
    }

    private static object? InvokeDeferred(DeferredHolder holder, object?[] arguments)
    {
        if (holder.Target is null)
            throw new InvalidOperationException("The function body has not been compiled yet.");
        return holder.Target.DynamicInvoke(arguments);
    }

    // ── Misc helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the element type for a collection/array type.
    /// </summary>
    private static Type GetElementType(Type collectionType)
    {
        if (collectionType.IsArray) return collectionType.GetElementType()!;
        foreach (Type iface in collectionType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return iface.GetGenericArguments()[0];
        }

        return typeof(object);
    }

    /// <summary>
    /// Builds a child <see cref="VBSyntaxCompilerContext"/> that inherits symbols from
    /// <paramref name="parent"/> and additionally exposes <paramref name="variable"/>.
    /// </summary>
    private static VBSyntaxCompilerContext BuildChildContext(
        CompilationContext parent,
        ParameterExpression variable,
        string name)
    {
        var ctx = new VBSyntaxCompilerContext();
        if (parent.RuntimeContext is not null)
            foreach (var kv in parent.RuntimeContext.Symbols)
                ctx.Set(kv.Key, kv.Value);
        foreach (var kv in parent.Symbols)
            ctx.Set(kv.Key, kv.Value);
        ctx.Set(name, (Expression)variable);
        return ctx;
    }
}
