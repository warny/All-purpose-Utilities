# omy.Utils.OData (OData client helpers)

`omy.Utils.OData` delivers an HTTP client, a LINQ expression compiler, and metadata utilities for consuming OData services.

## Install
```bash
dotnet add package omy.Utils.OData
```

## Supported frameworks
- net9.0

## Features
- `QueryOData` — HTTP client with automatic paging, cookie forwarding, and credential support.
- `Query` / `IQuery` — query definition with `Table`, `Filters`, `Select`, `Top`, `Skip`, `OrderBy`, `Search`, `Count`.
- `ODataContext` — abstract base class that loads EDMX metadata and exposes typed/untyped LINQ queryables.
- `ODataQueryable<T>` + `CompileToODataQuery()` — LINQ expression compiler that translates expressions into OData query parameters.
- `Expand(params string[])` — appends `$expand` navigation property segments to a query.

## Quick usage

```csharp
using Utils.OData;

var query = new Query
{
    Table   = "Products",
    Select  = "Id,Name,Price",
    Filters = "Price gt 10",
    OrderBy = "Name",
    Top     = 50,
};

using var client = new QueryOData("https://example.com/odata");
HttpResponseMessage? response = await client.SimpleQuery(query);
if (response is not null && response.IsSuccessStatusCode)
{
    string json = await response.Content.ReadAsStringAsync();
    // deserialize or process the OData JSON payload
}
```

## QueryOData examples

### Aggregated JSON with automatic paging

`QueryToJSon` transparently fetches subsequent pages and returns all matching rows as a single `JsonArray`.

```csharp
using System.Text.Json.Nodes;
using Utils.OData;

using var client = new QueryOData("https://example.com/odata");

var query = new Query { Table = "Orders", Filters = "Status eq 'Open'" };
var result = await client.QueryToJSon(query, maxPerRequest: 100);

if (!result.IsError)
{
    JsonArray? rows = result.Value.Datas;
    Console.WriteLine($"Retrieved {rows?.Count} orders.");
}
```

### Streaming rows as IDataReader

`QueryToDataReader` returns an `IDataReader` that downloads subsequent pages in the background, suitable for large datasets.

```csharp
using System.Data;
using Utils.OData;

using var client = new QueryOData("https://example.com/odata");

var query = new Query { Table = "Transactions", Select = "Id,Amount,Date", Top = 10_000 };
var readerResult = await client.QueryToDataReader(query, maxPerRequest: 500);

if (!readerResult.IsError)
{
    using IDataReader reader = readerResult.Value;
    while (reader.Read())
    {
        int    id     = reader.GetInt32(0);
        decimal amount = reader.GetDecimal(1);
        Console.WriteLine($"{id}: {amount:C}");
    }
}
```

### Forwarding session context

Pass an incoming `HttpRequestMessage` to copy its headers and cookies to the outgoing OData request:

```csharp
using Utils.OData;

using var client = new QueryOData("https://example.com/odata");
var query = new Query { Table = "UserData" };
HttpResponseMessage? response = await client.SimpleQuery(query, sourceRequest: incomingRequest);
```

### Custom HttpClient

Provide your own `HttpClient` when you need custom message handlers or certificate validation:

```csharp
using System.Net.Http;
using Utils.OData;

var httpClient = new HttpClient();  // managed externally — not disposed by QueryOData
using var client = new QueryOData("https://example.com/odata", httpClient);
```

### Credentials

```csharp
using System.Net;
using Utils.OData;

using var client = new QueryOData("https://example.com/odata");
client.Credentials = new NetworkCredential("user", "password");
```

## ODataContext examples

Derive from `ODataContext` to load EDMX metadata once and expose typed and untyped entity sets.

```csharp
using Utils.OData;
using Utils.OData.Linq;

public partial class NorthwindContext : ODataContext
{
    public NorthwindContext() : base("northwind.edmx") { }

    // Expose a typed entity set via the protected Query<T> method
    public ODataQueryable<Product> Products => Query<Product>("Products");

    // Expose an untyped entity set for dynamic access
    public ODataQueryable<ODataUntypedRow> RawCategories => Table("Categories");
}

public class Product
{
    public int    Id    { get; set; }
    public string Name  { get; set; }
    public decimal Price { get; set; }
}
```

### LINQ expression compiler

The LINQ provider translates expressions into OData query parameters. Use `CompileToODataQuery()` to obtain the compiled parameters and `ToUriString()` to build the URL fragment.

```csharp
using Utils.OData;
using Utils.OData.Linq;

var ctx = new NorthwindContext();

var query = ctx.Products
    .Where(p => p.Price > 10)
    .OrderBy(p => p.Name)
    .Take(20);

ODataQueryCompilation compiled = query.CompileToODataQuery();
// compiled.EntitySetName → "Products"
// compiled.Filters       → ["Price gt 10"]
Console.WriteLine(compiled.ToUriString());
// → Products?$filter=Price gt 10
```

### Expand navigation properties

Use `Expand(params string[])` to request eager loading of navigation properties:

```csharp
using Utils.OData;
using Utils.OData.Linq;

var ctx = new NorthwindContext();
var query = ctx.Products
    .Where(p => p.Price > 5)
    .Expand("Supplier", "Category");

ODataQueryCompilation compiled = query.CompileToODataQuery();
Console.WriteLine(compiled.ToUriString());
// → Products?$expand=Supplier,Category&$filter=Price gt 5
```

### Untyped row access

Use `Table(entitySetName)` when no CLR type maps to the entity. Access field values through `GetValue<T>` inside a LINQ expression:

```csharp
using Utils.OData;
using Utils.OData.Linq;

var ctx = new NorthwindContext();
var query = ctx.RawCategories
    .Where(r => r.GetValue<string>("Country") == "France");

ODataQueryCompilation compiled = query.CompileToODataQuery();
```

## Related packages
- `omy.Utils.OData.Generators` – source generator for strongly typed OData contexts.
- `omy.Utils` – foundational helpers shared across the family.
