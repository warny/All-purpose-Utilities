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
string text = converter.Convert(12345);
Console.WriteLine(text);
```

### Use a specific culture

```csharp
using Utils.Mathematics;

NumberToStringConverter english = NumberToStringConverter.GetConverter("EN");
string value = english.Convert(21001);
Console.WriteLine(value);
```

### Convert decimal and fractional values

```csharp
using Utils.Mathematics;
using Utils.Numerics;

NumberToStringConverter french = NumberToStringConverter.GetConverter("FR-fr");

string decimalText = french.Convert(12.34m);
string fractionText = french.Convert(new Number(3, 2));

Console.WriteLine(decimalText);
Console.WriteLine(fractionText);
```

### Use language-specific finalization

```csharp
using Utils.Mathematics;

NumberToStringConverter german = NumberToStringConverter.GetConverter("DE");
string million = german.Convert(1_000_000);
Console.WriteLine(million); // "eine Million" with German specifics
```

## Notes

- Targets stable frameworks for package consumers.
- Versioning follows the `omy.Utils` package family.
- For broader utility features, see the root package: `omy.Utils`.
