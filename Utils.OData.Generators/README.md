# omy.Utils.OData.Generators

`omy.Utils.OData.Generators` is a Roslyn source generator that reads EDMX metadata at build time and emits strongly typed entity classes nested inside your `ODataContext` derivative.

## Install
```bash
dotnet add package omy.Utils.OData.Generators
```

## Supported frameworks
- netstandard2.0 (analyzer)

## Features

- Locates `partial` classes inheriting from `ODataContext` and inspects their base constructor call.
- Loads EDMX definitions from a relative file path or an HTTP/HTTPS endpoint at build time.
- Generates nested entity classes with nullable properties, XML documentation, and `[Key]` attributes for primary-key columns.
- Reports diagnostics when the metadata cannot be found, downloaded, or deserialized (`ODATA001`–`ODATA003`).

## Prerequisites

1. Reference `omy.Utils.OData` as a library dependency.
2. Add `omy.Utils.OData.Generators` as an analyzer reference so MSBuild executes the generator.
3. Declare your context as a `partial` class and pass the EDMX path or URL to the base constructor.

## Usage example

```csharp
using Utils.OData;
using Utils.OData.Linq;

// The generator reads Products.edmx and emits ProductContext.Product
public partial class ProductContext : ODataContext
{
    public ProductContext() : base("metadata/Products.edmx") { }

    // Expose typed sets using the protected Query<T> helper
    public ODataQueryable<Product> Products => Query<Product>("Products");
}
```

After build, `ProductContext.Product` is available as a generated nested partial class. Compose queries using LINQ and compile them to OData parameters:

```csharp
using Utils.OData.Linq;

var ctx = new ProductContext();
var query = ctx.Products
    .Where(p => p.Price > 5)
    .OrderBy(p => p.Name);

ODataQueryCompilation compiled = query.CompileToODataQuery();
Console.WriteLine(compiled.ToUriString());
// → Products?$filter=Price gt 5
```

## Examples

### Extending generated entities

Generated entity classes are `partial`, so you can add domain logic alongside the generated properties:

```csharp
public partial class ProductContext
{
    public partial class Product
    {
        public bool IsLowStock => UnitsInStock < 5;
    }
}
```

### Consuming remote metadata

Point the base constructor to a remote EDMX endpoint; the generator downloads and caches the metadata at build time:

```csharp
public partial class RemoteContext : ODataContext
{
    public RemoteContext() : base("https://example.com/$metadata") { }

    public ODataQueryable<Order> Orders => Query<Order>("Orders");
}
```

When network retrieval fails, diagnostic `ODATA002` is raised. Malformed payloads trigger `ODATA003`, surfacing problems at compile time rather than at runtime.

### Multiple contexts in one assembly

Each `partial` class referencing a different EDMX produces an isolated set of entity types:

```csharp
public partial class SalesContext : ODataContext
{
    public SalesContext() : base("metadata/Sales.edmx") { }

    public ODataQueryable<Invoice> Invoices => Query<Invoice>("Invoices");
}

public partial class SupportContext : ODataContext
{
    public SupportContext() : base("metadata/Support.edmx") { }

    public ODataQueryable<Ticket> Tickets => Query<Ticket>("Tickets");
}
```

### Expand navigation properties

Use `Expand(params string[])` to request eager loading of related entities:

```csharp
using Utils.OData.Linq;

var ctx = new ProductContext();
var query = ctx.Products
    .Where(p => p.Price > 5)
    .Expand("Supplier", "Category");

ODataQueryCompilation compiled = query.CompileToODataQuery();
Console.WriteLine(compiled.ToUriString());
// → Products?$expand=Supplier,Category&$filter=Price gt 5
```

### Diagnostics reference

| Id       | Cause                                                         | Resolution                                                    |
|----------|---------------------------------------------------------------|---------------------------------------------------------------|
| ODATA001 | Derived context is not declared `partial`.                    | Add the `partial` modifier to the class declaration.          |
| ODATA002 | Base constructor argument could not be resolved at build time.| Use a string literal for the EDMX path or URL.                |
| ODATA003 | EDMX file or URL could not be loaded or parsed.               | Check the path, network connectivity, and document structure. |

## Related packages
- `omy.Utils.OData` – runtime client and LINQ provider.
- `omy.Utils` – shared helpers used by the OData components.
