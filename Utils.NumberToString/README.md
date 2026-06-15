# omy.Utils.NumberToString

`omy.Utils.NumberToString` provides pluggable language-specific finalization hooks consumed by `NumberToStringConverter` in `omy.Utils`.

## Install

```bash
dotnet add package omy.Utils.NumberToString
```

## Supported frameworks
- net8.0

## Features
- `INumberToStringLanguageSpecifics` — interface for post-processing converted number text.
- `DefaultNumberToStringLanguageSpecifics` — no-op implementation; returns the text unchanged.
- `GermanNumberToStringLanguageSpecifics` — applies German grammar rules (e.g. `"eine Million"` instead of `"ein Million"`).

## Using language specifics with NumberToStringConverter

Language specifics are injected into `NumberToStringConverter` (from `omy.Utils`) to apply locale-aware text adjustments after number conversion. The built-in culture configurations in `omy.Utils` already wire them automatically:

```csharp
using Utils.Mathematics;

// The "DE" converter uses GermanNumberToStringLanguageSpecifics internally
NumberToStringConverter german = NumberToStringConverter.GetConverter("DE");
string text = german.Convert(1_000_000);
Console.WriteLine(text); // "eine Million"
```

## Implementing a custom finalization

Implement `INumberToStringLanguageSpecifics` to apply your own post-processing step:

```csharp
using Utils.Mathematics;

public class UpperCaseSpecifics : INumberToStringLanguageSpecifics
{
    public string FinalizeWriting(string languageIdentifier, string text)
        => text.ToUpperInvariant();
}
```

Pass an instance to the `NumberToStringConverter` constructor (declared in `omy.Utils`) via the `languageSpecifics` parameter.

## DefaultNumberToStringLanguageSpecifics

Used automatically when no language specifics are provided — it returns the input text unchanged:

```csharp
using Utils.Mathematics;

var specifics = new DefaultNumberToStringLanguageSpecifics();
string result = specifics.FinalizeWriting("EN", "twenty-one");
// → "twenty-one" (no change)
```

## Related packages
- `omy.Utils` — contains `NumberToStringConverter` and all built-in culture configurations.
