# Utils.OData.Generators

`Utils.OData.Generators` offers a Roslyn source generator that materializes entity classes and helpers for contexts derived from `Utils.OData.ODataContext`. It targets **.NET 9** and consumes EDMX metadata so OData payloads can be accessed through strongly typed models.

## Features

- Locates partial classes inheriting from `ODataContext` and inspects their base constructor calls.
- Loads EDMX definitions from relative paths or HTTP/HTTPS endpoints at build time.
- Generates nested entity classes with nullable support, XML documentation, and `[Key]` attributes for primary keys.
- Reports diagnostics when metadata cannot be found, downloaded, or deserialized (`ODATA001`–`ODATA003`).
- Produces deterministic source files that play nicely with incremental builds and IDE navigation.

## Prerequisites

1. Reference `Utils.OData` in your project.
2. Add `Utils.OData.Generators` as an analyzer reference so MSBuild executes the generator.
3. Create a partial context that calls an `ODataContext` base constructor with the EDMX location.

## Usage example

```csharp
using Utils.OData;

public partial class ProductContext : ODataContext
{
    public ProductContext() : base("metadata/Products.edmx") { }
}
```

When the project is built, the generator reads `metadata/Products.edmx`, produces partial classes such as `ProductContext.Product`, and populates each property with the correct .NET type. The generated members can then be consumed through the LINQ provider:

```csharp
var context = new ProductContext();
var query = context.Products.Where(p => p.Price > 5).OrderBy(p => p.Name);
```

If the EDMX path is wrong or the file cannot be parsed, the generator raises diagnostics so the issue is caught during compilation instead of at runtime.

## Examples

### Extending generated entities

Entities are emitted as partial classes, allowing domain-specific behavior to be added alongside the generated members.

```csharp
public partial class ProductContext
{
    public partial class Product
    {
        public bool IsLowStock => UnitsInStock < 5;
    }
}
```

Because `Product` is a partial class, additional properties and helper methods seamlessly coexist with generated properties while keeping custom logic separate from tooling output.

### Consuming remote metadata

You can point the base constructor to a remote EDMX URL; the generator downloads and caches the metadata during build.

```csharp
public partial class RemoteContext : ODataContext
{
    public RemoteContext() : base("https://example.com/$metadata") { }
}
```

When network retrieval fails, diagnostic `ODATA002` highlights the HTTP issue, while malformed payloads trigger `ODATA003`, making problems explicit to developers before deployment.

### Handling multiple models

Large solutions often require multiple contexts. Simply declare additional partial classes, each referencing a different EDMX location, and the generator will emit isolated entity hierarchies per context.

```csharp
public partial class SalesContext : ODataContext
{
    public SalesContext() : base("metadata/Sales.edmx") { }
}

public partial class SupportContext : ODataContext
{
    public SupportContext() : base("metadata/Support.edmx") { }
}
```

Each context receives its own `ODataQueryable<T>` properties and entity types, ensuring models stay isolated even when they share the same assembly.

### Navigation properties and expansions

Generated entities include navigation properties mapped from the EDMX metadata.

```csharp
using System.Linq;
using Utils.OData;

public partial class ProductContext : ODataContext
{
    public ProductContext() : base("metadata/Products.edmx") { }
}

var context = new ProductContext();
var query = context.Products
    .Where(p => p.Supplier.City == "Paris")
    .Expand(p => p.Supplier)
    .Select(p => new { p.Name, Supplier = p.Supplier.Name });
```

The LINQ provider recognizes the generated navigation properties and emits `$expand` segments
automatically when needed.

### Partial classes for validation

Because entities are partial classes, domain validation rules can live alongside generated
members without being lost during rebuilds.

```csharp
using System;

public partial class ProductContext
{
    public partial class Product
    {
        partial void OnNameChanging(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A product requires a non-empty name.");
            }
        }
    }
}
```

The generator respects existing partial methods and only emits missing declarations, so custom
validation hooks run whenever the entity property changes.

### Diagnostics reference

Diagnostics surfaced by the generator can be acted upon quickly during development:

| Id        | Message                                                            | Resolution hint                                         |
|-----------|--------------------------------------------------------------------|---------------------------------------------------------|
| ODATA001  | `ODataContext` constructor did not specify a metadata source.      | Ensure the base call references a local file or URL.    |
| ODATA002  | Metadata file or endpoint could not be reached.                    | Verify network connectivity and credentials.            |
| ODATA003  | Metadata content could not be parsed into EDMX structures.         | Validate that the EDMX document is well-formed XML.     |
| ODATA004  | Duplicate entity names detected across merged EDMX documents.      | Rename the entities or split them into distinct models. |

Keeping this table handy speeds up troubleshooting when metadata moves or services change.
