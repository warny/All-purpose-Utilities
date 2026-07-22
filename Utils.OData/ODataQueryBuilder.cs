using System.Collections.Specialized;
using System.Globalization;
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
    /// Initializes a new instance of the <see cref="ODataQueryBuilder"/> class.
    /// </summary>
    /// <param name="UrlPrefix">The base URL of the OData endpoint.</param>
    /// <param name="query">Query definition used to build the request URL.</param>
    /// <param name="skip">Number of items to skip in addition to <see cref="IQuery.Skip"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="UrlPrefix"/> or <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="skip"/> is negative, when <see cref="IQuery.Skip"/> is negative, or when <see cref="IQuery.Top"/> is zero or negative.</exception>
    /// <exception cref="OverflowException">Thrown when the combined skip value overflows <see cref="int"/>.</exception>
    public ODataQueryBuilder(string UrlPrefix, IQuery query, int skip = 0)
    {
        ArgumentNullException.ThrowIfNull(UrlPrefix);
        ArgumentNullException.ThrowIfNull(query);

        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be non-negative.");
        if (query.Skip.HasValue && query.Skip.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(query), "Query.Skip must be non-negative.");
        if (query.Top.HasValue && query.Top.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(query), "Query.Top must be a positive integer when specified.");

        int combinedSkip = checked(skip + (query.Skip ?? 0));

        // Item 4: normalize the slash boundary so that a trailing slash on the base URL
        // and a leading slash on the table name do not produce a double slash, and so
        // that a bare table name is always appended as a new path segment.
        string tableSegment = (query.Table ?? string.Empty).TrimStart('/');
        if (tableSegment.IndexOf('?') >= 0 || tableSegment.IndexOf('#') >= 0)
        {
            throw new ArgumentException(
                "The Table value must be a plain entity-set path and must not contain query strings or fragment identifiers.",
                nameof(query));
        }

        string normalizedBase = UrlPrefix.TrimEnd('/');
        var builder = new UriBuilderEx($"{normalizedBase}/{tableSegment}");

        AddQueryString(builder.QueryString, "$select", query.Select);
        AddQueryString(builder.QueryString, "$filter", query.Filters);
        AddQueryString(builder.QueryString, "$orderby", query.OrderBy);
        AddQueryString(builder.QueryString, "$skip", combinedSkip > 0 ? combinedSkip : (int?)null);
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
        if (string.IsNullOrWhiteSpace(value))
        {
            queryString.Remove(name);
            return;
        }
        queryString[name] = value;
    }

    /// <summary>
    /// Adds a numeric parameter to the query string collection when a value is provided.
    /// Uses invariant culture to ensure portable OData URLs regardless of the process locale.
    /// </summary>
    /// <param name="queryString">Target query string collection.</param>
    /// <param name="name">Parameter name to set.</param>
    /// <param name="value">Parameter value to serialize.</param>
    private static void AddQueryString(QueryString queryString, string name, int? value)
    {
        if (value is null) return;
        queryString[name] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Adds a boolean parameter to the query string collection, omitting it when the value equals the default.
    /// Serializes the value as OData-compliant lowercase literals (<c>true</c>/<c>false</c>).
    /// </summary>
    /// <param name="queryString">Target query string collection.</param>
    /// <param name="name">Parameter name to set.</param>
    /// <param name="value">Parameter value to serialize.</param>
    /// <param name="defaultValue">Optional default value used to skip serialization.</param>
    private static void AddQueryString(QueryString queryString, string name, bool value, bool? defaultValue = null)
    {
        if (value == defaultValue)
        {
            queryString.Remove(name);
            return;
        }
        queryString[name] = value ? "true" : "false";
    }
}
