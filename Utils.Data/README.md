# Utils.Data Library

**Utils.Data** offers simple data mapping facilities targeting **.NET 9**.
It lets you populate objects from `IDataRecord` or `IDataReader` instances using attributes to describe the relationship between fields and properties.

## Features

- Attributes to map database columns to object properties
- Extension methods to fill objects directly from data readers
- Support for custom converters through interfaces
- Focus on separating mapping metadata from actual processing

## Usage example

```csharp
using Utils.Data;
using System.Data;

class Person
{
    [Field("name")]
    public string Name { get; set; } = "";
}

IDataRecord record = /* retrieved from a data reader */;
Person person = record.ToObject<Person>();
```
