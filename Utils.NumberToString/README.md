# omy.Utils.NumberToString

`omy.Utils.NumberToString` provides localized number-to-text conversion APIs extracted from the broader `omy.Utils` library.

## Install

```bash
dotnet add package omy.Utils.NumberToString
```

## Usage

```csharp
using Utils.Mathematics;

NumberToStringConverter converter = NumberToStringConverter.Default;
string text = converter.ToString(12345);
Console.WriteLine(text);
```

## Notes

- Targets stable frameworks for package consumers.
- Versioning follows the `omy.Utils` package family.
- For broader utility features, see the root package: `omy.Utils`.
