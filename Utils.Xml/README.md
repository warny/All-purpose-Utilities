# omy.Utils.Xml (XML helpers)

`omy.Utils.Xml` offers attribute-driven XML processing helpers to bind XML documents to strongly typed objects.

## Install
```bash
dotnet add package omy.Utils.Xml
```

## Supported frameworks
- net8.0

## Features
- Attribute-based mapping between XML elements/attributes and CLR properties.
- `XmlDataProcessor` helpers to read and write documents.
- Validation support while converting XML content into objects.

## Quick usage
```csharp
using Utils.Xml;

public class Sample
{
    [XmlElement("name")] public string? Name { get; set; }
}

var processor = new XmlDataProcessor();
Sample value = processor.Deserialize<Sample>("<root><name>demo</name></root>");
```

## Related packages
- `omy.Utils` – shared helpers used by the XML utilities.
