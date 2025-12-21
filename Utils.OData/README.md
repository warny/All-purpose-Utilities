# omy.Utils.OData (OData client helpers)

`omy.Utils.OData` delivers HTTP client helpers, a LINQ provider, and metadata utilities to simplify consuming OData services.

## Install
```bash
dotnet add package omy.Utils.OData
```

## Supported frameworks
- net9.0

## Features
- `QueryOData` HTTP client with cookie forwarding, credential support, and automatic paging.
- `ODataQueryBuilder` for constructing compliant query strings from an `IQuery` description.
- `ODataContext` base class that loads EDMX metadata and exposes typed/untyped queryables.
- Metadata converters that map raw responses to CLR values.

## Quick usage
```csharp
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

## Related packages
- `omy.Utils.OData.Generators` – source generator for strongly typed contexts.
- `omy.Utils` – foundational helpers shared across the family.
