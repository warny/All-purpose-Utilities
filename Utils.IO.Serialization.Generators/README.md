# Utils.IO.Serialization.Generators

`Utils.IO.Serialization.Generators` ships a Roslyn source generator that produces strongly typed reader and writer extension methods for DTOs annotated with `GenerateReaderWriterAttribute`. It targets **.NET 9** and extends the `Utils.IO.Serialization` primitives by removing repetitive serialization boilerplate.

## Features

- Discovers classes, structs, and records decorated with `[GenerateReaderWriter]`.
- Inspects members tagged with `[Field(order)]` to preserve wire ordering.
- Emits `Read{Type}` and `Write{Type}` extension methods for `IReader` and `IWriter`.
- Reuses custom serializers automatically when a member type already exposes matching extensions.
- Generates partial, editor-friendly code with XML documentation.

## Preparing a model

Annotate each serializable field or property with the `FieldAttribute` specifying the binary order. The generator supports nested models and nullable members.

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
    public PriceTag Price = new();
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
using Utils.IO.Serialization.Generators;

var buffer = new MemoryStream();
IWriter writer = new BinaryWriterAdapter(new BinaryWriter(buffer));
IReader reader = new BinaryReaderAdapter(new BinaryReader(buffer));

var entry = new InventoryEntry { Id = 7, Name = "Sprocket", Price = new PriceTag { Amount = 19.95m, Currency = "USD" } };
writer.WriteInventoryEntry(entry);

buffer.Position = 0;
InventoryEntry roundTrip = reader.ReadInventoryEntry();
```

The generated extensions serialize the members in declaration order, nesting calls to generated serializers for `PriceTag`. When a member already exposes custom `Read{Member}` or `Write{Member}` extensions, the generator invokes them instead of emitting generic calls.

## Additional scenarios

### Nested collections and dictionaries

Complex object graphs composed of collections are supported out of the box.

```csharp
[GenerateReaderWriter]
public partial class Warehouse
{
    [Field(0)]
    public List<InventoryEntry> Items { get; set; } = new();

    [Field(1)]
    public Dictionary<string, PriceTag> RegionalPrices { get; set; } = new();
}

var warehouse = new Warehouse
{
    Items =
    [
        new InventoryEntry { Id = 1, Name = "Widget", Price = new PriceTag { Amount = 9.99m } },
        new InventoryEntry { Id = 2, Name = "Gadget", Price = new PriceTag { Amount = 14.5m } },
    ],
};
warehouse.RegionalPrices["EU"] = new PriceTag { Amount = 8.99m, Currency = "EUR" };

writer.WriteWarehouse(warehouse);
```

Enumerables are serialized using their existing element serializers, so nested graphs stay
consistent with individual entity serialization logic.

### Version tolerant models

When new fields are appended with higher `Field` numbers the generator keeps backward
compatibility by making them optional.

```csharp
[GenerateReaderWriter]
public partial class InventoryEntry
{
    [Field(0)]
    public required int Id { get; set; }

    [Field(1)]
    public required string Name { get; set; }

    [Field(2)]
    public PriceTag Price = new();

    [Field(3)]
    public string? Description { get; set; }
}
```

Older readers that only know the first three fields still deserialize correctly because the
generated code checks the buffer length before attempting to access new fields.

### Working with spans and pooled buffers

The generated extensions are regular methods that accept any `IWriter`/`IReader` implementation
so they integrate with high-performance pipelines.

```csharp
using System.Buffers;
using Utils.IO.Serialization;

ArrayPool<byte> pool = ArrayPool<byte>.Shared;
byte[] rented = pool.Rent(4096);

try
{
    var writer = new SpanWriterAdapter(rented);
    writer.WriteInventoryEntry(entry);

    var reader = new SpanReaderAdapter(writer.WrittenSpan);
    InventoryEntry clone = reader.ReadInventoryEntry();
}
finally
{
    pool.Return(rented);
}
```

Adapters such as `SpanWriterAdapter` and `SpanReaderAdapter` allow serialization without heap
allocations which is ideal for messaging systems.

## Advanced scenario: reusing manual serializers

You can mix generated serializers with hand-written ones to handle special data formats.

```csharp
using Utils.IO.Serialization;

public static class MoneySerializer
{
    public static void WritePriceTag(this IWriter writer, PriceTag value)
    {
        writer.WriteDecimal(value.Amount);
        writer.WriteString(value.Currency);
    }

    public static PriceTag ReadPriceTag(this IReader reader)
    {
        return new PriceTag
        {
            Amount = reader.ReadDecimal(),
            Currency = reader.ReadString(),
        };
    }
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

Because `MoneySerializer` already exposes `WritePriceTag` and `ReadPriceTag`, the generator defers to them when producing the `WriteInvoice` and `ReadInvoice` implementations, ensuring bespoke formatting is preserved.

## Streaming large collections

Generated extensions work nicely with streaming scenarios by combining them with buffered adapters.

```csharp
using System.IO;
using Utils.IO.Serialization;

await using var stream = File.OpenWrite("report.bin");
await using var writer = new BinaryWriterAdapter(new BinaryWriter(stream));

foreach (InventoryEntry item in inventory)
{
    writer.WriteInventoryEntry(item);
}
```

Since the generator only emits member-level serialization logic, you can control buffering and chunk sizes directly on the `Stream` or adapter used during serialization.

### Hybrid streaming with asynchronous writers

The same generated extensions can be paired with asynchronous `PipeWriter` implementations for
network streaming.

```csharp
using System.IO.Pipelines;
using Utils.IO.Serialization;

Pipe pipe = new();
PipeWriter pipeWriter = pipe.Writer;

await using var writer = new PipeWriterAdapter(pipeWriter);
foreach (InventoryEntry item in inventory)
{
    writer.WriteInventoryEntry(item);
    await writer.FlushAsync();
}
```

Because flushing is under the caller's control you can batch messages, append framing headers,
or interleave generated payloads with custom metadata before sending them over the network.
