using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Expressions;

public static class ExpressionEx
{
    /// <summary>
    /// Create an expression call on an object given the specified arguments
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="name"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public static MethodCallExpression CreateExpressionCall(Expression expression, string name, params Expression[] arguments)
        => CreateExpressionCall(expression, name, BindingFlags.Public | BindingFlags.Instance, arguments);

    /// <summary>
    /// Create an expression call on an object given the specified arguments
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="name"></param>
    /// <param name="bindingFlags"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public static MethodCallExpression CreateExpressionCall(Expression expression, string name, BindingFlags bindingFlags, params Expression[] arguments)
    {
        bindingFlags &= BindingFlags.Public | BindingFlags.NonPublic;

        Type[] argumentTypes = arguments.Select(a => a.Type).ToArray();
        try
        {
            var method = expression.Type.GetMethod(name, bindingFlags | BindingFlags.Instance, null, argumentTypes, null) ?? throw new MissingMethodException(name);
            return Expression.Call(expression, method, arguments);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }

    /// <summary>
    /// Create a static expression call given the specified arguments
    /// </summary>
    /// <param name="name"></param>
    /// <param name="bindingFlags"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public static MethodCallExpression CreateExpressionCall(Type type, string name, BindingFlags bindingFlags, params Expression[] arguments)
    {
        bindingFlags &= BindingFlags.Public | BindingFlags.NonPublic;

        Type[] argumentTypes = arguments.Select(a => a.Type).ToArray();
        var method = type.GetMethod(name, bindingFlags | BindingFlags.Static, null, argumentTypes, null) ?? throw new MissingMethodException(name);
        return Expression.Call(null, method, arguments);
    }


    /// <summary>
    /// Try to get a conversion method from an expression or the target type
    /// This function searches 
    /// </summary>
    /// <param name="source">Source expression to get a convert from</param>
    /// <param name="outType">Target Type to get a convert to</param>
    /// <param name="builder">Resulting expression</param>
    /// <returns></returns>
    public static bool TryGetConverterMethod(Expression source, Type outType, out Expression builder)
        => TryGetConverterMethod(source, outType, BindingFlags.Public, out builder);

    /// <summary>
    /// Try to get a conversion method from an expression or the target type
    /// This function searches 
    /// </summary>
    /// <param name="source">Source expression to get a convert from</param>
    /// <param name="outType">Target Type to get a convert to</param>
    /// <param name="builder">Resulting expression</param>
    /// <returns></returns>
    public static bool TryGetConverterMethod(Expression source, Type outType, BindingFlags bindingFlags, out Expression builder)
    {
        bindingFlags &= BindingFlags.Public | BindingFlags.NonPublic;

        var methodsInstance = source.Type
            .GetMethods(bindingFlags | BindingFlags.Instance)
            .Where(m => m.ReturnType == outType && m.GetParameters().Length == 0)
            .ToArray();

        foreach (var method in new MethodInfo[] {
            methodsInstance.FirstOrDefault(m => m.Name.StartsWith("As")),
            methodsInstance.FirstOrDefault(m => m.Name.StartsWith("To")),
            methodsInstance.FirstOrDefault(),
        }.Where(m => m is not null))
        {
            builder = Expression.Call(source, method);
            return true;
        }

        var methodsStatic = source.Type
            .GetMethods(bindingFlags | BindingFlags.Static)
            .Where(m => m.ReturnType == outType && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == source.Type)
            .ToArray();

        var methodsTarget = outType
            .GetMethods(bindingFlags | BindingFlags.Static)
            .Where(m => m.ReturnType == outType && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == source.Type)
            .ToArray();

        foreach (var method in new MethodInfo[] {
            methodsStatic.FirstOrDefault(m => m.Name.StartsWith("As")),
            methodsStatic.FirstOrDefault(m => m.Name.StartsWith("To")),
            methodsStatic.FirstOrDefault(),
            methodsTarget.FirstOrDefault(m => m.Name.StartsWith("From")),
            methodsTarget.FirstOrDefault(m => m.Name.StartsWith("Parse")),
            methodsTarget.FirstOrDefault(),
        }.Where(m => m is not null))
        {
            builder = Expression.Call(method, source);
            return true;
        }

        var constructorTarget = outType
            .GetConstructors(bindingFlags | BindingFlags.Instance)
            .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == source.Type)
            .Where(c => c is not null)
            .ToArray();

        foreach (var constructor in constructorTarget)
        {
            builder = Expression.New(constructor, source);
            return true;
        }

        builder = null;
        return false;
    }


    public static Expression ForEach(ParameterExpression iterator, ParameterExpression enumerable, Expression iteration, LabelTarget breakLoop = null, LabelTarget continueLoop = null)
    {
        breakLoop ??= Expression.Label("__break__");
        continueLoop ??= Expression.Label("__continue__");

        Type enumerableType = enumerable.Type.GetInterfaces().FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ?? enumerable.Type.GetInterfaces().FirstOrDefault(i => i == typeof(IEnumerable));
        if (enumerableType != null) throw new InvalidOperationException($"{enumerable.Type} type is not enumerable");

        var enumerableTyped = Expression.Variable(enumerableType, "__enumerable__");
        var getEnumeratorExpression = CreateExpressionCall(enumerableTyped, "GetEnumerator");
        var enumerator = Expression.Variable(getEnumeratorExpression.Type, "__enumerator__");
        var getEnumerator = Expression.ConvertChecked(enumerable, enumerableType);

        var iterateExpression = CreateExpressionCall(enumerator, "MoveNext");


        return Expression.Block(
            [enumerableTyped, iterator, enumerator],
            [
                Expression.Assign(enumerableTyped, Expression.Convert(enumerable, enumerableTyped.Type)),
                Expression.Assign(enumerator, getEnumeratorExpression),
                Expression.Loop(
                    Expression.Block(
                        Expression.IfThen(Expression.Not(iterateExpression), Expression.Goto(breakLoop)),
                        Expression.Assign(iterator, Expression.Convert(CreateExpressionCall(enumerator, "Current"), iterator.Type)),
                        iteration
                    ),
                    breakLoop,
                    continueLoop
                )
            ]
        );
    }

    public static Expression For(ParameterExpression iterator, Expression init, Expression test, Expression[] next, Expression iteration, LabelTarget breakLoop = null, LabelTarget continueLoop = null)
    {
        breakLoop ??= Expression.Label("__break__");
        continueLoop ??= Expression.Label("__continue__");

        return Expression.Block(
            [iterator],
            [
                Expression.Assign(iterator, init),
                Expression.Loop(
                    Expression.Block(
                        [
                        Expression.IfThen(Expression.Not(test), Expression.Goto(breakLoop)),
                        iteration,
                        .. next
                        ]
                    ),
                    breakLoop, continueLoop
                )
            ]
        );
    }

    public static Expression While(Expression test, Expression iteration, LabelTarget breakLoop = null, LabelTarget continueLoop = null)
    {
        breakLoop ??= Expression.Label("__break__");
        continueLoop ??= Expression.Label("__continue__");

        return Expression.Loop(
            Expression.Block(
                Expression.IfThen(Expression.Not(test), Expression.Goto(breakLoop)),
                iteration
            ),
            breakLoop, continueLoop
        );
    }

    public static Expression Do(Expression test, Expression iteration, LabelTarget breakLoop = null, LabelTarget continueLoop = null)
    {
        breakLoop ??= Expression.Label("__break__");
        continueLoop ??= Expression.Label("__continue__");

        return Expression.Loop(
            Expression.Block(
                iteration,
                Expression.IfThen(Expression.Not(test), Expression.Goto(breakLoop))
            ),
            breakLoop, continueLoop
        );
    }

    public static Expression CreateMemberExpression(Expression expression, string memberName, params Expression[] arguments)
    => CreateMemberExpression(expression, memberName, BindingFlags.Public);

    public static Expression CreateMemberExpression(Expression expression, string memberName, BindingFlags bindingFlags, params Expression[] arguments)
    {
        bindingFlags &= BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.NonPublic;
        bindingFlags |= BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        var nameComparer = (bindingFlags & BindingFlags.IgnoreCase) == BindingFlags.IgnoreCase
            ? StringComparer.InvariantCultureIgnoreCase
            : StringComparer.InvariantCulture;
        Type[] argumentTypes = arguments.Select(a => a.Type).ToArray();
        foreach (var member in expression.Type.GetMembers(bindingFlags).Where(m => nameComparer.Compare(m.Name, memberName) == 0))
        {
            switch (member)
            {
                case PropertyInfo p:
                    {
                        var indexParameters = p.GetIndexParameters();
                        if (arguments.Length != indexParameters.Length) continue;
                        if (arguments.Length == 0)
                        {
                            return Expression.Property(expression, p);
                        }
                        var indexTypes = indexParameters.Select(p => p.ParameterType).ToArray();
                        if (!CompareTypes(argumentTypes, indexTypes)) continue;
                        return Expression.Property(expression, p, arguments);
                    };
                case FieldInfo f:
                    {
                        if (arguments.Length != 0) continue;
                        return Expression.Field(expression, f);
                    };
                case MethodInfo m:
                    {
                        var methodParameters = m.GetParameters();
                        if (arguments.Length != methodParameters.Length) continue;
                        var argumentsTypes = methodParameters.Select(p => p.ParameterType).ToArray();
                        if (!CompareTypes(argumentTypes, argumentsTypes)) continue;
                        return Expression.Call(expression, m, arguments);
                    };
            }
        }
        throw new MissingMemberException(memberName);
    }

    public static Expression CreateMemberExpression(Expression expression, MemberInfo member, params Expression[] arguments)
    {
        Type[] argumentTypes = arguments.Select(a => a.Type).ToArray();
        switch (member)
        {
            case PropertyInfo p:
                {
                    var indexParameters = p.GetIndexParameters();
                    if (arguments.Length != indexParameters.Length) throw new ArgumentException("bad argument count", nameof(arguments));
                    if (arguments.Length == 0)
                    {
                        return Expression.Property(expression, p);
                    }
                    var indexTypes = indexParameters.Select(p => p.ParameterType).ToArray();
                    if (!CompareTypes(argumentTypes, indexTypes)) throw new ArgumentException("bad argument count", nameof(arguments));
                    return Expression.Property(expression, p, arguments);
                };
            case FieldInfo f:
                {
                    if (arguments.Length != 0) throw new ArgumentException("bad argument count", nameof(arguments));
                    return Expression.Field(expression, f);
                };
            case MethodInfo m:
                {
                    var methodParameters = m.GetParameters();
                    if (arguments.Length != methodParameters.Length) throw new ArgumentException("bad argument count", nameof(arguments));
                    var argumentsTypes = methodParameters.Select(p => p.ParameterType).ToArray();
                    if (!CompareTypes(argumentTypes, argumentsTypes)) throw new ArgumentException("bad argument count", nameof(arguments));
                    return Expression.Call(expression, m, arguments);
                };
        }
        throw new NotSupportedException("");
    }

    public static Expression CreateStaticExpression(Type type, string memberName, params Expression[] arguments)
        => CreateStaticExpression(type, memberName, BindingFlags.Public);

    public static Expression CreateStaticExpression(Type type, string memberName, BindingFlags bindingFlags, params Expression[] arguments)
    {
        bindingFlags &= BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.NonPublic;
        bindingFlags |= BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        var nameComparer = (bindingFlags & BindingFlags.IgnoreCase) == BindingFlags.IgnoreCase
            ? StringComparer.InvariantCultureIgnoreCase
            : StringComparer.InvariantCulture;
        Type[] argumentTypes = arguments.Select(a => a.Type).ToArray();
        foreach (var member in type.GetMembers(bindingFlags).Where(m => nameComparer.Compare(m.Name, memberName) == 0))
        {
            switch (member)
            {
                case PropertyInfo p:
                    {
                        var indexParameters = p.GetIndexParameters();
                        if (arguments.Length != indexParameters.Length) continue;
                        if (arguments.Length == 0)
                        {
                            return Expression.Property(null, p);
                        }
                        var indexTypes = indexParameters.Select(p => p.ParameterType).ToArray();
                        if (!CompareTypes(argumentTypes, indexTypes)) continue;
                        return Expression.Property(null, p, arguments);
                    };
                case FieldInfo f:
                    {
                        if (arguments.Length != 0) continue;
                        return Expression.Field(null, f);
                    };
                case MethodInfo m:
                    {
                        var methodParameters = m.GetParameters();
                        if (arguments.Length != methodParameters.Length) continue;
                        var argumentsTypes = methodParameters.Select(p => p.ParameterType).ToArray();
                        if (!CompareTypes(argumentTypes, argumentsTypes)) continue;
                        return Expression.Call(null, m, arguments);
                    };
            }
        }
        throw new MissingMemberException(memberName);
    }

    private static bool CompareTypes(Type[] typeToReplace, Type[] replacementTypes)
    {
        if (replacementTypes.Length != typeToReplace.Length) return false;
        for (int i = 0; i < replacementTypes.Length; i++)
        {
            if (!typeToReplace[i].IsAssignableFrom(replacementTypes[i])) return false;
        }
        return true;
    }

    public static bool TryGetConverter(Expression expressionLeft, Expression expressionRight, out Expression converter, BindingFlags bindingFlags = BindingFlags.Public)
    {
        return TryGetConverter(expressionLeft.Type, expressionRight, out converter, bindingFlags);
    }

    public static bool TryGetConverter((Type targetType, Expression expressionRight)[] expressions, out Expression converter, BindingFlags bindingFlags = BindingFlags.Public)
    {
        foreach (var expression in expressions)
        {
            if (TryGetConverter(expression.targetType, expression.expressionRight, out converter, bindingFlags)) return true;
        }
        converter = null;
        return false;
    }

    public static bool TryGetConverter(Type targetType, Expression expressionRight, out Expression converter, BindingFlags bindingFlags = BindingFlags.Public)
    {
        // si le type est le même, on renvoie l'expression telle quelle
        if (targetType == expressionRight.Type) { converter = expressionRight; return true; }
        // si le type est différent, on ne peut pas l'affecter directement, il faut convertir
        if (targetType.IsAssignableFrom(expressionRight.Type)) { converter = Expression.Convert(expressionRight, targetType); return true; }

        //on recherche si le type a une méthode qui converti dans le type donné
        MethodInfo methodInfo = null;
        foreach (var method in expressionRight.Type.GetMethods(bindingFlags | BindingFlags.Instance))
        {
            if (!targetType.IsAssignableFrom(method.ReturnType)) continue;
            var parameters = method.GetParameters();
            if (parameters.Length != 0) continue;
            methodInfo = method;
            break;
        }
        if (methodInfo != null)
        {
            converter = Expression.Call(expressionRight, methodInfo);
            if (converter.Type != targetType) converter = Expression.Convert(converter, targetType);
            return true;
        }

        // on recherche les constructeurs
        // si on trouve le constructeur qui a le type exact, on l'utilise
        // sinon, on prend en constructeur dont le type est compatible
        ConstructorInfo constructorInfo = null;
        Type parameterType = null;
        foreach (var constructor in targetType.GetConstructors(bindingFlags | BindingFlags.Instance))
        {
            var parameters = constructor.GetParameters();
            if (parameters.Length != 1) continue;
            if (!parameters[0].ParameterType.IsAssignableFrom(expressionRight.Type)) continue;
            if (parameters[0].ParameterType == expressionRight.Type) { converter = Expression.New(constructor, expressionRight); return true; }
            constructorInfo = constructor;
            parameterType = parameters[0].ParameterType;
        }
        if (constructorInfo != null)
        {
            converter = Expression.New(constructorInfo, Expression.Convert(expressionRight, parameterType));
            return true;
        }

        // on recherche les méthodes static
        // si on trouve la méthode qui a le type exact, on l'utilise
        // sinon, on prend la méthode dont le type est compatible
        methodInfo = null;
        parameterType = null;
        foreach (var method in targetType.GetMethods(bindingFlags | BindingFlags.Static))
        {
            var parameters = method.GetParameters();
            if (!targetType.IsAssignableFrom(method.ReturnType)) continue;
            if (parameters.Length != 1) continue;
            if (!parameters[0].ParameterType.IsAssignableFrom(expressionRight.Type)) continue;
            methodInfo = method;
            parameterType = parameters[0].ParameterType;
            if (targetType == method.ReturnType && parameters[0].ParameterType == expressionRight.Type) break;
        }
        if (methodInfo != null)
        {
            if (parameterType != expressionRight.Type) expressionRight = Expression.Convert(expressionRight, parameterType);
            converter = Expression.Call(null, methodInfo, expressionRight);
            if (converter.Type != targetType) converter = Expression.Convert(converter, targetType);
            return true;
        }

        converter = null;
        return false;
    }

    public static Expression ExtractInnerExpression(LambdaExpression lambda, params Expression[] replacingExpressions)
    {
        if (lambda.Parameters.Count != replacingExpressions.Length) throw new ArgumentException($"{nameof(replacingExpressions)} must be as long as {nameof(lambda)} arguments count", nameof(replacingExpressions));

        var replacements = EnumerableEx.Zip(lambda.Parameters, replacingExpressions).ToDictionary(r => r.Item1, r => r.Item2);
        if (replacements.Any(r => !r.Key.Type.IsAssignableFrom(r.Value.Type))) throw new ArgumentException($"{nameof(replacingExpressions)} types must be compatible with {nameof(lambda)} arguments types", nameof(replacingExpressions));

        var labels = new Dictionary<LabelTarget, LabelTarget>();

        return Copy(lambda.Body, replacements, labels);
    }

    private static Expression Copy(
        Expression expression,
        IReadOnlyDictionary<ParameterExpression, Expression> replacements,
        IDictionary<LabelTarget, LabelTarget> labels
    )
    {
        return expression switch
        {
            ParameterExpression pe => replacements.TryGetValue(pe, out var replacement) ? replacement : pe,

            ConstantExpression ce => Expression.Constant(ce.Value, ce.Type),
            NewArrayExpression nae when nae.NodeType == ExpressionType.NewArrayInit => Expression.NewArrayInit(nae.Type, nae.Expressions.Select(a => Copy(a, replacements, labels))),
            NewArrayExpression nae when nae.NodeType == ExpressionType.NewArrayBounds => Expression.NewArrayBounds(nae.Type, nae.Expressions.Select(a => Copy(a, replacements, labels))),
            NewExpression ne => Expression.New(ne.Constructor, ne.Arguments.Select(a => Copy(a, replacements, labels))),

            UnaryExpression ue when ue.NodeType == ExpressionType.Unbox => Expression.Unbox(Copy(ue.Operand, replacements, labels), ue.Type),
            UnaryExpression ue when ue.NodeType == ExpressionType.Convert => Expression.Convert(Copy(ue.Operand, replacements, labels), ue.Type),
            UnaryExpression ue when ue.NodeType == ExpressionType.ConvertChecked => Expression.ConvertChecked(Copy(ue.Operand, replacements, labels), ue.Type),
            UnaryExpression ue => UnaryExpressions[ue.NodeType](Copy(ue.Operand, replacements, labels)),

            BinaryExpression be => BinaryExpressions[expression.NodeType](Copy(be.Left, replacements, labels), Copy(be.Right, replacements, labels)),

            BlockExpression be => Copy(be, replacements, labels),
            LoopExpression le => Copy(le, replacements, labels),
            ConditionalExpression ce when ce.IfFalse is null => Expression.IfThen(Copy(ce.Test, replacements, labels), Copy(ce.IfTrue, replacements, labels)),
            ConditionalExpression ce => Expression.IfThenElse(Copy(ce.Test, replacements, labels), Copy(ce.IfTrue, replacements, labels), Copy(ce.IfFalse, replacements, labels)),

            MemberExpression me when me.Member is PropertyInfo m => Expression.Property(Copy(me.Expression, replacements, labels), m),
            MemberExpression me when me.Member is FieldInfo m => Expression.Field(Copy(me.Expression, replacements, labels), m),
            IndexExpression ie => Expression.Property(Copy(ie.Object, replacements, labels), ie.Indexer, ie.Arguments.Select(a => Copy(a, replacements, labels))),
            MethodCallExpression mce => Expression.Call(mce.Method, mce.Arguments.Select(a => Copy(a, replacements, labels))),

            LabelExpression le => Copy(le, replacements, labels),

            DebugInfoExpression die => Expression.DebugInfo(die.Document, die.StartLine, die.StartColumn, die.EndLine, die.EndColumn),

            null => null,
            _ => throw new NotSupportedException($"{expression.GetType()} is not supported")
        };

    }

    private static Expression Copy(
        BlockExpression expression,
        IReadOnlyDictionary<ParameterExpression, Expression> replacements,
        IDictionary<LabelTarget, LabelTarget> labels
    )
    {
        var newReplacements = replacements.ToDictionary(kv => kv.Key, kv => kv.Value);
        var newVariables = new List<ParameterExpression>();
        foreach (var variable in expression.Variables.Select(v => new KeyValuePair<ParameterExpression, ParameterExpression>(v, Expression.Variable(v.Type, v.Name))))
        {
            newVariables.Add(variable.Value);
            newReplacements.Add(variable.Key, variable.Value);
        }
        var body = expression.Expressions.Select(e => Copy(e, newReplacements, labels)).ToList();
        return Expression.Block(expression.Type, newVariables, body);
    }

    private static Expression Copy(
        LoopExpression expression,
        IReadOnlyDictionary<ParameterExpression, Expression> replacements,
        IDictionary<LabelTarget, LabelTarget> labels
    )
    {
        return Expression.Loop(
            Copy(expression.Body, replacements, labels),
            Copy(expression.BreakLabel, replacements, labels),
            Copy(expression.ContinueLabel, replacements, labels)
        );
    }

    private static Expression Copy(
        LabelExpression expression,
        IReadOnlyDictionary<ParameterExpression, Expression> replacements,
        IDictionary<LabelTarget, LabelTarget> labels
    )
    {
        LabelTarget target = expression.Target;
        LabelTarget newTarget = Copy(target, replacements, labels);
        return Expression.Label(newTarget);
    }

    private static LabelTarget Copy(
        LabelTarget target,
        IReadOnlyDictionary<ParameterExpression, Expression> replacements,
        IDictionary<LabelTarget, LabelTarget> labels
    )
    {
        if (target == null) return null;
        if (!labels.TryGetValue(target, out var newTarget))
        {
            newTarget = target.Type == null
                ? Expression.Label(target.Name)
                : Expression.Label(target.Type, target.Name);
            labels[target] = newTarget;
        }
        return newTarget;
    }

    private static IReadOnlyDictionary<ExpressionType, Func<Expression, Expression>> UnaryExpressions = new Dictionary<ExpressionType, Func<Expression, Expression>>()
    {
        { ExpressionType.Negate, Expression.Negate },
        { ExpressionType.Not, Expression.Not },
        { ExpressionType.OnesComplement, Expression.OnesComplement },

        { ExpressionType.ArrayLength, Expression.ArrayLength },

        { ExpressionType.Increment, Expression.Increment },
        { ExpressionType.UnaryPlus, Expression.UnaryPlus },
        { ExpressionType.Decrement, Expression.Decrement },

        { ExpressionType.PostIncrementAssign, Expression.PostIncrementAssign},
        { ExpressionType.PostDecrementAssign, Expression.PostDecrementAssign},
        { ExpressionType.PreIncrementAssign, Expression.PreIncrementAssign},
        { ExpressionType.PreDecrementAssign, Expression.PreDecrementAssign},

        { ExpressionType.Quote, Expression.Quote},
    }.ToImmutableDictionary();

    private static IReadOnlyDictionary<ExpressionType, Func<Expression, Expression, Expression>> BinaryExpressions = new Dictionary<ExpressionType, Func<Expression, Expression, Expression>>()
    {
        { ExpressionType.Assign, Expression.Assign },
        { ExpressionType.ArrayIndex, Expression.ArrayIndex },
        { ExpressionType.Coalesce, Expression.Coalesce },

        { ExpressionType.Equal, Expression.Equal },
        { ExpressionType.NotEqual, Expression.NotEqual },
        { ExpressionType.GreaterThan, Expression.GreaterThan },
        { ExpressionType.GreaterThanOrEqual, Expression.GreaterThanOrEqual },
        { ExpressionType.LessThan, Expression.LessThan },
        { ExpressionType.LessThanOrEqual, Expression.LessThanOrEqual },

        { ExpressionType.Add, Expression.Add },
        { ExpressionType.AddChecked, Expression.AddChecked },
        { ExpressionType.AddAssign, Expression.AddAssign },
        { ExpressionType.AddAssignChecked, Expression.AddAssignChecked },

        { ExpressionType.Subtract, Expression.Subtract },
        { ExpressionType.SubtractChecked, Expression.SubtractChecked },
        { ExpressionType.SubtractAssign, Expression.SubtractAssign },
        { ExpressionType.SubtractAssignChecked, Expression.SubtractAssignChecked },

        { ExpressionType.Multiply, Expression.Multiply },
        { ExpressionType.MultiplyChecked, Expression.MultiplyChecked },
        { ExpressionType.MultiplyAssign, Expression.MultiplyAssign },
        { ExpressionType.MultiplyAssignChecked, Expression.MultiplyAssignChecked },

        { ExpressionType.Divide, Expression.Divide },
        { ExpressionType.DivideAssign, Expression.DivideAssign },

        { ExpressionType.Power, Expression.Power },
        { ExpressionType.PowerAssign, Expression.PowerAssign },

        { ExpressionType.Or, Expression.Or },
        { ExpressionType.OrAssign, Expression.OrAssign },
        { ExpressionType.OrElse, Expression.OrElse },

        { ExpressionType.ExclusiveOr, Expression.ExclusiveOr },
        { ExpressionType.ExclusiveOrAssign, Expression.ExclusiveOrAssign },

        { ExpressionType.And, Expression.And },
        { ExpressionType.AndAssign, Expression.AndAssign },
        { ExpressionType.AndAlso, Expression.AndAlso },

        { ExpressionType.LeftShift, Expression.LeftShift },
        { ExpressionType.LeftShiftAssign, Expression.LeftShiftAssign },

        { ExpressionType.RightShift, Expression.RightShift },
        { ExpressionType.RightShiftAssign, Expression.RightShiftAssign },
    }.ToImmutableDictionary();

}
