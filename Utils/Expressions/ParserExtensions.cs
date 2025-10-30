using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Utils.Mathematics;
using Utils.Objects;
using Utils.Reflection;

namespace Utils.Expressions;

/// <summary>
/// Provides extension methods used internally by the parser to handle type resolution,
/// number type adjustments, generic method inference, and other parsing utilities.
/// </summary>
internal static class ParserExtensions
{
    /// <summary>
    /// Gets the current application domain using reflection.
    /// </summary>
    private static AppDomain CurrentDomain { get; } =
        (AppDomain)typeof(string).GetTypeInfo().Assembly
            .GetType("System.AppDomain")
            .GetRuntimeProperty("CurrentDomain")
            .GetMethod
            .Invoke(null, []);

    /// <summary>
    /// Determines whether the given symbol is a valid name, i.e., whether it starts
    /// with a letter, underscore, or the dollar sign.
    /// </summary>
    /// <param name="symbol">The symbol string to check.</param>
    /// <returns>
    /// <see langword="true"/> if the symbol starts with a valid name character;
    /// otherwise <see langword="false"/>.
    /// </returns>
    internal static bool IsName(this string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return false;
        return char.IsLetter(symbol[0]) || symbol[0] == '_' || symbol[0] == '$';
    }

    /// <summary>
    /// If <paramref name="setNullable"/> is <see langword="true"/>, converts the provided
    /// <paramref name="type"/> to a nullable version (e.g., int -&gt; int?), otherwise returns
    /// the original <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The original <see cref="Type"/>.</param>
    /// <param name="setNullable">
    /// Indicates whether to convert the type into a nullable variant.
    /// </param>
    /// <returns>A nullable <see cref="Type"/> if requested, or the original type.</returns>
    public static Type ToNullableIf(this Type type, bool setNullable)
    {
        if (type == null) return type;
        if (!setNullable) return type;
        return typeof(Nullable<>).MakeGenericType(type);
    }

    /// <summary>
    /// Attempts to locate a <see cref="Type"/> object by searching all loaded assemblies
    /// in the current application domain.
    /// </summary>
    /// <param name="typeName">The fully qualified name of the type to find.</param>
    /// <returns>
    /// The <see cref="Type"/> if found; otherwise <see langword="null"/>.
    /// </returns>
    public static Type GetTypeCore(string typeName)
    {
        var assemblies = CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in assemblies)
        {
            var type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }
        return null;
    }

    /// <summary>
    /// Compares the <see cref="Type"/> of two numeric expressions and adjusts the lower-level expression
    /// to match the higher-level type, based on <see cref="IParserOptions.NumberTypeLevel"/>.
    /// </summary>
    /// <param name="options">A <see cref="ParserOptions"/> containing numeric type priorities.</param>
    /// <param name="left">The left <see cref="Expression"/>.</param>
    /// <param name="right">The right <see cref="Expression"/>.</param>
    /// <returns>A tuple of adjusted (left, right) expressions.</returns>
    public static (Expression left, Expression right) AdjustNumberType(this IParserOptions options, Expression left, Expression right)
    {
        if (left.Type == right.Type) return (left, right);
        if (!options.NumberTypeLevel.TryGetValue(left.Type, out int leftLevel)) return (left, right);
        if (!options.NumberTypeLevel.TryGetValue(right.Type, out int rightLevel)) return (left, right);

        if (leftLevel > rightLevel)
        {
            right = options.AdjustType(right, left.Type);
        }
        else
        {
            left = options.AdjustType(left, right.Type);
        }
        return (left, right);
    }

    /// <summary>
    /// Compares the <see cref="Type"/> of a numeric expression <paramref name="right"/> with the specified <paramref name="type"/>,
    /// and adjusts the expression if it is of a lower numeric level.
    /// </summary>
    /// <param name="options">A <see cref="ParserOptions"/> containing numeric type priorities.</param>
    /// <param name="type">The type to which the expression might need to adjust.</param>
    /// <param name="right">The <see cref="Expression"/> to potentially adjust.</param>
    /// <returns>An adjusted expression if the types differ and a higher-level type is required.</returns>
    public static Expression AdjustNumberType(this IParserOptions options, Type type, Expression right)
    {
        if (type == right.Type) return right;
        if (!options.NumberTypeLevel.TryGetValue(type, out int leftLevel)) return right;
        if (!options.NumberTypeLevel.TryGetValue(right.Type, out int rightLevel)) return right;

        if (leftLevel > rightLevel)
        {
            right = options.AdjustType(right, type);
        }
        return right;
    }

    /// <summary>
    /// Converts an <see cref="Expression"/> to the specified <paramref name="type"/> if needed,
    /// respecting numeric type conversions in <see cref="ParserOptions"/>.
    /// </summary>
    /// <param name="options">A <see cref="ParserOptions"/> storing numeric level information.</param>
    /// <param name="expression">The expression to convert.</param>
    /// <param name="type">The target type for conversion.</param>
    /// <returns>An <see cref="Expression"/> converted to the specified <paramref name="type"/>.</returns>
    public static Expression AdjustType(this IParserOptions options, Expression expression, Type type)
    {
        if (expression is ConstantExpression ce
            && options.NumberTypeLevel.TryGetValue(ce.Type, out var constLevel)
            && options.NumberTypeLevel.TryGetValue(type, out var typeLevel)
            && constLevel < typeLevel)
        {
            return Expression.Constant(Convert.ChangeType(ce.Value, type), type);
        }
        return Expression.Convert(expression, type);
    }

    /// <summary>
    /// Reads a bracketed substring from the tokenizer until the matching end bracket is encountered,
    /// optionally assuming the start bracket has already been read.
    /// </summary>
    /// <param name="context">The current <see cref="ParserContext"/> managing tokens and state.</param>
    /// <param name="markers">A <see cref="Parenthesis"/> structure specifying start/end symbols.</param>
    /// <param name="hasReadPre">
    /// <see langword="true"/> if the start symbol has already been read, otherwise <see langword="false"/>.
    /// </param>
    /// <returns>The substring inside the bracketed region, or <see langword="null"/> if unmatched.</returns>
    public static string GetBracketString(ParserContext context, Parenthesis markers, bool hasReadPre)
    {
        // Read the opening parenthesis if it hasn't been read
        if (!hasReadPre) context.Tokenizer.ReadSymbol(markers.Start);
        int startPosition = context.Tokenizer.Position.Index + markers.Start.Length;

        int depth = 1;
        string str;
        while (depth > 0)
        {
            str = context.Tokenizer.ReadToken(true);
            // If an opening parenthesis is encountered, it indicates nested brackets
            if (str == markers.Start) depth++;
            if (str == markers.End) depth--;
        }
        int endPosition = context.Tokenizer.Position.Index - markers.End.Length + 1;
        return context.Tokenizer.Content[startPosition..endPosition];
    }

    /// <summary>
    /// Retrieves the operator priority level from <see cref="ParserOptions.OperatorPriorityLevel"/>,
    /// optionally modifying the operator symbol for unary usage (e.g. "++before" or "+before").
    /// </summary>
    /// <param name="options">The <see cref="ParserOptions"/> containing operator priority info.</param>
    /// <param name="operatorSymbol">The operator symbol being looked up.</param>
    /// <param name="isBefore">
    /// <see langword="true"/> for prefix usage (e.g., ++i), otherwise <see langword="false"/>.
    /// </param>
    /// <returns>An integer representing the operator's priority, or 100 if not found.</returns>
    public static int GetOperatorLevel(this IParserOptions options, string operatorSymbol, bool isBefore)
    {
        operatorSymbol += operatorSymbol switch
        {
            "++" or "--" => isBefore ? "before" : "behind",
            "+" or "-" => isBefore ? "before" : "",
            _ => ""
        };
        if (!options.OperatorPriorityLevel.TryGetValue(operatorSymbol, out var result)) result = 100;
        return result;
    }

    /// <summary>
    /// Compares method parameters with the given expression <paramref name="obj"/>
    /// and a list of types <paramref name="toAssign"/>, returning a numeric distance that indicates
    /// how close the match is (-1 if unassignable).
    /// </summary>
    /// <param name="methodInfo">The method to compare against.</param>
    /// <param name="obj">
    /// The target object for instance methods; <see langword="null"/> for static methods.
    /// </param>
    /// <param name="toAssign">The collection of argument types to match.</param>
    /// <returns>
    /// An integer representing the total match distance, or -1 if the parameters are not assignable.
    /// </returns>
    public static int CompareParametersAndTypes(this MethodInfo methodInfo, Expression obj, IEnumerable<Type> toAssign)
    {
        ParameterInfo[] toBeAssigned = methodInfo.GetParameters();
        return CompareParametersAndTypes(obj, toAssign, toBeAssigned);
    }

    /// <summary>
    /// Compares constructor parameters with a list of types <paramref name="toAssign"/>,
    /// returning a numeric distance that indicates how close the match is (-1 if unassignable).
    /// </summary>
    /// <param name="constructorInfo">The constructor to compare against.</param>
    /// <param name="toAssign">The collection of argument types to match.</param>
    /// <returns>
    /// An integer representing the total match distance, or -1 if the parameters are not assignable.
    /// </returns>
    public static int CompareParametersAndTypes(this ConstructorInfo constructorInfo, IEnumerable<Type> toAssign)
    {
        ParameterInfo[] toBeAssigned = constructorInfo.GetParameters();
        return CompareParametersAndTypes(null, toAssign, toBeAssigned);
    }

    /// <summary>
    /// Internal helper that compares a set of <paramref name="toBeAssigned"/> parameters against
    /// an enumerable of types <paramref name="toAssign"/>. Optionally includes an <paramref name="obj"/>
    /// if the method is an extension method.
    /// </summary>
    /// <param name="obj">An <see cref="Expression"/> representing the instance for extension methods (can be null).</param>
    /// <param name="toAssign">An enumerable of <see cref="Type"/> objects representing argument types.</param>
    /// <param name="toBeAssigned">An array of <see cref="ParameterInfo"/> to compare against.</param>
    /// <returns>A total distance measure, or -1 if unassignable.</returns>
    public static int CompareParametersAndTypes(Expression obj, IEnumerable<Type> toAssign, ParameterInfo[] toBeAssigned)
    {
        int totalDistance = 0;

        var eToBeAssigned = ((IEnumerable<ParameterInfo>)toBeAssigned).GetEnumerator();
        var eToAssign = toBeAssigned.Any() && toBeAssigned.IsExtension()
            ? toAssign.Prepend(obj.Type).GetEnumerator()
            : toAssign.GetEnumerator();

        ParameterInfo toBeAssignLast;
        bool bToBeAssigned, bToAssign;
        while ((bToBeAssigned = eToBeAssigned.MoveNext()) & (bToAssign = eToAssign.MoveNext()))
        {
            toBeAssignLast = eToBeAssigned.Current;
            if (toBeAssignLast.HasParams())
            {
                // Param array scenario
                var paramType = toBeAssignLast.ParameterType.GetElementType();
                // Check if an array has been passed directly
                if (eToAssign.Current.IsArray && eToAssign.Current.GetElementType().ToString() == paramType.ToString())
                {
                    var arrayDistance = toBeAssignLast.ParameterType.GetTypeDistance(eToAssign.Current);
                    if (arrayDistance != -1)
                    {
                        totalDistance += arrayDistance;
                        return totalDistance;
                    }
                }

                // Otherwise, check elements one by one, track the longest distance
                var distance = -1;
                do
                {
                    var elementDistance = paramType.GetTypeDistance(eToAssign.Current);
                    if (elementDistance == -1) return -1;
                    distance = MathEx.Max(distance, elementDistance);
                } while (eToAssign.MoveNext());
                totalDistance += distance;
                return totalDistance;
            }
            else
            {
                var distance = toBeAssignLast.ParameterType.GetTypeDistance(eToAssign.Current);
                if (distance == -1) return -1;
                totalDistance += distance;
            }
        }

        if (bToBeAssigned == bToAssign) return totalDistance;
        return -1;
    }

    /// <summary>
    /// Computes a type distance between <paramref name="toBeAssigned"/> and <paramref name="toAssign"/>,
    /// returning -1 if <paramref name="toAssign"/> is not assignable to <paramref name="toBeAssigned"/>.
    /// </summary>
    /// <param name="toBeAssigned">The target <see cref="Type"/> to match.</param>
    /// <param name="toAssign">The source <see cref="Type"/>.</param>
    /// <returns>
    /// A non-negative integer representing how many steps it takes for <paramref name="toAssign"/>
    /// to reach <paramref name="toBeAssigned"/> via inheritance or interface, or 1 if an operator-based
    /// conversion is implied, or -1 if not assignable.
    /// </returns>
    public static int GetTypeDistance(this Type toBeAssigned, Type toAssign)
    {
        if (!toBeAssigned.IsAssignableFromEx(toAssign)) return -1;
        int distance = 0;

        if (toBeAssigned.IsGenericEnumerable(out var toBeAssignedElement) && toAssign.IsGenericEnumerable(out var toAssignElement))
        {
            distance += GetTypeDistance(toBeAssignedElement, toAssignElement);
        }

        for (Type type = toAssign; type != typeof(object); type = type.BaseType, distance++)
        {
            if (type == toBeAssigned) return distance;
            foreach (var @interface in type.GetInterfaces().Except(type.BaseType.GetInterfaces()))
            {
                if (toBeAssigned == @interface) return distance + 1;
            }
        }
        if (toBeAssigned == typeof(object)) return distance;

        // If types can be assigned but not by derivation, assume an operator-based conversion => distance of 1
        return 1;
    }

    private static readonly Type IEnumerableType = typeof(IEnumerable);
    private static readonly Type IGenericEnumerableType = typeof(IEnumerable<>);

    /// <summary>
    /// Determines whether the specified <paramref name="type"/> implements
    /// the non-generic <see cref="IEnumerable"/> interface, plus tries to parse the generic element type if any.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to inspect.</param>
    /// <returns><see langword="true"/> if the type is assignable to <c>IEnumerable</c>; otherwise <see langword="false"/>.</returns>
    public static bool IsGenericEnumerable(this Type type) => type.IsGenericEnumerable(out _);

    /// <summary>
    /// Determines whether the specified <paramref name="type"/> is a generic <see cref="IEnumerable{T}"/>,
    /// and if so, outputs the <c>T</c> (element type).
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to inspect.</param>
    /// <param name="elementType">
    /// When this method returns, contains the generic element type if the type is <c>IEnumerable{T}</c>;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if the type is a generic enumerable; otherwise <see langword="false"/>.</returns>
    public static bool IsGenericEnumerable(this Type type, out Type elementType)
    {
        elementType = null;
        if (!IEnumerableType.IsAssignableFrom(type)) return false;
        var result = type.IsOfGenericType(IGenericEnumerableType, out var elementsTypes);
        if (result)
        {
            elementType = elementsTypes[0];
        }
        return result;
    }

    /// <summary>
    /// Determines whether the specified <paramref name="type"/> matches the given generic type definition,
    /// without providing the resulting type arguments.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to inspect.</param>
    /// <param name="IGenericEnumerableType">The generic type definition to match.</param>
    /// <returns><see langword="true"/> if <paramref name="type"/> matches; otherwise <see langword="false"/>.</returns>
    public static bool IsOfGenericType(this Type type, Type IGenericEnumerableType)
        => type.IsOfGenericType(IGenericEnumerableType, out _);

    /// <summary>
    /// Determines whether the specified <paramref name="type"/> matches the given generic type definition,
    /// and if so, retrieves the type arguments.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to inspect.</param>
    /// <param name="IGenericEnumerableType">The generic type definition to match.</param>
    /// <param name="elementType">
    /// When this method returns, contains the array of type arguments if matched; otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="type"/> matches; otherwise <see langword="false"/>.</returns>
    public static bool IsOfGenericType(this Type type, Type IGenericEnumerableType, out Type[] elementType)
    {
        elementType = null;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == IGenericEnumerableType)
        {
            elementType = type.GetGenericArguments();
            return true;
        }
        var enumerableInterface = type.GetInterfaces()
            .Where(t => t.IsGenericType)
            .FirstOrDefault(t => t.GetGenericTypeDefinition() == IGenericEnumerableType);

        if (enumerableInterface == null) return false;

        elementType = enumerableInterface.GetGenericArguments();
        return true;
    }

    /// <summary>
    /// Recursively determines the generic argument mapping between <paramref name="parameterType"/> and
    /// <paramref name="toAssign"/>, storing the correspondence in <paramref name="correspondances"/>.
    /// </summary>
    /// <param name="parameterType">The type in which type parameters may appear.</param>
    /// <param name="toAssign">The actual <see cref="Type"/> that is assigned.</param>
    /// <param name="baseDistance">The current distance offset in the type hierarchy.</param>
    /// <param name="correspondances">A list tracking (GenericType, Correspondance, Distance) triplets.</param>
    public static void GetGenericArgumentComparableType(
        Type parameterType,
        Type toAssign,
        int baseDistance,
        List<(Type GenericType, Type Correspondance, int Distance)> correspondances)
    {
        if (parameterType == null) return;
        if (parameterType.IsGenericParameter)
        {
            correspondances.Add((parameterType, toAssign, baseDistance));
            return;
        }
        if (parameterType.IsArray && toAssign.IsArray)
        {
            GetGenericArgumentComparableType(parameterType.GetElementType(), toAssign.GetElementType(), baseDistance + 1, correspondances);
            return;
        }

        var comparisonType = toAssign.FindGenericBaseType(parameterType);
        if (comparisonType == null) return;

        var parametersGenericTypes = parameterType.GetGenericArguments();
        var toAssignGenericTypes = comparisonType.GetGenericArguments();
        if (parametersGenericTypes.Length != toAssignGenericTypes.Length) return;

        for (var i = 0; i < parametersGenericTypes.Length; i++)
        {
            GetGenericArgumentComparableType(parametersGenericTypes[i], toAssignGenericTypes[i], baseDistance + 1, correspondances);
        }
    }

    /// <summary>
    /// Finds the generic base type of <paramref name="type"/> matching <paramref name="baseType"/>,
    /// or <see langword="null"/> if not found.
    /// </summary>
    /// <param name="type">The candidate <see cref="Type"/>.</param>
    /// <param name="baseType">A generic type definition or interface to match.</param>
    /// <returns>The matching <see cref="Type"/>, or <see langword="null"/> if no match is found.</returns>
    public static Type FindGenericBaseType(this Type type, Type baseType)
    {
        if (type.ToString() == baseType.ToString()) return type;
        if (type == typeof(object)) return null;
        if (baseType.IsGenericTypeDefinition) return type.FindGenericBaseType(baseType.GetGenericTypeDefinition());

        if (type.IsGenericType && type.GetGenericTypeDefinition().ToString() == baseType.ToString()) return type;
        if (baseType.IsInterface)
        {
            return type
                .GetInterfaces()
                .FirstOrDefault(i => (i.IsGenericType && i.GetGenericTypeDefinition().ToString() == baseType.ToString())
                                     || i.ToString() == baseType.ToString());
        }

        return type.BaseType.FindGenericBaseType(baseType);
    }

    /// <summary>
    /// Infers the generic arguments for a generic <see cref="MethodInfo"/> based on the
    /// <paramref name="objectType"/> (extension receiver) and <paramref name="parameterTypes"/>.
    /// Returns <see langword="null"/> if inference fails.
    /// </summary>
    /// <param name="method">A possibly generic method.</param>
    /// <param name="objectType">The receiver type if the method is an extension method; otherwise <see langword="null"/>.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> representing argument types.</param>
    /// <returns>A specialized <see cref="MethodInfo"/> if successful; otherwise <see langword="null"/>.</returns>
    public static MethodInfo InferGenericMethod(this MethodInfo method, Type objectType, Type[] parameterTypes)
    {
        if (!method.IsGenericMethodDefinition) return method;

        var methodParameters = method.GetParameters();
        var eParameters = ((IEnumerable<ParameterInfo>)method.GetParameters()).GetEnumerator();
        var eTypes = methodParameters.Length > 0 && methodParameters[0].GetCustomAttribute<ExtensionAttribute>() != null
            ? parameterTypes.Prepend(objectType).GetEnumerator()
            : ((IEnumerable<Type>)parameterTypes).GetEnumerator();

        var correspondances = new List<(Type GenericType, Type Correspondance, int Distance)>();

        ParameterInfo toBeAssignedLast;
        bool bToBeAssigned, bToAssign;

        while ((bToBeAssigned = eParameters.MoveNext()) & (bToAssign = eTypes.MoveNext()))
        {
            toBeAssignedLast = eParameters.Current;
            if (toBeAssignedLast.ParameterType.IsArray && toBeAssignedLast.GetCustomAttribute<ParamArrayAttribute>() != null)
            {
                // param array scenario
                ParserExtensions.GetGenericArgumentComparableType(toBeAssignedLast.ParameterType, eTypes.Current, 1, correspondances);
                var paramType = toBeAssignedLast.ParameterType.GetElementType();

                do
                {
                    ParserExtensions.GetGenericArgumentComparableType(toBeAssignedLast.ParameterType, eTypes.Current, 0, correspondances);
                } while (eTypes.MoveNext());
            }
            else
            {
                ParserExtensions.GetGenericArgumentComparableType(toBeAssignedLast.ParameterType, eTypes.Current, 0, correspondances);
            }
        }

        // Construct the generic argument array from the correspondences
        var candidateArguments = correspondances
            .GroupBy(c => c.GenericType, c => c)
            .ToLookup(
                g => g.Key,
                g => g
                    .OrderByDescending(c => c.Distance)
                    .Select(c => c.Correspondance)
                    .FirstOrDefault()
            );
        var genericArguments = method.GetGenericArguments().Select(a => candidateArguments[a].FirstOrDefault()).ToArray();
        if (genericArguments.Any(ga => ga == null)) return null;

        return method.MakeGenericMethod(genericArguments);
    }

    /// <summary>
    /// Adjusts <paramref name="listParams"/> (expressions) to match the parameters in a <see cref="ConstructorInfo"/>,
    /// handling param arrays if needed.
    /// </summary>
    /// <param name="constructorInfo">A <see cref="ConstructorInfo"/> describing the target constructor.</param>
    /// <param name="listParams">An array of <see cref="Expression"/> representing arguments.</param>
    /// <returns>A new array of <see cref="Expression"/> that matches the constructor's parameter signature.</returns>
    public static Expression[] AdjustParameters(this ConstructorInfo constructorInfo, Expression[] listParams)
    {
        var methodParams = constructorInfo.GetParameters();
        return AdjustParameters(null, listParams, methodParams);
    }

    /// <summary>
    /// Adjusts <paramref name="listParams"/> (expressions) to match the parameters in a <see cref="MethodInfo"/>,
    /// handling param arrays and extension methods if needed.
    /// </summary>
    /// <param name="methodInfo">A <see cref="MethodInfo"/> describing the target method.</param>
    /// <param name="obj">
    /// An <see cref="Expression"/> representing the receiver (if method is extension), otherwise <see langword="null"/>.
    /// </param>
    /// <param name="listParams">An array of <see cref="Expression"/> representing arguments.</param>
    /// <returns>A new array of <see cref="Expression"/> that matches the method's parameter signature.</returns>
    public static Expression[] AdjustParameters(this MethodInfo methodInfo, Expression obj, Expression[] listParams)
    {
        var methodParams = methodInfo.GetParameters();
        return AdjustParameters(obj, listParams, methodParams);
    }

    /// <summary>
    /// Internal helper that adjusts a set of <paramref name="listParams"/> to match
    /// <paramref name="methodParams"/>, handling extension parameters, param arrays, etc.
    /// </summary>
    private static Expression[] AdjustParameters(Expression obj, Expression[] listParams, ParameterInfo[] methodParams)
    {
        Expression[] result;
        int lastResultIndex;

        // If extension method, we shift the arguments by one to include 'obj'
        if (methodParams.IsExtension())
        {
            result = new Expression[methodParams.Length + 1];
            result[0] = obj;
            listParams[0..result.Length].CopyTo(result, 1);
        }
        else
        {
            result = new Expression[methodParams.Length];
            listParams[0..result.Length].CopyTo(result, 0);
        }

        // Handle param array scenario
        if (methodParams.HasParams())
        {
            lastResultIndex = result.Length - 1;
            var lastParameter = methodParams[lastResultIndex];
            var lastParameters = listParams[lastResultIndex..];
            var elementType = lastParameter.ParameterType.GetElementType();

            if (lastParameters.Length != 1 || lastParameter.ParameterType == elementType)
            {
                // Create an array of the param elements
                result[lastResultIndex] = Expression.NewArrayInit(elementType, listParams[lastResultIndex..]);
            }
        }

        // Convert expression types where needed
        for (int i = 0; i < result.Length; i++)
        {
            if (methodParams[i].ParameterType == result[i].Type) continue;
            if (methodParams[i].ParameterType.IsClass != result[i].Type.IsClass)
            {
                result[i] = Expression.Convert(result[i], methodParams[i].ParameterType);
            }
            if (methodParams[i].ParameterType.In(Types.Number))
            {
                if (result[i] is ConstantExpression ce)
                {
                    result[i] = Expression.Constant(Convert.ChangeType(ce.Value, methodParams[i].ParameterType));
                }
                else
                {
                    result[i] = Expression.Convert(result[i], methodParams[i].ParameterType);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Determines whether the specified <see cref="MethodInfo"/> is an extension method.
    /// </summary>
    public static bool IsExtension(this MethodInfo method) => method.GetParameters().IsExtension();

    /// <summary>
    /// Determines whether the first parameter of the specified collection of <see cref="ParameterInfo"/>
    /// is marked as an extension parameter.
    /// </summary>
    public static bool IsExtension(this IEnumerable<ParameterInfo> parameters)
        => parameters.FirstOrDefault()?.IsExtension() ?? false;

    /// <summary>
    /// Determines whether the given <see cref="ParameterInfo"/> is marked as an extension parameter.
    /// </summary>
    public static bool IsExtension(this ParameterInfo parameter)
        => parameter.GetCustomAttribute<ExtensionAttribute>() != null;

    /// <summary>
    /// Determines whether the specified <see cref="MethodInfo"/> has a <c>params</c> array parameter.
    /// </summary>
    public static bool HasParams(this MethodInfo method) => method.GetParameters().HasParams();

    /// <summary>
    /// Determines whether the last parameter in the given collection of <see cref="ParameterInfo"/>
    /// is a <c>params</c> array parameter.
    /// </summary>
    public static bool HasParams(this IEnumerable<ParameterInfo> parameters)
        => parameters.LastOrDefault()?.HasParams() ?? false;

    /// <summary>
    /// Determines whether the given <see cref="ParameterInfo"/> is marked with <see cref="ParamArrayAttribute"/>
    /// (i.e., if it is a <c>params</c> parameter).
    /// </summary>
    public static bool HasParams(this ParameterInfo parameter)
        => parameter.GetCustomAttribute<ParamArrayAttribute>() != null;
}
