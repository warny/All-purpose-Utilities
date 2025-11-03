# Utils.OData

`Utils.OData` delivers an HTTP client, LINQ provider, and metadata helpers that simplify consuming OData services from **.NET 9** applications. The library can be used on its own or together with the `Utils.OData.Generators` package to produce strongly typed contexts from EDMX metadata.

## Features

- `QueryOData` HTTP client with cookie forwarding, credential support, and automatic paging.
- `ODataQueryBuilder` for constructing compliant query strings from an `IQuery` description.
- `ODataContext` base class that loads EDMX metadata from files or URLs and exposes typed/untyped queryables.
- Metadata converters such as `EdmFieldConverterRegistry` that transform raw responses into CLR values.
- Lightweight LINQ provider enabling `Where`, `Select`, `OrderBy`, and `Take` over OData entity sets.

## Examples

### Building a request

```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Utils.OData;

var query = new Query
{
    Table = "Products",
    Select = "Id,Name,Price",
    Filters = "Price gt 10",
    OrderBy = "Name",
    Top = 50,
};

var client = new QueryOData("https://example.com/odata");
using HttpResponseMessage? response = await client.SimpleQuery(query, cancellationToken: CancellationToken.None);
if (response is not null && response.IsSuccessStatusCode)
{
    string json = await response.Content.ReadAsStringAsync();
    // deserialize or process the payload
}
```

The `QueryOData` helper automatically merges `skip` values and forwards headers and cookies from an incoming `HttpRequestMessage` when required. Responses can be streamed, buffered, or converted to JSON through `QueryToJSon`.

### Typed contexts with LINQ

```csharp
public partial class ProductContext : ODataContext
{
    public ProductContext() : base("metadata/Products.edmx") { }

    public ODataQueryable<Product> Products => Query<Product>("Products");
}

var context = new ProductContext();
var cheapProducts = await context.Products
    .Where(p => p.Price < 20)
    .OrderBy(p => p.Name)
    .Take(10)
    .ToListAsync();
```

For fully typed models, combine this runtime library with the `Utils.OData.Generators` package. The generator inspects the EDMX referenced by `ODataContext` and creates entity classes with properly typed properties and key annotations.

### Server-driven paging and continuation

`QueryOData` exposes helpers for iterating paginated feeds without manually tracking `$skip` or `$top` values.

```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

var client = new QueryOData("https://example.com/odata");
await foreach (HttpResponseMessage page in client.QueryPagedAsync(query, CancellationToken.None))
{
    string json = await page.Content.ReadAsStringAsync();
    // handle each page as it arrives
}
```

The iterator inspects the OData `@odata.nextLink` annotations and issues follow-up requests transparently, making it easy to hydrate large datasets.

### Combining metadata converters

When working with dynamic feeds you can register custom converters with the `EdmFieldConverterRegistry` to map EDM primitive names to CLR types.

```csharp
using Utils.OData;

var registry = new EdmFieldConverterRegistry();
registry.Register("Edm.GeographyPoint", token => GeoJsonConverter.ToPoint(token));
registry.Register("Edm.Guid", token => Guid.Parse(token.ToString()));

var converter = new EdmFieldConverter(registry);
object value = converter.Convert("Edm.GeographyPoint", jsonToken);
```

By extending the registry, applications can deserialize spatial or custom EDM types without duplicating conversion logic across query handlers.

### Constructing queries with the builder

`ODataQueryBuilder` helps compose URLs with complex filters while maintaining readability.

```csharp
var builder = new ODataQueryBuilder()
    .For("Products")
    .Select("Id", "Name", "Price")
    .Filter(f => f
        .GreaterThan("Price", 25)
        .And(f.StartsWith("Name", "S")))
    .OrderBy("Name")
    .Top(25)
    .SkipToken("YWJjMTIz");

string uri = builder.ToString();
// /Products?$select=Id,Name,Price&$filter=(Price gt 25 and startswith(Name,'S'))&$orderby=Name&$top=25&$skiptoken=YWJjMTIz
```

The fluent API mirrors OData keywords which keeps composed URIs consistent with service
expectations.

### Streaming JSON directly

`QueryOData` can stream raw JSON payloads without buffering entire responses in memory.

```csharp
using System.IO;
using System.Threading;

var client = new QueryOData("https://example.com/odata");

await foreach (Stream payload in client.QueryToJSon(query, CancellationToken.None))
{
    using var reader = new StreamReader(payload, leaveOpen: true);
    string page = await reader.ReadToEndAsync();
    ProcessJson(page);
}
```

Each yielded `Stream` represents a page of results which can be parsed incrementally for
low-latency dashboards or ETL jobs.

### Integrating with `HttpClientFactory`

The HTTP client accepts an existing `HttpClient` instance so it can reuse message handlers and
policies configured by `IHttpClientFactory`.

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddODataClient(this IServiceCollection services)
    {
        services.AddHttpClient<QueryOData>(client =>
        {
            client.BaseAddress = new Uri("https://example.com/odata/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }
}

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<QueryOData>();
```

This pattern reuses retry, caching, or resilience handlers registered through the factory.

### Customizing requests with hooks

For ad-hoc customization override `BuildRequestMessage` in a derived class.

```csharp
using System.Net.Http;
using System.Net.Http.Headers;

public class AuthorizedODataClient : QueryOData
{
    private readonly ITokenProvider _tokens;

    public AuthorizedODataClient(string serviceBaseUrl, ITokenProvider tokens)
        : base(serviceBaseUrl)
    {
        _tokens = tokens;
    }

    protected override HttpRequestMessage BuildRequestMessage(Query query)
    {
        HttpRequestMessage request = base.BuildRequestMessage(query);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.Current); 
        return request;
    }
}
```

Customizing the request ensures headers, cookies, or diagnostics are consistently applied before
the HTTP call is sent.
