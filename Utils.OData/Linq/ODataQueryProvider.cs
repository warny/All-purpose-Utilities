using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.OData.Linq;

/// <summary>
/// Provides the LINQ provider responsible for compiling OData queries.
/// </summary>
public sealed class ODataQueryProvider : IQueryProvider
{
    private readonly string _entitySetName;

    /// <summary>
    /// Gets the cached reflection handle to the generic <see cref="CreateQuery{TElement}"/> method.
    /// </summary>
    private static readonly MethodInfo GenericCreateQueryMethod = typeof(ODataQueryProvider)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.IsGenericMethodDefinition && method.Name == nameof(CreateQuery) && method.GetParameters().Length == 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataQueryProvider"/> class.
    /// </summary>
    /// <param name="entitySetName">The entity set associated with the provider.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entitySetName"/> is null or whitespace.</exception>
    public ODataQueryProvider(string entitySetName)
    {
        if (string.IsNullOrWhiteSpace(entitySetName))
        {
            throw new ArgumentException("The entity set name must be provided.", nameof(entitySetName));
        }

        _entitySetName = entitySetName;
    }

    /// <summary>
    /// Creates a strongly typed query from an expression tree.
    /// </summary>
    /// <typeparam name="TElement">Type of the entity represented by the query.</typeparam>
    /// <param name="expression">The expression tree representing the query.</param>
    /// <returns>A new <see cref="ODataQueryable{TEntity}"/> instance.</returns>
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        return new ODataQueryable<TElement>(this, expression, _entitySetName);
    }

    /// <summary>
    /// Creates a query from an expression tree without a compile-time generic argument.
    /// </summary>
    /// <param name="expression">The expression tree representing the query.</param>
    /// <returns>An <see cref="IQueryable"/> instance.</returns>
    public IQueryable CreateQuery(Expression expression)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        var elementType = ResolveElementType(expression.Type);
        var method = GenericCreateQueryMethod.MakeGenericMethod(elementType);
        return (IQueryable)method.Invoke(this, new object[] { expression })!;
    }

    /// <summary>
    /// Executes the query represented by an expression tree and converts it into an <see cref="ODataQueryCompilation"/>.
    /// </summary>
    /// <typeparam name="TResult">Type expected by the caller.</typeparam>
    /// <param name="expression">The expression tree to compile.</param>
    /// <returns>The compiled query representation.</returns>
    public TResult Execute<TResult>(Expression expression)
    {
        var compilation = Compile(expression);
        if (compilation is TResult typedResult)
        {
            return typedResult;
        }

        if (typeof(TResult) == typeof(object))
        {
            return (TResult)(object)compilation;
        }

        // Item 27: this provider compiles LINQ expressions to OData URI syntax only.
        // Supported LINQ operators: Where (→ $filter), Expand (→ $expand).
        // Unsupported terminal operators (Count, Any, First, Select, OrderBy, Skip, Take, …)
        // must be applied after materialising the OData response as LINQ-to-Objects.
        throw new NotSupportedException(
            $"The OData LINQ provider cannot execute '{typeof(TResult).Name}' terminal operators. " +
            "It compiles Where/$filter and Expand/$expand to an ODataQueryCompilation only. " +
            "Retrieve the data first, then apply aggregation or projection with LINQ-to-Objects.");
    }

    /// <summary>
    /// Executes the query represented by an expression tree and converts it into an <see cref="ODataQueryCompilation"/>.
    /// </summary>
    /// <param name="expression">The expression tree to compile.</param>
    /// <returns>The compiled query representation.</returns>
    public object Execute(Expression expression)
    {
        return Compile(expression);
    }

    /// <summary>
    /// Compiles the provided expression tree into an <see cref="ODataQueryCompilation"/> instance.
    /// </summary>
    /// <param name="expression">Expression tree describing the query.</param>
    /// <returns>The compiled query.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> is <see langword="null"/>.</exception>
    public ODataQueryCompilation Compile(Expression expression)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        return ODataQueryTranslator.Translate(expression, _entitySetName);
    }

    /// <summary>
    /// Resolves the element type produced by an <see cref="IQueryable"/> sequence expression.
    /// </summary>
    /// <remarks>
    /// Item 28: the previous implementation took the first generic argument of any generic type,
    /// which is incorrect for types whose first type parameter is not the sequence element
    /// (e.g. <c>Dictionary&lt;K, V&gt;</c>).  The correct strategy is to look for the
    /// <see cref="IQueryable{T}"/> interface and extract its single type argument.
    /// </remarks>
    /// <param name="sequenceType">Type describing the queryable sequence.</param>
    /// <returns>The element type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sequenceType"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">Thrown when no <see cref="IQueryable{T}"/> interface can be found.</exception>
    private static Type ResolveElementType(Type sequenceType)
    {
        if (sequenceType is null)
        {
            throw new ArgumentNullException(nameof(sequenceType));
        }

        // Check the type itself first, then its interfaces.
        if (sequenceType.IsGenericType && sequenceType.GetGenericTypeDefinition() == typeof(IQueryable<>))
        {
            return sequenceType.GetGenericArguments()[0];
        }

        foreach (Type iface in sequenceType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                return iface.GetGenericArguments()[0];
            }
        }

        throw new NotSupportedException(
            $"Cannot determine the element type for '{sequenceType.FullName}': " +
            "the type does not implement IQueryable<T>.");
    }
}
