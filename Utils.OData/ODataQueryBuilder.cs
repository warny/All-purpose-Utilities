using System.Collections.Specialized;
using Utils.Net;

namespace Utils.OData;

/// <summary>
/// Builds OData-compliant query URLs from <see cref="IQuery"/> definitions.
/// </summary>
public class ODataQueryBuilder
{
    /// <summary>
    /// Gets the fully qualified URL generated from the provided query.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Gets the authorization header extracted from the base URL, if any.
    /// </summary>
    public string? Authorization { get; } = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataQueryBuilder"/> class.
    /// </summary>
    /// <param name="UrlPrefix">The base URL of the OData endpoint.</param>
    /// <param name="query">Query definition used to build the request URL.</param>
    /// <param name="skip">Number of items to skip in addition to <see cref="IQuery.Skip"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="UrlPrefix"/> or <paramref name="query"/> is <see langword="null"/>.</exception>
    public ODataQueryBuilder(string UrlPrefix, IQuery query, int skip = 0)
    {
        ArgumentNullException.ThrowIfNull(UrlPrefix);
        ArgumentNullException.ThrowIfNull(query);

        var builder = new UriBuilderEx(UrlPrefix + "/" + query.Table);

        AddQueryString(builder.QueryString, "$select", query.Select);
        AddQueryString(builder.QueryString, "$filter", query.Filters);
        AddQueryString(builder.QueryString, "$orderby", query.OrderBy);
        AddQueryString(builder.QueryString, "$skip", skip + (query.Skip ?? 0));
        AddQueryString(builder.QueryString, "$top", query.Top);
        AddQueryString(builder.QueryString, "$count", query.Count, false);
        AddQueryString(builder.QueryString, "$search", query.Search);

        Url = builder.GetUrlWithoutAuthorization();

    }

    /// <summary>
    /// Adds or removes a string parameter from the query string collection.
    /// </summary>
    /// <param name="queryString">Target query string collection.</param>
    /// <param name="name">Parameter name to set.</param>
    /// <param name="value">Parameter value to use.</param>
    private static void AddQueryString(QueryString queryString, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) queryString.Remove(name);
        queryString[name] = value;
    }

    /// <summary>
    /// Adds a numeric parameter to the query string collection when a value is provided.
    /// </summary>
    /// <param name="queryString">Target query string collection.</param>
    /// <param name="name">Parameter name to set.</param>
    /// <param name="value">Parameter value to serialize.</param>
    private static void AddQueryString(QueryString queryString, string name, int? value)
    {
        if (value is null) return;
        queryString[name] = value?.ToString();
    }

    /// <summary>
    /// Adds a boolean parameter to the query string collection, removing it when the value equals the default one.
    /// </summary>
    /// <param name="queryString">Target query string collection.</param>
    /// <param name="name">Parameter name to set.</param>
    /// <param name="value">Parameter value to serialize.</param>
    /// <param name="defaultValue">Optional default value used to skip serialization.</param>
    private static void AddQueryString(QueryString queryString, string name, bool value, bool? defaultValue = null)
    {
        if (value == defaultValue) queryString.Remove(name);
        queryString[name] = value.ToString();
    }
}
