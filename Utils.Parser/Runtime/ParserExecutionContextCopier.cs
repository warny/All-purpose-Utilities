using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Parser.Runtime;

/// <summary>
/// Copies parser execution-context instances with a cached, compiled field-copy delegate for each context type.
/// </summary>
/// <typeparam name="TContext">The parser execution-context type to copy.</typeparam>
/// <remarks>
/// The copier performs a shallow structural copy intended for parser execution contexts. It recreates known collection containers
/// such as arrays, <see cref="List{T}"/>, <see cref="Dictionary{TKey,TValue}"/>, and <see cref="HashSet{T}"/> with explicit copy expressions,
/// and can recreate unknown <see cref="IEnumerable{T}"/> collections when they expose a safe copy constructor, parameterless constructor
/// with <c>AddRange(IEnumerable&lt;T&gt;)</c>, or parameterless constructor with <c>Add(T)</c>. It does not deep-clone contained elements.
/// Unknown references and collections without a safe reconstruction strategy are copied by reference. Static fields and field-like event
/// backing fields are ignored. Readonly instance fields are rejected because they cannot be assigned safely during normal context copies.
/// Reflection is used only while building the delegate cached by the closed generic type; subsequent copies invoke the compiled delegate.
/// </remarks>
public static class ParserExecutionContextCopier<TContext>
    where TContext : class
{
    /// <summary>
    /// Stores the lazily built copy delegate for this closed context type.
    /// </summary>
    private static readonly Lazy<Action<TContext, TContext>> CopyToCore = new(BuildCopyToCore, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Creates a new context instance through <paramref name="factory"/> and copies <paramref name="source"/> into it.
    /// </summary>
    /// <param name="source">The context instance whose state is copied.</param>
    /// <param name="factory">The caller-provided factory used to create the target context instance.</param>
    /// <returns>A new context instance containing the copied state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The context type contains an unsupported readonly instance field.</exception>
    public static TContext Copy(TContext source, Func<TContext> factory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(factory);

        TContext target = factory();

        ArgumentNullException.ThrowIfNull(target, nameof(factory));

        CopyTo(source, target);
        return target;
    }

    /// <summary>
    /// Copies the state of <paramref name="source"/> into an existing <paramref name="target"/> context instance.
    /// </summary>
    /// <param name="source">The context instance whose state is copied.</param>
    /// <param name="target">The context instance that receives the copied state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The context type contains an unsupported readonly instance field.</exception>
    public static void CopyTo(TContext source, TContext target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        CopyToCore.Value(source, target);
    }

    /// <summary>
    /// Builds the compiled field-copy delegate for this closed context type.
    /// </summary>
    /// <returns>The compiled delegate that copies state from source to target.</returns>
    private static Action<TContext, TContext> BuildCopyToCore()
    {
        ParameterExpression source = Expression.Parameter(typeof(TContext), "source");
        ParameterExpression target = Expression.Parameter(typeof(TContext), "target");
        List<Expression> assignments = [];

        foreach (FieldInfo field in GetInstanceFields(typeof(TContext)))
        {
            if (ShouldSkipField(field))
            {
                continue;
            }

            if (field.IsInitOnly)
            {
                throw CreateReadonlyFieldException(field);
            }

            assignments.Add(BuildFieldAssignment(source, target, field));
        }

        return Expression.Lambda<Action<TContext, TContext>>(Expression.Block(assignments), source, target).Compile();
    }

    /// <summary>
    /// Enumerates all instance fields declared by the supplied type and its base types.
    /// </summary>
    /// <param name="type">The type whose field hierarchy is inspected.</param>
    /// <returns>The instance fields that may participate in copying.</returns>
    private static IEnumerable<FieldInfo> GetInstanceFields(Type type)
    {
        for (Type? currentType = type; currentType is not null && currentType != typeof(object); currentType = currentType.BaseType)
        {
            foreach (FieldInfo field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    /// <summary>
    /// Determines whether a field should be excluded from context copying.
    /// </summary>
    /// <param name="field">The field being inspected.</param>
    /// <returns><see langword="true"/> when the field should not be copied; otherwise, <see langword="false"/>.</returns>
    private static bool ShouldSkipField(FieldInfo field)
    {
        return field.IsStatic || IsEventBackingField(field);
    }

    /// <summary>
    /// Determines whether a field is the backing field for a field-like event.
    /// </summary>
    /// <param name="field">The field being inspected.</param>
    /// <returns><see langword="true"/> when the field backs an event; otherwise, <see langword="false"/>.</returns>
    private static bool IsEventBackingField(FieldInfo field)
    {
        Type? declaringType = field.DeclaringType;

        return declaringType?.GetEvent(field.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is not null;
    }

    /// <summary>
    /// Creates the diagnostic exception used when a readonly field is discovered.
    /// </summary>
    /// <param name="field">The readonly field that cannot be copied.</param>
    /// <returns>The exception describing the unsupported field.</returns>
    private static InvalidOperationException CreateReadonlyFieldException(FieldInfo field)
    {
        string typeName = field.DeclaringType?.FullName ?? typeof(TContext).FullName ?? typeof(TContext).Name;

        return new InvalidOperationException(
            $"Readonly instance field '{field.Name}' on '{typeName}' cannot be copied by ParserExecutionContextCopier<T>. " +
            "Use mutable state or provide explicit copy support in a later custom context strategy.");
    }

    /// <summary>
    /// Builds the assignment expression that copies a single field.
    /// </summary>
    /// <param name="source">The expression representing the source context.</param>
    /// <param name="target">The expression representing the target context.</param>
    /// <param name="field">The field to copy.</param>
    /// <returns>An expression assigning the copied field value to the target.</returns>
    private static Expression BuildFieldAssignment(ParameterExpression source, ParameterExpression target, FieldInfo field)
    {
        Expression sourceField = BuildFieldAccess(source, field);
        Expression targetField = BuildFieldAccess(target, field);
        Expression copiedValue = BuildCopiedValueExpression(sourceField, field.FieldType);

        return Expression.Assign(targetField, copiedValue);
    }

    /// <summary>
    /// Builds a field access expression, converting the context parameter to the declaring type when inherited fields are copied.
    /// </summary>
    /// <param name="context">The context parameter expression.</param>
    /// <param name="field">The field to access.</param>
    /// <returns>An expression that accesses the requested field.</returns>
    private static Expression BuildFieldAccess(ParameterExpression context, FieldInfo field)
    {
        Type declaringType = field.DeclaringType ?? typeof(TContext);
        Expression instance = declaringType == typeof(TContext) ? context : Expression.Convert(context, declaringType);

        return Expression.Field(instance, field);
    }

    /// <summary>
    /// Builds an expression that copies a source field value according to the supported shallow structural copy rules.
    /// </summary>
    /// <param name="sourceField">The expression reading the source field value.</param>
    /// <param name="fieldType">The field type being copied.</param>
    /// <returns>An expression that produces the value to assign to the target field.</returns>
    private static Expression BuildCopiedValueExpression(Expression sourceField, Type fieldType)
    {
        if (fieldType.IsArray)
        {
            return BuildArrayCopyExpression(sourceField, fieldType);
        }

        if (fieldType.IsGenericType)
        {
            Type genericDefinition = fieldType.GetGenericTypeDefinition();

            if (genericDefinition == typeof(List<>))
            {
                return BuildListCopyExpression(sourceField, fieldType);
            }

            if (genericDefinition == typeof(Dictionary<,>))
            {
                return BuildDictionaryCopyExpression(sourceField, fieldType);
            }

            if (genericDefinition == typeof(HashSet<>))
            {
                return BuildHashSetCopyExpression(sourceField, fieldType);
            }
        }

        return BuildUnknownCollectionCopyExpression(sourceField, fieldType);
    }

    /// <summary>
    /// Builds an expression that clones an array when it is not null.
    /// </summary>
    /// <param name="sourceField">The expression reading the source array.</param>
    /// <param name="fieldType">The array field type.</param>
    /// <returns>An expression that returns a cloned array or <see langword="null"/>.</returns>
    private static Expression BuildArrayCopyExpression(Expression sourceField, Type fieldType)
    {
        MethodInfo cloneMethod = typeof(Array).GetMethod(nameof(Array.Clone), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Array.Clone could not be found.");

        Expression clone = Expression.Convert(Expression.Call(sourceField, cloneMethod), fieldType);

        return BuildNullPreservingExpression(sourceField, clone, fieldType);
    }

    /// <summary>
    /// Builds an expression that recreates a <see cref="List{T}"/> using its enumerable copy constructor.
    /// </summary>
    /// <param name="sourceField">The expression reading the source list.</param>
    /// <param name="fieldType">The concrete list field type.</param>
    /// <returns>An expression that returns a copied list or <see langword="null"/>.</returns>
    private static Expression BuildListCopyExpression(Expression sourceField, Type fieldType)
    {
        Type elementType = fieldType.GetGenericArguments()[0];
        Type enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        ConstructorInfo constructor = fieldType.GetConstructor([enumerableType])
            ?? throw new InvalidOperationException($"The enumerable copy constructor could not be found for '{fieldType.FullName}'.");
        Expression copy = Expression.New(constructor, sourceField);

        return BuildNullPreservingExpression(sourceField, copy, fieldType);
    }

    /// <summary>
    /// Builds an expression that recreates a <see cref="Dictionary{TKey,TValue}"/> using its dictionary copy constructor.
    /// </summary>
    /// <param name="sourceField">The expression reading the source dictionary.</param>
    /// <param name="fieldType">The concrete dictionary field type.</param>
    /// <returns>An expression that returns a copied dictionary or <see langword="null"/>.</returns>
    private static Expression BuildDictionaryCopyExpression(Expression sourceField, Type fieldType)
    {
        Type[] genericArguments = fieldType.GetGenericArguments();
        Type dictionaryType = typeof(IDictionary<,>).MakeGenericType(genericArguments);
        ConstructorInfo constructor = fieldType.GetConstructor([dictionaryType])
            ?? throw new InvalidOperationException($"The dictionary copy constructor could not be found for '{fieldType.FullName}'.");
        Expression copy = Expression.New(constructor, sourceField);

        return BuildNullPreservingExpression(sourceField, copy, fieldType);
    }

    /// <summary>
    /// Builds an expression that recreates a <see cref="HashSet{T}"/> using its enumerable copy constructor.
    /// </summary>
    /// <param name="sourceField">The expression reading the source hash set.</param>
    /// <param name="fieldType">The concrete hash-set field type.</param>
    /// <returns>An expression that returns a copied hash set or <see langword="null"/>.</returns>
    private static Expression BuildHashSetCopyExpression(Expression sourceField, Type fieldType)
    {
        Type elementType = fieldType.GetGenericArguments()[0];
        Type enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        ConstructorInfo constructor = fieldType.GetConstructor([enumerableType])
            ?? throw new InvalidOperationException($"The enumerable copy constructor could not be found for '{fieldType.FullName}'.");
        Expression copy = Expression.New(constructor, sourceField);

        return BuildNullPreservingExpression(sourceField, copy, fieldType);
    }

    /// <summary>
    /// Builds an expression that recreates an unknown generic enumerable collection when a safe strategy is available.
    /// </summary>
    /// <param name="sourceField">The expression reading the source field.</param>
    /// <param name="fieldType">The field type being copied.</param>
    /// <returns>A collection copy expression when supported; otherwise, the original source-field expression.</returns>
    private static Expression BuildUnknownCollectionCopyExpression(Expression sourceField, Type fieldType)
    {
        if (fieldType == typeof(string))
        {
            return sourceField;
        }

        Type? enumerableType = FindGenericEnumerableType(fieldType);

        if (enumerableType is null)
        {
            return sourceField;
        }

        if (TryFindCompatibleCopyConstructor(fieldType, enumerableType, out ConstructorInfo? copyConstructor))
        {
            ConstructorInfo verifiedCopyConstructor = copyConstructor ?? throw new InvalidOperationException("A compatible copy constructor was expected.");
            Type constructorParameterType = verifiedCopyConstructor.GetParameters()[0].ParameterType;
            Expression copy = Expression.New(verifiedCopyConstructor, Expression.Convert(sourceField, constructorParameterType));

            return BuildNullPreservingExpression(sourceField, copy, fieldType);
        }

        ConstructorInfo? defaultConstructor = fieldType.GetConstructor(Type.EmptyTypes);

        if (defaultConstructor is null)
        {
            return sourceField;
        }

        MethodInfo? addRangeMethod = FindAddRangeMethod(fieldType, enumerableType);

        if (addRangeMethod is not null)
        {
            return BuildUnknownCollectionAddRangeCopyExpression(sourceField, fieldType, enumerableType, defaultConstructor, addRangeMethod);
        }

        MethodInfo? addMethod = FindAddMethod(fieldType, enumerableType);

        if (addMethod is not null)
        {
            return BuildUnknownCollectionAddLoopCopyExpression(sourceField, fieldType, enumerableType, defaultConstructor, addMethod);
        }

        return sourceField;
    }

    /// <summary>
    /// Locates the generic enumerable interface used to enumerate an unknown collection.
    /// </summary>
    /// <param name="fieldType">The collection field type being inspected.</param>
    /// <returns>The matching <see cref="IEnumerable{T}"/> interface, or <see langword="null"/> when none is available.</returns>
    private static Type? FindGenericEnumerableType(Type fieldType)
    {
        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return fieldType;
        }

        return fieldType.GetInterfaces()
            .FirstOrDefault(interfaceType => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    /// <summary>
    /// Attempts to find a public one-parameter constructor that can copy from the source collection or a compatible enumerable interface.
    /// </summary>
    /// <param name="fieldType">The collection field type being copied.</param>
    /// <param name="enumerableType">The generic enumerable interface exposed by the source collection.</param>
    /// <param name="constructor">The located constructor, or <see langword="null"/> when no compatible constructor exists.</param>
    /// <returns><see langword="true"/> when a compatible copy constructor is available; otherwise, <see langword="false"/>.</returns>
    private static bool TryFindCompatibleCopyConstructor(Type fieldType, Type enumerableType, out ConstructorInfo? constructor)
    {
        constructor = fieldType.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Select(candidate => new
            {
                Constructor = candidate,
                Parameters = candidate.GetParameters(),
            })
            .Where(candidate => candidate.Parameters.Length == 1)
            .Select(candidate => new
            {
                candidate.Constructor,
                ParameterType = candidate.Parameters[0].ParameterType,
            })
            .FirstOrDefault(candidate => IsSafeSourceParameter(fieldType, enumerableType, candidate.ParameterType))
            ?.Constructor;

        return constructor is not null;
    }

    /// <summary>
    /// Locates an <c>AddRange(IEnumerable&lt;T&gt;)</c>-style method for an unknown collection.
    /// </summary>
    /// <param name="fieldType">The collection field type being copied.</param>
    /// <param name="enumerableType">The generic enumerable interface exposed by the source collection.</param>
    /// <returns>The matching method, or <see langword="null"/> when no safe method exists.</returns>
    private static MethodInfo? FindAddRangeMethod(Type fieldType, Type enumerableType)
    {
        return fieldType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => new
            {
                Method = method,
                Parameters = method.GetParameters(),
            })
            .Where(candidate => candidate.Method.Name == "AddRange" && candidate.Parameters.Length == 1)
            .FirstOrDefault(candidate => IsSafeSourceParameter(fieldType, enumerableType, candidate.Parameters[0].ParameterType))
            ?.Method;
    }

    /// <summary>
    /// Determines whether a method or constructor parameter safely accepts the source collection.
    /// </summary>
    /// <param name="fieldType">The collection field type being copied.</param>
    /// <param name="enumerableType">The generic enumerable interface exposed by the source collection.</param>
    /// <param name="parameterType">The candidate parameter type.</param>
    /// <returns><see langword="true"/> when the source can be passed safely; otherwise, <see langword="false"/>.</returns>
    private static bool IsSafeSourceParameter(Type fieldType, Type enumerableType, Type parameterType)
    {
        return parameterType != typeof(object)
            && (parameterType.IsAssignableFrom(fieldType) || parameterType.IsAssignableFrom(enumerableType));
    }

    /// <summary>
    /// Locates an <c>Add(T)</c>-style method for an unknown sequential collection.
    /// </summary>
    /// <param name="fieldType">The collection field type being copied.</param>
    /// <param name="enumerableType">The generic enumerable interface exposed by the source collection.</param>
    /// <returns>The matching method, or <see langword="null"/> when no safe method exists.</returns>
    private static MethodInfo? FindAddMethod(Type fieldType, Type enumerableType)
    {
        Type elementType = enumerableType.GetGenericArguments()[0];

        return fieldType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => new
            {
                Method = method,
                Parameters = method.GetParameters(),
            })
            .Where(candidate => candidate.Method.Name == "Add" && candidate.Parameters.Length == 1)
            .FirstOrDefault(candidate => candidate.Parameters[0].ParameterType.IsAssignableFrom(elementType))
            ?.Method;
    }

    /// <summary>
    /// Builds a copy expression that initializes an unknown collection with <c>AddRange(IEnumerable&lt;T&gt;)</c>.
    /// </summary>
    /// <param name="sourceField">The expression reading the source collection.</param>
    /// <param name="fieldType">The collection field type being copied.</param>
    /// <param name="enumerableType">The generic enumerable interface exposed by the source collection.</param>
    /// <param name="defaultConstructor">The public parameterless constructor used to create the target collection.</param>
    /// <param name="addRangeMethod">The method used to add all source elements.</param>
    /// <returns>An expression that returns a copied collection or <see langword="null"/>.</returns>
    private static Expression BuildUnknownCollectionAddRangeCopyExpression(
        Expression sourceField,
        Type fieldType,
        Type enumerableType,
        ConstructorInfo defaultConstructor,
        MethodInfo addRangeMethod)
    {
        ParameterExpression collection = Expression.Variable(fieldType, "collection");
        Expression block = Expression.Block(
            [collection],
            Expression.Assign(collection, Expression.New(defaultConstructor)),
            Expression.Call(collection, addRangeMethod, Expression.Convert(sourceField, addRangeMethod.GetParameters()[0].ParameterType)),
            collection);

        return BuildNullPreservingExpression(sourceField, block, fieldType);
    }

    /// <summary>
    /// Builds a copy expression that initializes an unknown collection by enumerating the source and calling <c>Add(T)</c>.
    /// </summary>
    /// <param name="sourceField">The expression reading the source collection.</param>
    /// <param name="fieldType">The collection field type being copied.</param>
    /// <param name="enumerableType">The generic enumerable interface exposed by the source collection.</param>
    /// <param name="defaultConstructor">The public parameterless constructor used to create the target collection.</param>
    /// <param name="addMethod">The method used to add one source element at a time.</param>
    /// <returns>An expression that returns a copied collection or <see langword="null"/>.</returns>
    private static Expression BuildUnknownCollectionAddLoopCopyExpression(
        Expression sourceField,
        Type fieldType,
        Type enumerableType,
        ConstructorInfo defaultConstructor,
        MethodInfo addMethod)
    {
        Type elementType = enumerableType.GetGenericArguments()[0];
        Type enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);
        ParameterExpression collection = Expression.Variable(fieldType, "collection");
        ParameterExpression enumerator = Expression.Variable(enumeratorType, "enumerator");
        LabelTarget breakLabel = Expression.Label("break");
        MethodInfo getEnumeratorMethod = enumerableType.GetMethod(nameof(IEnumerable<object>.GetEnumerator))
            ?? throw new InvalidOperationException($"The generic enumerator method could not be found for '{enumerableType.FullName}'.");
        MethodInfo moveNextMethod = typeof(System.Collections.IEnumerator).GetMethod(nameof(System.Collections.IEnumerator.MoveNext))
            ?? throw new InvalidOperationException("IEnumerator.MoveNext could not be found.");
        PropertyInfo currentProperty = enumeratorType.GetProperty(nameof(IEnumerator<object>.Current))
            ?? throw new InvalidOperationException($"The generic current property could not be found for '{enumeratorType.FullName}'.");
        MethodInfo disposeMethod = typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))
            ?? throw new InvalidOperationException("IDisposable.Dispose could not be found.");
        Expression currentValue = Expression.Property(enumerator, currentProperty);
        Expression convertedCurrentValue = addMethod.GetParameters()[0].ParameterType == elementType
            ? currentValue
            : Expression.Convert(currentValue, addMethod.GetParameters()[0].ParameterType);
        Expression addCall = Expression.Call(collection, addMethod, convertedCurrentValue);
        Expression addStatement = addCall.Type == typeof(void) ? addCall : Expression.Block(addCall, Expression.Empty());
        Expression loop = Expression.Loop(
            Expression.IfThenElse(
                Expression.Call(enumerator, moveNextMethod),
                addStatement,
                Expression.Break(breakLabel)),
            breakLabel);
        Expression block = Expression.Block(
            [collection, enumerator],
            Expression.Assign(collection, Expression.New(defaultConstructor)),
            Expression.Assign(enumerator, Expression.Call(Expression.Convert(sourceField, enumerableType), getEnumeratorMethod)),
            Expression.TryFinally(loop, Expression.Call(enumerator, disposeMethod)),
            collection);

        return BuildNullPreservingExpression(sourceField, block, fieldType);
    }


    /// <summary>
    /// Builds a conditional expression that preserves null source values for reference-typed copy operations.
    /// </summary>
    /// <param name="sourceField">The expression reading the source field.</param>
    /// <param name="copyWhenNotNull">The expression used when the source field is not null.</param>
    /// <param name="fieldType">The field type being copied.</param>
    /// <returns>An expression that returns null for null sources or the copied value for non-null sources.</returns>
    private static Expression BuildNullPreservingExpression(Expression sourceField, Expression copyWhenNotNull, Type fieldType)
    {
        return Expression.Condition(
            Expression.Equal(sourceField, Expression.Constant(null, fieldType)),
            Expression.Constant(null, fieldType),
            copyWhenNotNull);
    }
}
