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
/// Fields whose declared type implements <see cref="ICloneable"/> are copied by calling <see cref="ICloneable.Clone"/> on the field value
/// so that objects expressing their own state-copy contract are respected. Unknown references and collections without a safe reconstruction
/// strategy are copied by reference. When <see cref="Copy"/> receives a source context that implements <see cref="ICloneable"/>, that clone
/// contract takes priority and the supplied factory is not invoked. Static fields and field-like event backing fields are ignored.
/// Readonly instance fields are rejected because they cannot be assigned safely during normal field-copy operations. Reflection is used only
/// while building the delegate cached by the closed generic type; subsequent field-copy operations invoke the compiled delegate.
/// </remarks>
public static class ParserExecutionContextCopier<TContext>
    where TContext : class
{
    /// <summary>
    /// Stores the lazily built copy delegate for this closed context type.
    /// </summary>
    private static readonly Lazy<Action<TContext, TContext>> CopyToCore = new(BuildCopyToCore, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Maps known types and open generic type definitions to their dedicated copy-expression builders.
    /// Lookup resolves <see cref="Type.GetGenericTypeDefinition"/> for generic types and the type itself otherwise.
    /// Types not present in this map are handled by structural checks (arrays, <see cref="ICloneable"/>) or the unknown-collection fallback.
    /// </summary>
    private static readonly Dictionary<Type, Func<Expression, Type, Expression>> KnownCopyBuilders = new()
    {
        [typeof(string)] = static (f, _) => f,
        [typeof(List<>)] = BuildEnumerableConstructorCopyExpression,
        [typeof(HashSet<>)] = BuildEnumerableConstructorCopyExpression,
        [typeof(Dictionary<,>)] = BuildDictionaryCopyExpression,
    };

    /// <summary>
    /// Creates a new context instance by using <see cref="ICloneable.Clone"/> when available, or through <paramref name="factory"/> followed by field copying.
    /// </summary>
    /// <param name="source">The context instance whose state is copied.</param>
    /// <param name="factory">The caller-provided factory used to create the target context instance when <paramref name="source"/> does not implement <see cref="ICloneable"/>.</param>
    /// <returns>A new context instance containing the copied state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>, or <paramref name="factory"/> is <see langword="null"/> when field copying is required.</exception>
    /// <exception cref="InvalidOperationException"><see cref="ICloneable.Clone"/> returns <see langword="null"/>, returns a value that is not assignable to <typeparamref name="TContext"/>, the factory returns <see langword="null"/>, or the context type contains an unsupported readonly instance field during field copying.</exception>
    public static TContext Copy(TContext source, Func<TContext> factory)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is ICloneable cloneableSource)
        {
            return CopyCloneable(cloneableSource);
        }

        ArgumentNullException.ThrowIfNull(factory);

        TContext target = factory();

        if (target is null)
        {
            throw new InvalidOperationException(
                $"The factory must return a non-null {typeof(TContext).Name} instance.");
        }

        CopyTo(source, target);
        return target;
    }

    /// <summary>
    /// Copies a cloneable source context by delegating ownership of clone semantics to the source type.
    /// </summary>
    /// <param name="cloneableSource">The source context exposed through <see cref="ICloneable"/>.</param>
    /// <returns>The cloned context after validating the clone result.</returns>
    /// <exception cref="InvalidOperationException"><see cref="ICloneable.Clone"/> returns <see langword="null"/> or a value that is not assignable to <typeparamref name="TContext"/>.</exception>
    private static TContext CopyCloneable(ICloneable cloneableSource)
    {
        object? clone = cloneableSource.Clone();

        if (clone is null)
        {
            throw new InvalidOperationException(
                $"ICloneable.Clone() on '{typeof(TContext).FullName ?? typeof(TContext).Name}' returned null. " +
                $"ParserExecutionContextCopier<T> requires Clone() to return a non-null value assignable to '{typeof(TContext).FullName ?? typeof(TContext).Name}'.");
        }

        if (clone is not TContext typedClone)
        {
            throw new InvalidOperationException(
                $"ICloneable.Clone() on '{typeof(TContext).FullName ?? typeof(TContext).Name}' returned '{clone.GetType().FullName ?? clone.GetType().Name}', " +
                $"which is not assignable to '{typeof(TContext).FullName ?? typeof(TContext).Name}'.");
        }

        return typedClone;
    }

    /// <summary>
    /// Copies the state of <paramref name="source"/> into an existing <paramref name="target"/> context instance through field copying.
    /// </summary>
    /// <param name="source">The context instance whose state is copied.</param>
    /// <param name="target">The context instance that receives the copied state.</param>
    /// <remarks>This method intentionally does not use <see cref="ICloneable"/> because callers provide the target instance that must receive the copied fields.</remarks>
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
        if (fieldType.IsValueType)
        {
            return sourceField;
        }

        Type lookupKey = fieldType.IsGenericType ? fieldType.GetGenericTypeDefinition() : fieldType;

        if (KnownCopyBuilders.TryGetValue(lookupKey, out Func<Expression, Type, Expression>? builder))
        {
            return builder(sourceField, fieldType);
        }

        if (fieldType.IsArray)
        {
            return BuildArrayCopyExpression(sourceField, fieldType);
        }

        if (typeof(ICloneable).IsAssignableFrom(fieldType))
        {
            return BuildCloneableFieldCopyExpression(sourceField, fieldType);
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
    /// Builds an expression that recreates a collection using its <see cref="IEnumerable{T}"/> copy constructor.
    /// </summary>
    /// <param name="sourceField">The expression reading the source collection.</param>
    /// <param name="fieldType">The concrete collection field type.</param>
    /// <returns>An expression that returns a copied collection or <see langword="null"/>.</returns>
    private static Expression BuildEnumerableConstructorCopyExpression(Expression sourceField, Type fieldType)
    {
        Type elementType = fieldType.GetGenericArguments()[0];
        Type enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        ConstructorInfo constructor = fieldType.GetConstructor([enumerableType])
            ?? throw new InvalidOperationException($"The enumerable copy constructor could not be found for '{fieldType.FullName}'.");

        return BuildNullPreservingExpression(sourceField, Expression.New(constructor, sourceField), fieldType);
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

        return BuildNullPreservingExpression(sourceField, Expression.New(constructor, sourceField), fieldType);
    }

    /// <summary>
    /// Builds an expression that copies an <see cref="ICloneable"/> field by calling <see cref="ICloneable.Clone"/> on the field value.
    /// </summary>
    /// <param name="sourceField">The expression reading the source field.</param>
    /// <param name="fieldType">The field type implementing <see cref="ICloneable"/>.</param>
    /// <returns>An expression that returns the cloned value or <see langword="null"/>.</returns>
    private static Expression BuildCloneableFieldCopyExpression(Expression sourceField, Type fieldType)
    {
        MethodInfo cloneMethod = typeof(ICloneable).GetMethod(nameof(ICloneable.Clone))
            ?? throw new InvalidOperationException("ICloneable.Clone could not be found.");
        Expression clone = Expression.Convert(Expression.Call(sourceField, cloneMethod), fieldType);

        return BuildNullPreservingExpression(sourceField, clone, fieldType);
    }

    /// <summary>
    /// Builds an expression that recreates an unknown generic enumerable collection when a safe strategy is available.
    /// </summary>
    /// <param name="sourceField">The expression reading the source field.</param>
    /// <param name="fieldType">The field type being copied.</param>
    /// <returns>A collection copy expression when supported; otherwise, the original source-field expression.</returns>
    private static Expression BuildUnknownCollectionCopyExpression(Expression sourceField, Type fieldType)
    {
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
            .FirstOrDefault(candidate =>
                candidate.Parameters[0].ParameterType != typeof(object) &&
                candidate.Parameters[0].ParameterType.IsAssignableFrom(elementType))
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
