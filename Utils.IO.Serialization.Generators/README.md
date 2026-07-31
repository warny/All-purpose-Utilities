# omy.Utils.IO.Serialization.Generators

`omy.Utils.IO.Serialization.Generators` is a Roslyn source generator that produces strongly typed, collision-free reader and writer extension methods for DTOs annotated with `[GenerateReaderWriter]`.

## Install
```bash
dotnet add package omy.Utils.IO.Serialization.Generators
```

## Supported frameworks
- netstandard2.0 (analyzer)

## Features

- Discovers classes, structs, and records decorated with `[GenerateReaderWriter]`.
- Inspects members tagged with `[Field(order)]` to define binary wire ordering.
- Emits uniquely named `Read<type-identity>_<hash>(this IReader)` and `Write<type-identity>_<hash>(this IWriter, T value)` extension methods.
- Automatically reuses custom serializers when a member type already exposes matching `Read{Member}` / `Write{Member}` extensions.
- Generates XML-documented, editor-friendly code.

## Preparing a model

Annotate each serializable field or property with `[Field(order)]`. The generator processes members in ascending order:

```csharp
using Utils.IO.Serialization;

[GenerateReaderWriter]
public partial class InventoryEntry
{
    [Field(0)]
    public required int Id { get; set; }

    [Field(1)]
    public required string Name { get; set; }

    [Field(2)]
    public PriceTag Price { get; set; } = new();
}

[GenerateReaderWriter]
public partial class PriceTag
{
    [Field(0)]
    public decimal Amount { get; set; }

    [Field(1)]
    public string Currency { get; set; } = "EUR";
}
```

## Usage example

```csharp
using System.IO;
using Utils.IO.Serialization;

var buffer = new MemoryStream();
var writer = new Writer(buffer);
var entry  = new InventoryEntry { Id = 7, Name = "Sprocket", Price = new PriceTag { Amount = 19.95m, Currency = "USD" } };
writer.WriteInventoryEntry_db945a29(entry);

buffer.Position = 0;
var reader       = new Reader(buffer);
InventoryEntry   roundTrip = reader.ReadInventoryEntry_db945a29();
```

`Writer` and `Reader` are the concrete `IWriter`/`IReader` implementations from `omy.Utils.IO`.

## Additional scenarios

### Nested models

When a member type is also annotated with `[GenerateReaderWriter]`, the generator nests calls to the generated serializer automatically:

```csharp
[GenerateReaderWriter]
public partial class Shipment
{
    [Field(0)]
    public required int TrackingId { get; set; }

    [Field(1)]
    public required InventoryEntry Item { get; set; }
}
```

`WriteShipment_126422e3` calls `WriteInventoryEntry_db945a29` internally; `ReadShipment_126422e3` calls `ReadInventoryEntry_db945a29`.

### Reusing manual serializers

If a member type already exposes hand-written `Write{Member}` / `Read{Member}` extensions, the generator defers to them:

```csharp
using Utils.IO.Serialization;

public static class MoneySerializer
{
    public static void WritePriceTag(this IWriter writer, PriceTag value)
    {
        writer.Write<decimal>(value.Amount);
        writer.Write<string>(value.Currency);
    }

    public static PriceTag ReadPriceTag(this IReader reader)
        => new() { Amount = reader.Read<decimal>(), Currency = reader.Read<string>() };
}

[GenerateReaderWriter]
public partial class Invoice
{
    [Field(0)]
    public required int Number { get; set; }

    [Field(1)]
    public required PriceTag Total { get; set; }
}
```

Because `WritePriceTag` already exists, the generated `WriteInvoice` calls it instead of emitting a generic `Write<PriceTag>`.

### Streaming large collections

Generated extensions are regular methods that work with any `Stream`-backed `Writer`/`Reader`:

```csharp
using System.IO;
using Utils.IO.Serialization;

await using var stream = File.OpenWrite("report.bin");
var writer = new Writer(stream);

foreach (InventoryEntry item in inventory)
    writer.WriteInventoryEntry_db945a29(item);
```

## Related packages
- `omy.Utils.IO` – provides `IReader`, `IWriter`, `Reader`, `Writer`, `[GenerateReaderWriter]`, and `[Field]`.
- `omy.Utils` – shared helpers.


[Versioned API documentation](https://warny.github.io/All-purpose-Utilities/v2.0.0-rc.1/)

## Version 2 generated method names

Version 2 uses the complete metadata identity for every generated class, hint name, reader, and writer. A method follows `Read<sanitized-full-identity>_<stable-hash>` / `Write<sanitized-full-identity>_<stable-hash>` rather than `Read<TypeName>` / `Write<TypeName>`. The readable prefix contains the namespace and containing types; the deterministic FNV-1a suffix prevents the remaining escaped-name collisions. This is an intentional 2.0 source-breaking change—use the generated member offered by IntelliSense rather than hard-coding a simple-name convention.

Contracts containing init-only properties are rejected with `UIOSG010`; the generator emits no source for that type, so consumer compilations do not receive a secondary assignment error from a `.g.cs` file.
