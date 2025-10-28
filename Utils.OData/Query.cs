namespace Utils.OData;

public class Query : IQuery
{
	/// <inheritdoc/>
	public string Table { get; set; }
	/// <inheritdoc/>
	public string Filters { get; set; }
	/// <inheritdoc/>
	public bool Count { get; set; } = false;
	/// <inheritdoc/>
	public int? Top { get; set; }
	/// <inheritdoc/>
	public int? Skip { get; set; }
	/// <inheritdoc/>
	public string? OrderBy { get; set; }
	/// <inheritdoc/>
	public string Select { get; set; }
	/// <inheritdoc/>
	public string? Search { get; set; }
}
