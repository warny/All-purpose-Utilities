using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.OData.Linq;

/// <summary>
/// Provides extension methods for working with OData LINQ queries.
/// </summary>
public static class ODataQueryableExtensions
{
    /// <summary>
    /// Caches the reflection handle to the generic <see cref="Expand{TEntity}(IQueryable{TEntity}, string[])"/> method.
    /// </summary>
    private static readonly MethodInfo ExpandMethodDefinition = typeof(ODataQueryableExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.IsGenericMethodDefinition && method.Name == nameof(Expand) && method.GetParameters().Length == 2);

    /// <summary>
    /// Compiles the provided queryable sequence into an <see cref="ODataQueryCompilation"/>.
    /// </summary>
    /// <param name="source">The queryable sequence to compile.</param>
    /// <returns>The compiled query representation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the query provider is not an <see cref="ODataQueryProvider"/>.</exception>
    public static ODataQueryCompilation CompileToODataQuery(this IQueryable source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (source.Provider is not ODataQueryProvider provider)
        {
            throw new InvalidOperationException("The queryable sequence is not backed by an OData query provider.");
        }

        return provider.Compile(source.Expression);
    }

    /// <summary>
    /// Requests the expansion of the specified navigation properties in the resulting OData query.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity described by the query.</typeparam>
    /// <param name="source">The queryable sequence to extend.</param>
    /// <param name="navigationProperties">Names of the navigation properties to expand.</param>
    /// <returns>A new <see cref="IQueryable{T}"/> instance that includes the requested expansions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="navigationProperties"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="navigationProperties"/> is empty or contains invalid entries.</exception>
    public static IQueryable<TEntity> Expand<TEntity>(this IQueryable<TEntity> source, params string[] navigationProperties)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (navigationProperties is null)
        {
            throw new ArgumentNullException(nameof(navigationProperties));
        }

        if (navigationProperties.Length == 0)
        {
            throw new ArgumentException("At least one navigation property must be provided.", nameof(navigationProperties));
        }

        string[] properties = navigationProperties.ToArray();
        foreach (string property in properties)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                throw new ArgumentException("Navigation property names must be non-empty strings.", nameof(navigationProperties));
            }
        }

        MethodInfo expandMethod = ExpandMethodDefinition.MakeGenericMethod(typeof(TEntity));
        Expression call = Expression.Call(
            null,
            expandMethod,
            source.Expression,
            Expression.Constant(properties));

        return source.Provider.CreateQuery<TEntity>(call);
    }
}
