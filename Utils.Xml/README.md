# omy.Utils.Xml (XML helpers)

`omy.Utils.Xml` provides an XPath-driven XML processing framework and streaming XML reader helpers.

## Install
```bash
dotnet add package omy.Utils.Xml
```

## Supported frameworks
- net8.0

## Features
- `XmlDataProcessor` — abstract base class; derive it and annotate methods with `[Match(xPath)]` to dispatch XML nodes automatically.
- `[Match(xPath)]` — triggers a method when a node matches the given XPath expression.
- `[XmlNamespace(prefix, uri)]` — registers namespace prefixes for XPath expressions at the class level.
- `Apply(xPath)` — selects and dispatches child nodes from within a handler.
- `ValueOf(xPath?)` — reads the text value of a node.
- `ReadSecure(uri)` — processes untrusted XML with hardened parser settings (no DTD, limited entity expansion).
- `XmlUtils.ReadChildElements` — streams immediate child elements from an `XmlReader`.
- `XmlUtils.GetXPath` — computes the XPath address of an `XmlElement`.

## Quick usage

```csharp
using System;
using Utils.XML;

public class BookProcessor : XmlDataProcessor
{
    protected override void Root()
    {
        // Dispatch all <book> elements under /library
        Apply("//book");
    }

    [Match("book")]
    protected void OnBook()
    {
        Console.WriteLine($"Title: {ValueOf("title")}, Year: {ValueOf("year")}");
    }
}

var processor = new BookProcessor();
processor.Read(File.OpenRead("library.xml"));
```

## XmlDataProcessor examples

### Namespaces

```csharp
using Utils.XML;

[XmlNamespace("ns", "http://example.com/schema")]
public class CatalogProcessor : XmlDataProcessor
{
    protected override void Root()
    {
        Apply("//ns:item");
    }

    [Match("ns:item")]
    protected void OnItem()
    {
        string id   = ValueOf("@id");
        string name = ValueOf("ns:name");
        Console.WriteLine($"{id}: {name}");
    }
}
```

### Multiple Match patterns on one method

```csharp
using Utils.XML;

public class InvoiceProcessor : XmlDataProcessor
{
    protected override void Root() => Apply("//invoice | //credit-note");

    [Match("invoice")]
    [Match("credit-note")]
    protected void OnDocument()
    {
        Console.WriteLine($"Document type: {Current.Name}, ref: {ValueOf("@ref")}");
    }
}
```

### Passing parameters through Apply

```csharp
using System.Collections.Generic;
using Utils.XML;

public class OrderProcessor : XmlDataProcessor
{
    protected override void Root()
    {
        var items = new List<string>();
        Apply("//order/item", items);
        Console.WriteLine($"Collected {items.Count} items");
    }

    [Match("item")]
    protected void OnItem(List<string> items)
    {
        items.Add(ValueOf("@sku"));
    }
}
```

### Reading from different sources

```csharp
using System.IO;
using System.Xml;
using Utils.XML;

var proc = new MyProcessor();

// From a file path (trusted)
#pragma warning disable CS0618
proc.Read("data.xml");
#pragma warning restore CS0618

// From a Stream
proc.Read(File.OpenRead("data.xml"));

// From an XmlReader
using var reader = XmlReader.Create("data.xml");
proc.Read(reader);

// Hardened read from an untrusted URI (DTD disabled, entity expansion limited)
proc.ReadSecure("https://example.com/feed.xml");
```

### GetNodes — iterate without dispatching

```csharp
using System.Xml.XPath;
using Utils.XML;

public class SummaryProcessor : XmlDataProcessor
{
    protected override void Root()
    {
        XPathNodeIterator rows = GetNodes("//row");
        int count = 0;
        while (rows.MoveNext()) count++;
        Console.WriteLine($"Row count: {count}");
    }
}
```

## XmlUtils examples

### ReadChildElements — stream child elements from XmlReader

```csharp
using System.Xml;
using Utils.XML;

using var reader = XmlReader.Create("products.xml");

// Advance to the <products> element
reader.ReadToFollowing("products");

// Iterate all immediate child elements
foreach (XmlReader child in reader.ReadChildElements())
{
    Console.WriteLine($"<{child.Name}> id={child.GetAttribute("id")}");
}

// Iterate only <product> children
reader.ReadToFollowing("products");
foreach (XmlReader child in reader.ReadChildElements("product"))
{
    Console.WriteLine(child.GetAttribute("sku"));
}
```

### GetXPath — resolve the XPath address of an XmlElement

```csharp
using System.Xml;
using Utils.XML;

var doc = new XmlDocument();
doc.LoadXml("<root><items><item/><item/></items></root>");

XmlElement second = (XmlElement)doc.SelectSingleNode("//item[2]");
Console.WriteLine(second.GetXPath());
// → /root/items/item[2]
```

## Related packages
- `omy.Utils` – shared helpers used by the XML utilities.
