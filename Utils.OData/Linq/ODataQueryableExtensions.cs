namespace Utils.OData.Linq;

/// <summary>
/// Provides extension methods for working with OData LINQ queries.
/// </summary>
public static class ODataQueryableExtensions
{
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
}
