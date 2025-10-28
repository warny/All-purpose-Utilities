using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Utils.OData;

public class QueryOData : IDisposable
{
	/// <summary>
	/// Gets the base URL used for oData requests.
	/// </summary>
	public string BaseUrl { get; }

	/// <summary>
	/// If provided, credentials used by the internal handler. If an external HttpClient is provided in the ctor,
	/// those credentials won't be applied to that external client.
	/// </summary>
	public NetworkCredential? Credentials
	{
		get => _credentials;
		set
		{
			_credentials = value;
			if (_handler is not null)
			{
				_handler.Credentials = value;
			}
		}
	}

	private NetworkCredential? _credentials;
	private readonly HttpClient? _httpClient;
	private readonly HttpClientHandler? _handler;
	private readonly bool _disposeClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="QueryOData"/> class with the specified base URL.
	/// </summary>
	/// <remarks>This constructor sets up an <see cref="HttpClient"/> with default credentials and a timeout of 600
	/// seconds.</remarks>
	/// <param name="baseUrl">The base URL for the OData service. Cannot be null.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="baseUrl"/> is null.</exception>
	public QueryOData(string baseUrl)
	{
		BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
		_handler = new HttpClientHandler()
		{
			UseDefaultCredentials = true,
			AllowAutoRedirect = true,
			CookieContainer = new CookieContainer(),
			Credentials = Credentials
		};

		_httpClient = new HttpClient(_handler)
		{
			Timeout = TimeSpan.FromSeconds(600)
		};
		_disposeClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="QueryOData"/> class with the specified base URL and HTTP client.
	/// </summary>
	/// <param name="baseUrl">The base URL for the OData service. Cannot be null.</param>
	/// <param name="httpClient">The HTTP client used to send requests. Cannot be null.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="baseUrl"/> or <paramref name="httpClient"/> is null.</exception>
	public QueryOData(string baseUrl, HttpClient httpClient)
	{
		BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_disposeClient = false;
	}

	/// <summary>
	/// Récupère le CookieContainer si la classe utilise son propre handler ; sinon null.
	/// Utile pour transférer des cookies provenant d'une requête entrante.
	/// </summary>
	public CookieContainer? CookieContainer => _handler?.CookieContainer;

	/// <summary>
	/// Exécute une query simple en utilisant le HttpClient interne.
	/// </summary>
	public Task<HttpResponseMessage?> SimpleQuery(IQuery parameter, int skip = 0)
		=> SimpleQuery(parameter, sourceRequest: null, skip);

	/// <summary>
	/// Variante permettant de fournir un HttpRequestMessage source (ex : provenant d'une requête entrante)
	/// dont les headers (et le cookie header) seront copiés dans la requête sortante pour "conserver le contexte HTTP".
	/// </summary>
	public async Task<HttpResponseMessage?> SimpleQuery(IQuery parameter, HttpRequestMessage? sourceRequest = null, int skip = 0)
	{
		ODataQueryBuilder query = new ODataQueryBuilder(
			BaseUrl,
			parameter,
			skip: skip
		);

		HttpResponseMessage response = await HttpGet(query.Url, sourceRequest);
		return response;
	}

	/// <summary>
	/// Effectue le GET en réutilisant le HttpClient interne et, si fourni, en copiant les en-têtes de sourceRequest.
	/// </summary>
	private async Task<HttpResponseMessage> HttpGet(string url, HttpRequestMessage? sourceRequest = null)
	{
		if (_httpClient is null)
			throw new InvalidOperationException("HttpClient is not initialized.");

		using var request = new HttpRequestMessage(HttpMethod.Get, url);

		// Copier les headers (hors headers sensibles traités séparément)
		if (sourceRequest is not null)
		{
			foreach (var header in sourceRequest.Headers)
			{
				// Skip headers that would be invalid on DefaultRequestHeaders but add to the outgoing request
				request.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}

			if (sourceRequest.Content is not null)
			{
				foreach (var header in sourceRequest.Content.Headers)
				{
					request.Headers.TryAddWithoutValidation(header.Key, header.Value);
				}
			}

			// Copier les cookies dans le CookieContainer si nous en avons un
			if (_handler?.CookieContainer is not null)
			{
				if (sourceRequest.Headers.TryGetValues("Cookie", out var cookieHeaders))
				{
					var cookieHeader = string.Join("; ", cookieHeaders);
					try
					{
						var uri = new Uri(BaseUrl);
						_handler.CookieContainer.SetCookies(uri, cookieHeader);
					}
					catch
					{
						// Ne doit pas planter l'appel ; si BaseUrl n'est pas une URI valide ou autre, on ignore.
					}
				}
			}
		}

		Console.WriteLine($"Requesting : {url}");
		var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
		return response;
	}


	/// <summary>
	/// Executes a query and converts the result to a JSON format.
	/// </summary>
	/// <remarks>This method performs an asynchronous query operation and processes the resulting data stream into a
	/// JSON array.  It also retrieves associated metadata. If the query or conversion encounters an error, the method
	/// returns the error details.</remarks>
	/// <param name="parameter">The query parameter that defines the data retrieval criteria.</param>
	/// <param name="skip">The number of records to skip in the query result. Must be non-negative.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains a tuple with a <see cref="JsonArray"/>
	/// of data and a dictionary of metadata. Returns an error if the query or conversion fails.</returns>
	public async Task<ReturnValue<(JsonArray? Datas, Dictionary<string, string>? Metadatas)>> QueryToJSon(IQuery parameter, int skip = 0)
	{
		var queryResult = await QueryDatas(parameter, skip);

		if (queryResult.IsError) return new(queryResult.Error);

		var stream = queryResult.Value;

		var convertResult = ConvertDatas(stream);
		if (convertResult.IsError) return new(convertResult.Error);
		return convertResult;
	}

	/// <summary>
	/// Converts the data from the provided stream into a JSON array and extracts metadata.
	/// </summary>
	/// <remarks>The method parses the JSON content from the stream, expecting an array under the "value" key and
	/// additional metadata as key-value pairs. If the "value" array is null or empty, the method returns an
	/// error.</remarks>
	/// <param name="stream">The input stream containing JSON data to be parsed.</param>
	/// <returns>A <see cref="ReturnValue{T}"/> containing a tuple with a <see cref="JsonArray"/> of data and a dictionary of
	/// metadata. Returns an error code and message if no data is found.</returns>
	private static ReturnValue<(JsonArray? Datas, Dictionary<string, string>? Metadatas)> ConvertDatas(Stream stream)
	{
		var content = JsonNode.Parse(stream);
		var array = content?["value"]?.AsArray();

		var metadatas = content?.AsObject()
			.Where(e => e.Key != "value")
			.ToDictionary(e => e.Key, e => e.Value.AsValue().ToString());

		if (array is null || array.Count == 0) return new(1, "Aucune donnée retournée");
		return new((array, metadatas));
	}

	/// <summary>
	/// Executes a query based on the specified parameters and returns the result as a stream.
	/// </summary>
	/// <remarks>This method performs an asynchronous query operation and returns the result as a stream. If the
	/// query fails, the method returns an error code and message indicating the failure reason.</remarks>
	/// <param name="parameter">The query parameters used to define the data retrieval operation. Cannot be null.</param>
	/// <param name="skip">The number of records to skip in the query results. Must be non-negative.</param>
	/// <returns>A <see cref="ReturnValue{Stream}"/> containing the query result stream if successful; otherwise, an error code and
	/// message.</returns>
	private async Task<ReturnValue<Stream>> QueryDatas(IQuery parameter, int skip = 0)
	{
		var response = await SimpleQuery(parameter, skip: skip);

		if (response is null) return new(-2, "no response returned");
		if (!response.IsSuccessStatusCode) return new(-1, $"{(int)response.StatusCode} {response.ReasonPhrase}");

		return await response.Content.ReadAsStreamAsync();
	}

	/// <summary>
	/// Retrieves the column names from the specified query and JSON array.
	/// </summary>
	/// <param name="parameter">The query parameter containing the selection criteria. If the <c>Select</c> property is null or whitespace, all
	/// columns from the first JSON object in the array are returned.</param>
	/// <param name="array">The JSON array from which to extract column names. The array must contain at least one JSON object.</param>
	/// <returns>An array of strings representing the column names. If <paramref name="parameter"/> has a non-empty <c>Select</c>
	/// property, the specified columns are returned; otherwise, all columns from the first JSON object are returned.</returns>
	public static string[] GetColumns(IQuery parameter, JsonArray array)
	{
		string[] columns;
		if (string.IsNullOrWhiteSpace(parameter.Select))
		{
			var firstRow = array[0];
			var o = firstRow.AsObject();
			columns = o.Select(c => c.Key).ToArray();
		}
		else
		{
			columns = parameter.Select.Split(",").Select(c => c.Trim()).ToArray();
		}

		return columns;
	}

	#region IDisposable
	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposeClient)
		{
			_httpClient?.Dispose();
			_handler?.Dispose();
		}
		GC.SuppressFinalize(this);
	}
	#endregion
}
