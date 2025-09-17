# Utils.XML Library

The **Utils.XML** package groups the XML helpers extracted from the base Utils assembly.
It targets **.NET 8** and provides infrastructure to process XML documents using
attribute-driven dispatch as well as helper methods for navigating `XmlReader` instances.

## Features

- Attribute-based XML processing pipeline via `XmlDataProcessor`
- Class-level namespace registration through `XmlNamespaceAttribute`
- Declarative XPath triggers with the `MatchAttribute`
- `XmlReader` extensions to iterate over child elements and compute XPaths

## Usage example

```csharp
using System.IO;
using System.Xml;
using Utils.XML;

class ItemProcessor : XmlDataProcessor
{
    [Match("/items/item")]
    private void HandleItem()
    {
        string id = ValueOf("@id");
        Console.WriteLine($"Encountered item {id}");
    }

    protected override void Root()
    {
        Apply("/items/item");
    }
}

using XmlReader reader = XmlReader.Create(new StringReader("<items><item id=\"A\" /></items>"));
var processor = new ItemProcessor();
processor.Read(reader);
```
