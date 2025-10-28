using System.Collections.Specialized;
using System.Text;
using Utils.Net;

namespace Utils.OData;

public class ODataQueryBuilder
{
	public string Url { get; }
	public string? Authorization { get; } = null;

	public ODataQueryBuilder(string UrlPrefix, IQuery query, int skip=0)
	{
		var builder = new UriBuilderEx(Path.Combine(UrlPrefix, query.Table));

		AddQueryString(builder.QueryString, "$select", query.Select);
		AddQueryString(builder.QueryString, "$filter", query.Filters);
		AddQueryString(builder.QueryString, "$orderby", query.OrderBy);
		AddQueryString(builder.QueryString, "$skip", skip + (query.Skip ?? 0));
		AddQueryString(builder.QueryString, "$top", query.Top);
		AddQueryString(builder.QueryString, "$count", query.Count, false);
		AddQueryString(builder.QueryString, "$search", query.Search);

		Url = builder.GetUrlWithoutAuthorization();

	}

	private static void AddQueryString(QueryString queryString, string name, string? value) 
	{
		if (string.IsNullOrWhiteSpace(value)) queryString.Remove(name);
		queryString[name] = value;
	}

	private static void AddQueryString(QueryString queryString, string name, int? value)
	{
		if (value is null) return;
		queryString[name] = value?.ToString();
	}

	private static void AddQueryString(QueryString queryString, string name, bool value, bool? defaultValue = null)
	{
		if (value == defaultValue) queryString.Remove(name);
		queryString[name] = value.ToString();
	}
}
