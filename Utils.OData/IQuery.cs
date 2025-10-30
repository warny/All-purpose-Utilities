namespace Utils.OData;

public interface IQuery
{
	/// <summary>
	/// Table name
	/// </summary>
	public string Table { get; set; }

	/// <summary>
	/// Gets or sets the filter criteria used to refine search results.
	/// </summary>
	public string? Filters { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to count the total number of items matching the query.
	/// </summary>
	public bool Count { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of items to retrieve.
	/// </summary>
	public int? Top { get; set; }

	/// <summary>
	/// Gets or sets the number of items to skip.
	/// </summary>
	public int? Skip { get; set; }

	/// <summary>
	/// Gets or sets the criteria used to order the results.
	/// </summary>
	public string? OrderBy { get; set; }

	/// <summary>
	/// Gets or sets the fields to be selected.
	/// </summary>
	public string? Select { get; set; }

	/// <summary>
	/// Gets or sets a full text search string query.
	/// </summary>
	public string? Search { get; set; }
}
