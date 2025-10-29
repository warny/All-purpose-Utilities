using System.Linq;
using System.Text;

namespace Utils.OData.Linq;

/// <summary>
/// Represents the result of compiling a LINQ expression tree into an OData query.
/// </summary>
public sealed class ODataQueryCompilation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ODataQueryCompilation"/> class.
    /// </summary>
    /// <param name="entitySetName">The name of the entity set targeted by the query.</param>
    /// <param name="filters">The set of filter expressions applied to the query.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entitySetName"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filters"/> is null.</exception>
    public ODataQueryCompilation(string entitySetName, IReadOnlyList<string> filters)
    {
        if (string.IsNullOrWhiteSpace(entitySetName))
        {
            throw new ArgumentException("The entity set name must be provided.", nameof(entitySetName));
        }

        EntitySetName = entitySetName;
        Filters = (filters ?? throw new ArgumentNullException(nameof(filters))).ToArray();
    }

    /// <summary>
    /// Gets the name of the entity set targeted by the query.
    /// </summary>
    public string EntitySetName { get; }

    /// <summary>
    /// Gets the sequence of filter expressions applied to the query.
    /// </summary>
    public IReadOnlyList<string> Filters { get; }

    /// <summary>
    /// Builds the URI fragment that corresponds to the compiled query.
    /// </summary>
    /// <returns>The entity set name with appended OData query options when required.</returns>
    public string ToUriString()
    {
        if (Filters.Count == 0)
        {
            return EntitySetName;
        }

        var builder = new StringBuilder();
        builder.Append(EntitySetName);
        builder.Append("?$filter=");
        builder.Append(string.Join(" and ", Filters));
        return builder.ToString();
    }
}
