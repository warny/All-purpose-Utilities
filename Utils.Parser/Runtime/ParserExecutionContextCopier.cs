using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Parser.Runtime;

/// <summary>
/// Copies parser execution-context instances with a cached, compiled field-copy delegate for each context type.
/// </summary>
/// <typeparam name="TContext">The parser execution-context type to copy.</typeparam>
/// <remarks>
/// The copier performs a shallow structural copy intended for parser execution contexts. It recreates known collection containers
/// such as arrays, <see cref="List{T}"/>, <see cref="Dictionary{TKey,TValue}"/>, and <see cref="HashSet{T}"/>, but it does not deep-clone
/// the elements stored in those containers. Unrecognized reference values are copied by reference. Static fields and field-like event
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
                return BuildKnownCollectionCopyExpression(sourceField, fieldType);
            }

            if (genericDefinition == typeof(Dictionary<,>))
            {
                return BuildKnownCollectionCopyExpression(sourceField, fieldType);
            }

            if (genericDefinition == typeof(HashSet<>))
            {
                return BuildKnownCollectionCopyExpression(sourceField, fieldType);
            }
        }

        return sourceField;
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
    /// Builds an expression that recreates a supported collection when it is not null.
    /// </summary>
    /// <param name="sourceField">The expression reading the source collection.</param>
    /// <param name="fieldType">The concrete supported collection type.</param>
    /// <returns>An expression that returns a copied collection or <see langword="null"/>.</returns>
    private static Expression BuildKnownCollectionCopyExpression(Expression sourceField, Type fieldType)
    {
        ConstructorInfo constructor = fieldType.GetConstructor([fieldType])
            ?? throw new InvalidOperationException($"A copy constructor could not be found for '{fieldType.FullName}'.");
        Expression copy = Expression.New(constructor, sourceField);

        return BuildNullPreservingExpression(sourceField, copy, fieldType);
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
