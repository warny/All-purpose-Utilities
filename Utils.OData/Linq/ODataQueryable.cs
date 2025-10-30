using System.Collections;
using System.Linq;
using System.Linq.Expressions;

namespace Utils.OData.Linq;

/// <summary>
/// Represents a queryable OData entity set that can be composed using LINQ.
/// </summary>
/// <typeparam name="TEntity">Type of the entity described by the query.</typeparam>
public sealed class ODataQueryable<TEntity> : IOrderedQueryable<TEntity>, IODataQueryableRoot
{
    private readonly Expression _expression;

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataQueryable{TEntity}"/> class.
    /// </summary>
    /// <param name="provider">The query provider responsible for translating expressions.</param>
    /// <param name="entitySetName">The name of the entity set targeted by the query.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entitySetName"/> is null or whitespace.</exception>
    internal ODataQueryable(ODataQueryProvider provider, string entitySetName)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        EntitySetName = ValidateEntitySet(entitySetName);
        _expression = Expression.Constant(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataQueryable{TEntity}"/> class with a specific expression.
    /// </summary>
    /// <param name="provider">The provider that will evaluate the query.</param>
    /// <param name="expression">The expression tree representing the query.</param>
    /// <param name="entitySetName">The entity set targeted by the query.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> or <paramref name="expression"/> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entitySetName"/> is null or whitespace.</exception>
    internal ODataQueryable(ODataQueryProvider provider, Expression expression, string entitySetName)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        EntitySetName = ValidateEntitySet(entitySetName);
    }

    /// <summary>
    /// Gets the expression tree associated with the query.
    /// </summary>
    public Expression Expression => _expression;

    /// <summary>
    /// Gets the type of the elements returned by the query.
    /// </summary>
    public Type ElementType => typeof(TEntity);

    /// <summary>
    /// Gets the query provider used to translate the query.
    /// </summary>
    public IQueryProvider Provider { get; }

    /// <summary>
    /// Gets the name of the entity set targeted by the query.
    /// </summary>
    public string EntitySetName { get; }

    /// <summary>
    /// Returns an enumerator over the query results. Execution is not supported yet and the method throws an exception.
    /// </summary>
    /// <returns>This method does not return and always throws.</returns>
    /// <exception cref="NotSupportedException">Thrown whenever enumeration is attempted.</exception>
    public IEnumerator<TEntity> GetEnumerator()
    {
        throw new NotSupportedException("Enumeration of OData queries is not supported by the in-memory compiler.");
    }

    /// <summary>
    /// Returns an enumerator over the query results. Execution is not supported yet and the method throws an exception.
    /// </summary>
    /// <returns>This method does not return and always throws.</returns>
    /// <exception cref="NotSupportedException">Thrown whenever enumeration is attempted.</exception>
    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotSupportedException("Enumeration of OData queries is not supported by the in-memory compiler.");
    }

    /// <summary>
    /// Validates the entity set name argument.
    /// </summary>
    /// <param name="entitySetName">The entity set name to validate.</param>
    /// <returns>The validated name.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided value is null or whitespace.</exception>
    private static string ValidateEntitySet(string entitySetName)
    {
        if (string.IsNullOrWhiteSpace(entitySetName))
        {
            throw new ArgumentException("The entity set name must be provided.", nameof(entitySetName));
        }

        return entitySetName;
    }
}

/// <summary>
/// Exposes the metadata required by the query translator to resolve the query root.
/// </summary>
internal interface IODataQueryableRoot
{
    /// <summary>
    /// Gets the name of the entity set targeted by the query root.
    /// </summary>
    string EntitySetName { get; }

    /// <summary>
    /// Gets the type of the elements represented by the query root.
    /// </summary>
    Type ElementType { get; }
}
