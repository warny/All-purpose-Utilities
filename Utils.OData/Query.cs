namespace Utils.OData;

/// <summary>
/// Represents a mutable OData query definition used to build request URIs.
/// </summary>
public class Query : IQuery
{
    /// <inheritdoc/>
    public required string Table { get; set; }
    /// <inheritdoc/>
    public string? Filters { get; set; } = null;
    /// <inheritdoc/>
    public bool Count { get; set; } = false;
    /// <inheritdoc/>
    public int? Top { get; set; }
    /// <inheritdoc/>
    public int? Skip { get; set; }
    /// <inheritdoc/>
    public string? OrderBy { get; set; }
    /// <inheritdoc/>
    public string? Select { get; set; } = null;
    /// <inheritdoc/>
    public string? Search { get; set; }
}
