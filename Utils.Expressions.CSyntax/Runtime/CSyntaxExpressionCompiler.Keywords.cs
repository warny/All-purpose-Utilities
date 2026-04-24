using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Utils.Objects;
using Utils.Parser.Runtime;

using Utils.Expressions;
namespace Utils.Expressions.CSyntax.Runtime;

/// <summary>
/// Compiles C-like parse trees into LINQ expression trees by using
/// <see cref="ParseTreeCompiler{TContext, TResult}"/>.
/// </summary>
public sealed partial class CSyntaxExpressionCompiler
{
    /// <summary>
    /// Compiles an <c>if</c>/<c>else</c> control structure.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Conditional expression.</returns>
    private static Expression? CompileIfInstruction(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        List<Expression> expressions = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (expressions.Count < 2)
        {
            return expressions.FirstOrDefault();
        }

        Expression test = EnsureBoolean(expressions[0]);
        Expression whenTrue = expressions[1];
        if (expressions.Count < 3)
        {
            return Expression.IfThen(test, whenTrue);
        }

        Expression whenFalse = expressions[2];
        if (whenTrue.Type == whenFalse.Type)
        {
            return Expression.Condition(test, whenTrue, whenFalse);
        }

        if (whenTrue.Type == typeof(void) || whenFalse.Type == typeof(void))
        {
            return Expression.IfThenElse(test, EnsureVoidCompatible(whenTrue), EnsureVoidCompatible(whenFalse));
        }

        return Expression.Condition(test, whenTrue, ConvertIfNeeded(whenFalse, whenTrue.Type));
    }

    /// <summary>
    /// Compiles a <c>while</c> loop structure.
    /// </summary>
    private static Expression? CompileWhileInstruction(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        List<Expression> expressions = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (expressions.Count < 2)
        {
            return expressions.FirstOrDefault();
        }

        return ExpressionEx.While(EnsureBoolean(expressions[0]), expressions[1]);
    }

    /// <summary>
    /// Compiles a <c>do...while</c> loop structure.
    /// </summary>
    private static Expression? CompileDoWhileInstruction(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        List<Expression> expressions = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (expressions.Count < 2)
        {
            return expressions.FirstOrDefault();
        }

        return ExpressionEx.Do(EnsureBoolean(expressions[1]), expressions[0]);
    }

    /// <summary>
    /// Compiles a <c>for</c> loop structure.
    /// </summary>
    private static Expression? CompileForInstruction(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
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

        source = ExtractInstructionSource(source, "for");

        (string header, string bodySource) = ExtractLoopHeaderAndBody(source);
        List<string> headerParts = SplitTopLevel(header, ';').Select(static p => p.Trim()).ToList();
        while (headerParts.Count < 3)
        {
            headerParts.Add(string.Empty);
        }

        string initText = headerParts[0];
        ParameterExpression? namedIterator = null;
        Expression? initValue = null;
        Dictionary<string, Expression>? forSymbols = null;

        // Detect variable declaration in init: "type name = value"
        if (initText.Length > 0 && TryParseForInitDeclaration(initText, out string declTypeName, out string declVarName, out string declValueText))
        {
            Type varType = string.Equals(declTypeName, "var", StringComparison.Ordinal)
                ? typeof(object)
                : ResolveNativeType(declTypeName, context.ImportedNamespaces);
            namedIterator = Expression.Variable(varType, declVarName);
            initValue = ConvertIfNeeded(CompileSubExpression(declValueText, context, null), varType);
            forSymbols = new Dictionary<string, Expression>(StringComparer.Ordinal) { [declVarName] = namedIterator };
        }

        Expression rawInit = initText.Length == 0
            ? Expression.Constant(0d)
            : initValue ?? CompileSubExpression(initText, context, null);

        Expression testExpression = headerParts[1].Length == 0
            ? Expression.Constant(true)
            : EnsureBoolean(CompileSubExpression(headerParts[1], context, forSymbols));
        string nextExpressionText = NormalizeForIteratorStep(headerParts[2]);
        Expression[] nextExpressions = nextExpressionText.Length == 0
            ? []
            : [CompileSubExpression(nextExpressionText, context, forSymbols)];
        Expression bodyExpression = bodySource.Length == 0
            ? Expression.Empty()
            : CompileSubExpression(NormalizeLoopBodySource(bodySource), context, forSymbols);

        ParameterExpression iterator = namedIterator
            ?? Expression.Variable(rawInit.Type == typeof(void) ? typeof(double) : rawInit.Type, "__for_iterator__");
        Expression finalInit = initValue
            ?? (rawInit.Type == iterator.Type ? rawInit : ConvertIfNeeded(rawInit, iterator.Type));

        return ExpressionEx.For(iterator, finalInit, testExpression, nextExpressions, bodyExpression);
    }

    /// <summary>
    /// Tries to parse a for-loop init string as a variable declaration of the form
    /// <c>type name = value</c>.
    /// </summary>
    private static bool TryParseForInitDeclaration(string text, out string typeName, out string varName, out string valueText)
    {
        Match m = Regex.Match(text.Trim(),
            @"^([A-Za-z_][A-Za-z0-9_]*(?:<[^>]*>)?)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+)$");
        if (m.Success)
        {
            typeName = m.Groups[1].Value;
            varName = m.Groups[2].Value;
            valueText = m.Groups[3].Value;
            return true;
        }
        typeName = varName = valueText = string.Empty;
        return false;
    }

    /// <summary>
    /// Compiles a <c>foreach</c> loop structure.
    /// </summary>
    private static Expression? CompileForeachInstruction(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
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

        (string header, string bodySource) = ExtractLoopHeaderAndBody(source);
        Match inKeyword = Regex.Match(header, @"\bin\b");
        if (!inKeyword.Success)
        {
            throw new InvalidOperationException("Unable to parse foreach header.");
        }
        int inIndex = inKeyword.Index;

        string leftPart = header[..inIndex].Trim();
        string enumerableSource = header[(inIndex + inKeyword.Length)..].Trim();
        string[] iteratorParts = leftPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (iteratorParts.Length == 0)
        {
            throw new InvalidOperationException("Unable to parse foreach iterator.");
        }

        string iteratorName = iteratorParts[^1];
        Expression enumerableExpression = CompileSubExpression(enumerableSource, context, null);
        Type iteratorType = iteratorParts.Length > 1 && iteratorParts[0] != "var"
            ? ResolveNativeType(iteratorParts[0])
            : ResolveEnumerableElementType(enumerableExpression.Type);
        ParameterExpression iterator = Expression.Variable(iteratorType, iteratorName);
        ParameterExpression enumerableVariable = Expression.Variable(enumerableExpression.Type, "__foreach_source__");
        Expression body = bodySource.Length == 0
            ? Expression.Empty()
            : CompileSubExpression(NormalizeLoopBodySource(bodySource), context, new Dictionary<string, Expression>(StringComparer.Ordinal)
            {
                [iteratorName] = iterator,
            });

        Expression foreachBody = ExpressionEx.ForEach(iterator, enumerableVariable, body);
        return Expression.Block(
            [enumerableVariable],
            Expression.Assign(enumerableVariable, enumerableExpression),
            foreachBody);
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
        var bodyContext = new ExpressionCompilerContext();
        foreach (KeyValuePair<string, object?> runtimeSymbol in context.RuntimeContext.Symbols)
        {
            bodyContext.Set(runtimeSymbol.Key, runtimeSymbol.Value);
        }
        foreach (KeyValuePair<string, Expression> entry in signature.ParameterSymbols)
        {
            bodyContext.Set(entry.Key, entry.Value);
        }

        Type delegateType = Expression.GetDelegateType([.. signature.Parameters.Select(static p => p.Type), signature.ReturnType]);
        DeferredDelegateHolder? deferredHolder = null;
        Delegate? deferredDelegate = null;
        if (signature.IsPublic)
        {
            if (context.RuntimeContext.TryGet(GetDeferredHolderSymbolName(signature.Name), out object? holderObject)
                && holderObject is DeferredDelegateHolder existingHolder
                && context.RuntimeContext.TryGet(signature.Name, out object? delegateObject)
                && delegateObject is Delegate existingDelegate)
            {
                deferredHolder = existingHolder;
                deferredDelegate = existingDelegate;
            }
            else
            {
                deferredHolder = new DeferredDelegateHolder();
                deferredDelegate = CreateDeferredDelegate(delegateType, signature.Parameters, deferredHolder, signature.ReturnType);
                context.RuntimeContext.Set(signature.Name, deferredDelegate);
                context.RuntimeContext.Set(GetDeferredHolderSymbolName(signature.Name), deferredHolder);
            }

            bodyContext.Set(signature.Name, deferredDelegate);
        }

        Expression body = context.Compiler.CompileSource(signature.BodySource, bodyContext);
        LambdaExpression lambda = Expression.Lambda(delegateType, ConvertIfNeeded(body, signature.ReturnType), signature.Parameters);
        Delegate compiledDelegate = lambda.Compile();

        if (deferredHolder is not null && deferredDelegate is not null)
        {
            deferredHolder.Target = compiledDelegate;
            context.RuntimeContext.Set(signature.Name, deferredDelegate);
            context.RuntimeContext.Set(GetDeferredHolderSymbolName(signature.Name), deferredHolder);
            return Expression.Empty();
        }

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

}
