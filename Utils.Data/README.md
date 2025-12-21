# omy.Utils.Data (data mappers)

`omy.Utils.Data` offers attributes and helpers to map `IDataRecord`/`IDataReader` rows into typed objects without heavy ORMs.

## Install
```bash
dotnet add package omy.Utils.Data
```

## Supported frameworks
- net8.0

## Features
- Field attributes to describe mappings and conversions.
- Builders to interpolate SQL queries safely.
- Helpers to populate objects from `IDataRecord`/`IDataReader` instances.

## Quick usage
```csharp
using Utils.Data;

public class User
{
    [Field("id")] public int Id { get; set; }
    [Field("display_name")] public string? Name { get; set; }
}

IDataRecord record = /* read from a data reader */;
User user = record.Fill<User>();
```

## Related packages
- `omy.Utils` – shared helpers the data utilities rely on.
- `omy.Utils.IO` – for binary/stream serialization scenarios.
