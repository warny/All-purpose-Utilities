using System.Diagnostics.CodeAnalysis;

namespace Utils.OData.Linq;

/// <summary>
/// Represents a row projected from an OData entity set when the entity type is not known at compile time.
/// </summary>
public sealed class ODataUntypedRow
{
    /// <summary>
    /// Retrieves the value for the provided column name. This member is only intended for use inside LINQ expressions.
    /// </summary>
    /// <typeparam name="TValue">Type expected for the column value.</typeparam>
    /// <param name="columnName">Name of the column to access.</param>
    /// <returns>This method never returns because it always throws.</returns>
    /// <exception cref="NotSupportedException">Always thrown when the method is invoked directly.</exception>
    [ExcludeFromCodeCoverage]
    public TValue? GetValue<TValue>(string columnName)
    {
        throw new NotSupportedException("Untyped OData rows can only be used inside LINQ expression trees.");
    }

    /// <summary>
    /// Gets the value for the specified column name. This indexer is only provided for building expression trees.
    /// </summary>
    /// <param name="columnName">Name of the column to access.</param>
    /// <returns>This member never returns because it always throws.</returns>
    /// <exception cref="NotSupportedException">Always thrown when the indexer is evaluated.</exception>
    [ExcludeFromCodeCoverage]
    public object? this[string columnName]
    {
        get
        {
            throw new NotSupportedException("Untyped OData rows can only be used inside LINQ expression trees.");
        }
    }
}
