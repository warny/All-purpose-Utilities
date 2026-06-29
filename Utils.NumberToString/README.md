# omy.Utils.NumberToString

Number-to-string conversion for multiple languages and cultures, with support for ordinals, morphological variants (gender, case…), and currency amounts.

## Install

```bash
dotnet add package omy.Utils.NumberToString
```

## Supported frameworks

- net8.0

## Supported cultures

| Code | Language | Ordinals | Variants |
|------|----------|----------|---------|
| EN, EN-uk, EN-us | English | ✓ | — (numbers are invariable) |
| FR, FR-fr, FR-ca | French | ✓ | gender (masculin/feminin) |
| FR-be, FR-ch | Belgian/Swiss French | ✓ | gender (masculin/feminin) |
| DE | German | — | genus (maskulin/feminin/neutrum) × kasus (nominativ/akkusativ/dativ/genitiv) |
| ES | Spanish | — | gender (masculino/femenino) |
| IT | Italian | — | gender (maschile/femminile) |
| PT | Portuguese | — | gender (masculino/feminino) |
| PL | Polish | — | — (not yet implemented) |
| NL | Dutch | ✓ | — (numbers are invariable) |
| RU | Russian | — | — (cases not yet implemented) |
| AR | Arabic | — | — (not yet implemented) |
| HE | Hebrew | — | gender (standalone/zachar/nekeva) |
| ZH | Chinese | — | — (no inflection) |
| JA | Japanese | — | — (no inflection) |
| KO | Korean | — | — (numbers are invariable) |
| HI | Hindi | — | — (not yet implemented) |
| EL | Greek | — | — (not yet implemented) |
| FI | Finnish | — | sijamuoto (nominatiivi/partitiivi/genetiivi) |
| CA | Catalan | — | gender (masculí/femení) |
| EU | Basque | ✓ | — (no grammatical gender) |
| GL | Galician | — | gender (masculino/feminino) |
| ZU | Zulu | — | — (not yet implemented) |
| EE | Ewe | — | — (not yet implemented) |
| WO | Wolof | — | — (not yet implemented) |

---

## Basic conversion

```csharp
using Utils.Mathematics;

NumberToStringConverter en = NumberToStringConverter.GetConverter("EN");
NumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");
NumberToStringConverter de = NumberToStringConverter.GetConverter("DE");

en.Convert(42);         // "forty-two"
en.Convert(-7);         // "minus seven"
en.Convert(1_000_000);  // "one million"

fr.Convert(21);         // "vingt et un"
fr.Convert(1_000_000);  // "un million"

de.Convert(1);          // "eins"
de.Convert(1_000_000);  // "eine Million"   ← GermanNumberToStringLanguageSpecifics
```

`GetConverter` falls back to the language code when a region variant is not found, then to `"EN"` as final default.

---

## Ordinals

```csharp
NumberToStringConverter en = NumberToStringConverter.GetConverter("EN");

en.ConvertOrdinal(1);    // "first"
en.ConvertOrdinal(2);    // "second"
en.ConvertOrdinal(3);    // "third"
en.ConvertOrdinal(21);   // "twenty-first"
en.ConvertOrdinal(100);  // "one hundredth"
en.ConvertOrdinal(-5);   // "minus fifth"
```

```csharp
NumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");

fr.ConvertOrdinal(1);    // "premier"       ← whole-number exception
fr.ConvertOrdinal(2);    // "deuxième"
fr.ConvertOrdinal(5);    // "cinquième"     ← word rule: cinq → cinquième
fr.ConvertOrdinal(9);    // "neuvième"      ← word rule: neuf → neuvième
fr.ConvertOrdinal(21);   // "vingt et unième"
fr.ConvertOrdinal(1000); // "millième"      ← removeTrailing="e" + suffix ième
```

```csharp
NumberToStringConverter frBe = NumberToStringConverter.GetConverter("FR-be");

frBe.ConvertOrdinal(1);   // "premier"           ← exception
frBe.ConvertOrdinal(71);  // "septante et unième" ← Belgian 70 + word rule for "un"
frBe.ConvertOrdinal(80);  // "huitantième"        ← Belgian 80 + removeTrailing="e"
frBe.ConvertOrdinal(90);  // "nonantième"
```

```csharp
NumberToStringConverter nl = NumberToStringConverter.GetConverter("NL");

nl.ConvertOrdinal(1);   // "eerste"           ← exception
nl.ConvertOrdinal(2);   // "tweede"           ← word rule
nl.ConvertOrdinal(8);   // "achtste"          ← suffix "ste"
nl.ConvertOrdinal(11);  // "elfde"            ← word rule (exception 11=elf)
nl.ConvertOrdinal(20);  // "twintigste"       ← suffix "ste"
nl.ConvertOrdinal(21);  // "eenentwintigste"  ← fused compound + suffix "ste"
nl.ConvertOrdinal(101); // "honderd eerste"   ← word rule for "een"
```

```csharp
NumberToStringConverter eu = NumberToStringConverter.GetConverter("EU");

eu.ConvertOrdinal(1);    // "lehenengo"           ← irregular first
eu.ConvertOrdinal(2);    // "bigarren"            ← suffix "garren"
eu.ConvertOrdinal(10);   // "hamargarren"
eu.ConvertOrdinal(11);   // "hamaikagarren"       ← exception 11=hamaika + suffix
eu.ConvertOrdinal(21);   // "hogeita batgarren"   ← "bat" in compound gets suffix
```

> **Ordinal pipeline**: word-level rules are matched against the raw cardinal text, before
> `AdjustFunction` and `INumberToStringLanguageSpecifics.FinalizeWriting` are applied.
> `AdjustFunction` (and `FinalizeWriting`) then run on the ordinal result. This means a
> converter with an uppercase `AdjustFunction` correctly produces `"TWENTY-FIRST"`, not
> `"TWENTY-ONEth"`.

> **Languages without ordinals**: DE, IT, ES, PT, CA, GL, FI, HE, RU, PL, ZH, JA, KO, AR,
> HI, EL, ZU, EE, WO. German and Italian are technically feasible but their ordinals require
> complex agreement (gender × case) or — for German — fused compounds that cannot be handled
> with a single suffix in the current XML model.

---

## Morphological variants

Many languages inflect numbers for gender or grammatical case. Variants are declared per language in the XML configuration as named dimensions with ordered values. **The first declared value is the default** — calling `Convert` without parameters automatically uses it.

### French — grammatical gender

French has one variant dimension: **gender** (`masculin` / `feminin`).

```csharp
NumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");

// No parameter → masculine (first value = default)
fr.Convert(1);   // "un"
fr.Convert(21);  // "vingt et un"

// Explicit feminine
fr.Convert(1,  "gender=feminin");   // "une"
fr.Convert(21, "gender=feminin");   // "vingt et une"
fr.Convert(31, "gender=feminin");   // "trente et une"
fr.Convert(61, "gender=feminin");   // "soixante et une"

// Replacement applies to the LAST word only
// "million" is not replaced even in feminine
fr.Convert(1_000_000, "gender=feminin");    // "un million"
fr.Convert(1_000_021, "gender=feminin");    // "un million vingt et une"
```

### Listing available variants for a language

```csharp
NumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");

foreach (var dimension in fr.VariantDimensions)
{
    Console.WriteLine($"{dimension.Name}: {string.Join(", ", dimension.Values)}");
    Console.WriteLine($"  default: {dimension.DefaultValue}");
}
// gender: masculin, feminin
//   default: masculin
```

Via the `INumberToStringConverter` interface:

```csharp
INumberToStringConverter converter = NumberToStringConverter.GetConverter("FR");
bool supportsGender = converter.VariantDimensions
    .Any(d => d.Name.Equals("gender", StringComparison.OrdinalIgnoreCase));
```

### Spanish — gender and hundreds

In Spanish, `uno` (1) and compound hundreds `-cientos` vary in gender.

```csharp
NumberToStringConverter es = NumberToStringConverter.GetConverter("ES");

es.Convert(1);                      // "uno"
es.Convert(1,   "gender=femenino"); // "una"
es.Convert(200, "gender=femenino"); // "doscientas"
es.Convert(500, "gender=femenino"); // "quinientas"
es.Convert(900, "gender=femenino"); // "novecientas"
```

> **Limitation**: fused compound forms without spaces (`veintiuno`, `treintauno`) are not
> converted — the `LastWord` rule requires a space or hyphen before `uno`. Fixing the
> `buildStrings` in the configuration (`"treinta y *"` instead of `"treinta*"`) would solve this.

### Portuguese — gender with spaces, units and hundreds

Portuguese uses spaces in all its compounds (`vinte e um`), making the `LastWord` rule
effective for every form. `um` and `dois` vary, as do all hundreds (except 100 `cem`/`cento`).

```csharp
NumberToStringConverter pt = NumberToStringConverter.GetConverter("PT");

pt.Convert(1,   "gender=feminino"); // "uma"
pt.Convert(2,   "gender=feminino"); // "duas"
pt.Convert(21,  "gender=feminino"); // "vinte e uma"
pt.Convert(22,  "gender=feminino"); // "vinte e duas"
pt.Convert(200, "gender=feminino"); // "duzentas"
pt.Convert(201, "gender=feminino"); // "duzentas e uma"
pt.Convert(202, "gender=feminino"); // "duzentas e duas"

// The multiplier before "mil" stays masculine (last word = "mil")
pt.Convert(2_000, "gender=feminino"); // "dois mil"  (limitation)
```

### Italian — only `uno` varies

In Italian, hundreds (`duecento`, `trecento`…) are invariable in gender.
Only `uno` → `una` changes.

```csharp
NumberToStringConverter it = NumberToStringConverter.GetConverter("IT");

it.Convert(1, "gender=femminile"); // "una"
it.Convert(100); // "cento"         ← invariable
it.Convert(200); // "duecento"      ← invariable
```

> **Limitation**: fused compounds (`ventiuno`, `trentuno`…) are not converted,
> for the same reason as Spanish.

### Catalan — hyphens as word boundaries

Catalan uses hyphens in its compounds (`vint-i-un`, `trenta-un`…).
A hyphen is a word boundary for `LastWord`, so the rule applies correctly to compound
numbers. Only `dos-cents` (200) has a feminine form among hundreds.

```csharp
NumberToStringConverter ca = NumberToStringConverter.GetConverter("CA");

ca.Convert(1,   "gender=femení"); // "una"
ca.Convert(2,   "gender=femení"); // "dues"
ca.Convert(21,  "gender=femení"); // "vint-i-una"   ← hyphen = word boundary ✓
ca.Convert(22,  "gender=femení"); // "vint-i-dues"
ca.Convert(31,  "gender=femení"); // "trenta-una"
ca.Convert(200, "gender=femení"); // "dues-centes"
ca.Convert(201, "gender=femení"); // "dues-centes una"
```

### Galician — like Portuguese

Galician uses spaces (`vinte e un`) and follows logic similar to Portuguese.
`un` → `unha`, `dous` → `dúas`, and only `douscentos` (200) has a feminine form.

```csharp
NumberToStringConverter gl = NumberToStringConverter.GetConverter("GL");

gl.Convert(1,   "gender=feminino"); // "unha"
gl.Convert(2,   "gender=feminino"); // "dúas"
gl.Convert(21,  "gender=feminino"); // "vinte e unha"
gl.Convert(22,  "gender=feminino"); // "vinte e dúas"
gl.Convert(200, "gender=feminino"); // "douscentas"
gl.Convert(201, "gender=feminino"); // "douscentas unha"
```

### Belgian/Swiss French — same gender, different words

FR-be and FR-ch use septante/huitante/nonante instead of soixante-dix/quatre-vingts/quatre-vingt-dix,
but the gender rule is identical to FR: the `gender` dimension (masculin/féminin) is available.

```csharp
NumberToStringConverter frBe = NumberToStringConverter.GetConverter("FR-be");

frBe.Convert(71);                   // "septante et un"
frBe.Convert(71, "gender=feminin"); // "septante et une"
frBe.Convert(81, "gender=feminin"); // "huitante et une"
frBe.Convert(91, "gender=feminin"); // "nonante et une"

// "un million" → last word = "million" → no replacement
frBe.Convert(1_000_000, "gender=feminin"); // "un million"
```

### German — genus × kasus

In German, only one digit is declined: `ein` (1) takes different forms.
Compounds like `einundzwanzig` (21) are invariable.

```csharp
NumberToStringConverter de = NumberToStringConverter.GetConverter("DE");

// Default (masculine nominative) — GermanSpecifics: "ein" → "eins"
de.Convert(1);   // "eins"

// Gender and case variations
de.Convert(1, "genus=feminin");                       // "eine"
de.Convert(1, "kasus=akkusativ", "genus=maskulin");   // "einen"
de.Convert(1, "kasus=akkusativ", "genus=feminin");    // "eine"
de.Convert(1, "kasus=dativ",     "genus=maskulin");   // "einem"
de.Convert(1, "kasus=dativ",     "genus=neutrum");    // "einem"
de.Convert(1, "kasus=dativ",     "genus=feminin");    // "einer"
de.Convert(1, "kasus=genitiv",   "genus=maskulin");   // "eines"
de.Convert(1, "kasus=genitiv",   "genus=neutrum");    // "eines"
de.Convert(1, "kasus=genitiv",   "genus=feminin");    // "einer"

// Compounds are not declined
de.Convert(21, "genus=feminin");  // "einundzwanzig"  (unchanged)

// "eine Million": GermanSpecifics corrects "ein Million" → "eine Million" independently of variants
de.Convert(1_000_000);  // "eine Million"
```

Full inflection table for `ein`:

| kasus \ genus | maskulin | feminin | neutrum |
|--------------|----------|---------|---------|
| Nominativ    | eins*    | eine    | eins*   |
| Akkusativ    | einen    | eine    | eins*   |
| Dativ        | einem    | einer   | einem   |
| Genitiv      | eines    | einer   | eines   |

\* `GermanNumberToStringLanguageSpecifics` converts the raw form `ein` to `eins` (counting form).
For accusative/nominative neuter, the adjectival form `ein` (without -s) and the counting form `eins` are
indistinguishable without syntactic context; the system returns `eins` in both cases.

### Finnish — grammatical cases (sijamuoto)

Finnish has no grammatical gender but has 15 cases. Three cases are
implemented: nominative (default), partitive (`partitiivi`) and genitive
(`genetiivi`).

**Implementation notes:**

- Compound tens (`kaksikymmentä`…) and compound hundreds (`kaksisataa`…)
  are already in a partitive-compatible form in the configuration:
  only units and standalone scale words need `LastWord` rules.
- In the genitive, compound tens and hundreds change entirely
  (`kaksikymmentä` → `kahdenkymmenen`) via `Anywhere`.
- `seitsemän`, `kahdeksan`, `yhdeksän` (7, 8, 9) are invariable in the genitive.

```csharp
NumberToStringConverter fi = NumberToStringConverter.GetConverter("FI");

// Nominative (default)
fi.Convert(1);    // "yksi"
fi.Convert(21);   // "kaksikymmentä yksi"
fi.Convert(100);  // "sata"

// Partitive
fi.Convert(1,   "sijamuoto=partitiivi"); // "yhtä"
fi.Convert(2,   "sijamuoto=partitiivi"); // "kahta"
fi.Convert(5,   "sijamuoto=partitiivi"); // "viittä"
fi.Convert(10,  "sijamuoto=partitiivi"); // "kymmentä"
fi.Convert(11,  "sijamuoto=partitiivi"); // "yhtätoista"
fi.Convert(21,  "sijamuoto=partitiivi"); // "kaksikymmentä yhtä"
fi.Convert(100, "sijamuoto=partitiivi"); // "sataa"
fi.Convert(201, "sijamuoto=partitiivi"); // "kaksisataa yhtä"

// Genitive
fi.Convert(2,   "sijamuoto=genetiivi"); // "kahden"
fi.Convert(20,  "sijamuoto=genetiivi"); // "kahdenkymmenen"
fi.Convert(21,  "sijamuoto=genetiivi"); // "kahdenkymmenen yhden"
fi.Convert(200, "sijamuoto=genetiivi"); // "kahdensadan"
fi.Convert(221, "sijamuoto=genetiivi"); // "kahdensadan kahdenkymmenen yhden"
fi.Convert(11,  "sijamuoto=genetiivi"); // "yhdentoista"
```

> **Limitation**: in a compound number like `sata yksi` (101), the word `sata` (hundred)
> is not converted to `sadan` in the genitive, because it is neither the last word nor
> alone in the text. An `Anywhere "sata"→"sadan"` rule would corrupt compound forms
> like `kaksisataa`. Similarly, `yksi tuhat` (1000) produces `yksi tuhatta` in the partitive.

### Hebrew — gender paradox (zachar / nekeva)

In Hebrew, digits 3-10 exhibit a "gender paradox": the grammatically feminine form (-ה) is used
with masculine nouns (zachar), and the form without ה is used with feminine nouns (nekeva).

The `gender` dimension has three values:
- `standalone` (default): forms compatible with feminine nouns and abstract counting
- `zachar` (masculine nouns): adds ה to 3-9, 2 → שניים, 10 → עשרה
- `nekeva` (feminine nouns): only 1 changes (אחד → אחת)

```csharp
NumberToStringConverter he = NumberToStringConverter.GetConverter("HE");

he.Convert(1);                    // "אחד"   (standalone / default)
he.Convert(1, "gender=nekeva");   // "אחת"
he.Convert(2, "gender=zachar");   // "שניים"
he.Convert(3, "gender=zachar");   // "שלושה"
```

> **Limitation**: the multiplier before אלף (thousands) is not converted because
> "אלף" is the last word, not the unit.

### Discovering available variants

```csharp
NumberToStringConverter de = NumberToStringConverter.GetConverter("DE");

foreach (var dim in de.VariantDimensions)
    Console.WriteLine($"{dim.Name}: {string.Join(", ", dim.Values)}  (default: {dim.DefaultValue})");
// genus: maskulin, feminin, neutrum  (default: maskulin)
// kasus: nominativ, akkusativ, dativ, genitiv  (default: nominativ)
```

### Architecture — multi-dimensional variants and cascade

The XML configuration declares each dimension, then replacement rules from least specific to most specific. The declaration order of rules at equal constraint levels matters: a rule transforms the text in sequence, and the next rule sees the result of the previous one.

```xml
<Variants>
  <Dimension name="genus"  values="maskulin,feminin,neutrum" />
  <Dimension name="kasus"  values="nominativ,akkusativ,dativ,genitiv" />

  <!-- 1 constraint — genus=feminin declared FIRST -->
  <Variant genus="feminin">
    <Replacement oldValue="ein" newValue="eine" scope="LastWord" />
  </Variant>
  <!-- dativ/genitiv maskulin+neutrum: "ein" still present when genus≠feminin -->
  <Variant kasus="dativ">
    <Replacement oldValue="ein" newValue="einem" scope="LastWord" />
  </Variant>
  <Variant kasus="genitiv">
    <Replacement oldValue="ein" newValue="eines" scope="LastWord" />
  </Variant>

  <!-- 2 constraints — overrides the results of 1-constraint rules -->
  <Variant kasus="akkusativ" genus="maskulin">
    <Replacement oldValue="ein" newValue="einen" scope="LastWord" />
  </Variant>
  <!-- For dativ+feminin: genus=feminin has already changed "ein"→"eine",
       so the 2-constraint rule targets "eine" instead of "ein" -->
  <Variant kasus="dativ" genus="feminin">
    <Replacement oldValue="eine" newValue="einer" scope="LastWord" />
  </Variant>
  <Variant kasus="genitiv" genus="feminin">
    <Replacement oldValue="eine" newValue="einer" scope="LastWord" />
  </Variant>
</Variants>
```

**Cascade rules**: variants with fewer constraints are applied before those with more constraints. Within the same specificity level, the declaration order is preserved — allowing transformations to be composed.

**`LastWord` scope**: the replacement only applies if `oldValue` matches exactly the last word of the result (separated by a space or hyphen). This prevents modifying `ein` inside `einundzwanzig` or in `ein million` when the last word is `million`.

**Unknown dimensions**: if the caller passes a dimension not declared for a language, it is silently ignored — the result is the same as calling without any variant.

### Languages with no morphological variants

The following languages have no declared variants, either because their numeral morphology
is invariable in common contexts, or because the morphological distinction is not yet implemented.

| Code | Language | Reason |
|------|----------|--------|
| EN | English | Numbers are invariable (no gender or case) |
| NL | Dutch | Numbers are invariable |
| KO | Korean | Numbers are invariable |
| ZH | Chinese | No inflection |
| JA | Japanese | No inflection |
| EU | Basque | No grammatical gender (language isolate) |
| PL | Polish | Cases not yet implemented |
| RU | Russian | Cases not yet implemented |
| AR | Arabic | Not yet implemented |
| HI | Hindi | Not yet implemented (Hindi has gender but numbers are often invariable in common usage) |
| EL | Greek | Not yet implemented |
| ZU | Zulu | Not yet implemented |
| EE | Ewe | Not yet implemented |
| WO | Wolof | Not yet implemented |

For all these languages, `VariantDimensions` returns an empty list and any parameter
passed to `Convert()` is silently ignored.

---

## Currency

```csharp
using Utils.Mathematics;

var euro = new CurrencyDefinition
{
    UnitSingular    = "euro",
    UnitPlural      = "euros",
    SubunitSingular = "centime",
    SubunitPlural   = "centimes",
    Connector       = "et",
};

NumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");
fr.ConvertCurrency(1m,     euro);  // "un euro"
fr.ConvertCurrency(21.50m, euro);  // "vingt et un euros et cinquante centimes"
fr.ConvertCurrency(-5.01m, euro);  // "moins cinq euros et un centime"

var dollar = new CurrencyDefinition
{
    UnitSingular    = "dollar",
    UnitPlural      = "dollars",
    SubunitSingular = "cent",
    SubunitPlural   = "cents",
    Connector       = "and",
};

NumberToStringConverter en = NumberToStringConverter.GetConverter("EN");
en.ConvertCurrency(12.01m, dollar); // "twelve dollars and one cent"
```

`SubunitDigits` (default 2) controls the number of decimal places for subunits.

---

## Customisation via `NumberToStringConverterOptions`

Clone and modify an existing converter:

```csharp
var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
{
    AdjustFunction = text => text.ToUpperInvariant(),
    MaxNumber      = new BigInteger(999_999_999),
};
var converter = new NumberToStringConverter(options);

converter.Convert(42);            // "FORTY-TWO"
converter.Convert(1_000_000_000); // throws ArgumentOutOfRangeException
```

---

## Registering additional XML configurations

```csharp
// Load one or more XML configurations at startup
NumberToStringConverter.RegisterConfigurations([myXmlConfig]);

// Duplicate cultures are silently ignored (first registration wins)
NumberToStringConverter.RegisterConfigurations([xmlA, xmlA]); // does not throw
```

---

## `INumberToStringLanguageSpecifics`

Post-processing hook applied **after** variants, as the last step of the pipeline. Useful when a grammatical rule requires context that cannot be expressed by simple replacements.

```csharp
public class UpperCaseSpecifics : INumberToStringLanguageSpecifics
{
    public string FinalizeWriting(string languageIdentifier, string text)
        => text.ToUpperInvariant();
}
```

### Factory registration

Registering a named instance avoids reflection-based lookup at XML deserialization time:

```csharp
NumberToStringConverter.RegisterLanguageSpecifics(
    nameof(MyLanguageSpecifics),
    new MyLanguageSpecifics());
```

The content of the `<LanguageSpecifics>` node in the XML must match the full or short name passed here.

### `GermanNumberToStringLanguageSpecifics`

Provided in the package. Corrects `"ein"` → `"eins"` (standalone number) and `"ein Million"` → `"eine Million"`:

```csharp
NumberToStringConverter de = NumberToStringConverter.GetConverter("DE");

de.Convert(1);          // "eins"         ← standalone ein → eins
de.Convert(21);         // "einundzwanzig"
de.Convert(1_000_000);  // "eine Million" ← ein + feminine noun
de.Convert(2_000_000);  // "zwei Millionen"
```

---

## Conversion pipeline

```
number
  → ConvertRaw          (digit groups + exceptions + language replacements)
  → AdjustFunction      (optional user-supplied transformation)
  → ApplyVariantRules   (morphological replacements, least to most specific)
  → FinalizeWriting     (INumberToStringLanguageSpecifics)
  → sign wrapping       (Minus template if negative)
```

**Ordinal pipeline** (via `ConvertOrdinal`):

```
number
  → OrdinalExceptions   (integer-level early exit, e.g. 1 → "premier")
  → ConvertRaw          (raw cardinal text, no adjustment)
  → ApplyVariantRules   (default variant values)
  → ApplyOrdinalTransform  (word rules + suffix on last word)
  → AdjustFunction      (user transform + FinalizeWriting)
  → sign wrapping
```

Applying ordinal rules **before** `AdjustFunction` ensures that word-level rules always match
the raw cardinal text, regardless of any uppercase transformation or language-specific
finalizer applied later.

Variants are applied *before* `FinalizeWriting`. This ensures that for German, a variant can act
on the raw form `"ein"` before `GermanNumberToStringLanguageSpecifics` converts it to `"eins"`.

---

## XML Configuration

Language configurations are XML files whose structure is described by
`NumberConvertionConfiguration.xsd` (namespace `Utils/NumberConvertionConfiguration.xsd`).

### General structure

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
    <Language groupSize="3" separator=" " groupSeparator=""
              zero="zéro" minus="moins *"
              decimalSeparator="virgule" fractionSeparator="sur">

        <Culture>FR</Culture>      <!-- 2-letter code -->
        <Culture>FR-fr</Culture>   <!-- optional region code -->

        <Groups>…</Groups>
        <NumberScale>…</NumberScale>
        <Replacements>…</Replacements>         <!-- optional -->
        <Exceptions>…</Exceptions>             <!-- optional -->
        <LanguageSpecifics>…</LanguageSpecifics> <!-- optional -->
        <Fractions>…</Fractions>               <!-- optional -->
        <Ordinals suffix="…">…</Ordinals>      <!-- optional -->
        <Variants>…</Variants>                 <!-- optional -->

    </Language>
</Numbers>
```

A single file may contain multiple `<Language>` elements. Multiple `<Culture>` elements
on the same language register the same converter under several codes.

### `<Language>` attributes

| Attribute | Required | Description |
|-----------|----------|-------------|
| `groupSize` | ✓ | Number of digits per group (always 3 for thousands). |
| `separator` | ✓ | Word separator within a group (usually `" "`). |
| `groupSeparator` | ✓ | Text between groups (e.g. `","` in English, `""` in French). |
| `zero` | ✓ | Text for the value 0. |
| `minus` | ✓ | Template for negatives; `*` is replaced by the absolute value. |
| `decimalSeparator` | | Word between the integer and decimal parts (e.g. `"point"`, `"virgule"`). |
| `fractionSeparator` | | Connector for fractions (e.g. `"sur"`, `"over"`). |
| `maxNumber` | | Maximum accepted value; beyond this, `ArgumentOutOfRangeException` is thrown. |
| `baseOn` | | Reserved (future inheritance). |

---

### `<Groups>` — digit tables

Each `<Group level="N">` declares how digits 0–9 are written at position N
in a group: `level="1"` = units, `level="2"` = tens, `level="3"` = hundreds.

Each `<Digit digit="N" string="…" buildString="…"/>`:
- `string` — text when this digit is alone in its position.
- `buildString` — template with `*` replaced by the lower sub-group.

```xml
<Groups>
    <Group level="1">
        <Digit digit="0" string="" />
        <Digit digit="1" string="et un" />
        <Digit digit="2" string="deux" />
        <!-- … -->
    </Group>
    <Group level="2">
        <Digit digit="0" string="" buildString="*" />
        <Digit digit="2" string="vingt" buildString="vingt *" />
        <!-- digit=2, group=2, sub="et un" → buildString="vingt *" → "vingt et un" -->
        <!-- … -->
    </Group>
    <Group level="3">
        <Digit digit="1" string="cent" buildString="cent *" />
        <!-- … -->
    </Group>
</Groups>
```

---

### `<NumberScale>` — names of large powers

```xml
<NumberScale firstLetterUpperCase="false" voidGroup="ni" groupSeparator="lli" startIndex="0">

    <!-- Fixed names for the first scale levels -->
    <StaticNames>
        <Scale value="0" string=""/>        <!-- units group (no suffix) -->
        <Scale value="1" string="mille"/>   <!-- 10^3 -->
        <!-- Further levels can also be listed: million, milliard, … -->
    </StaticNames>

    <!-- Suffixes for dynamically generated levels (Latin prefix + suffix) -->
    <Suffixes>
        <Suffix>on(s)</Suffix>    <!-- million, billion, trillion… -->
        <Suffix>ard(s)</Suffix>   <!-- milliard, billiard…         -->
    </Suffixes>

    <!-- Optional prefix tables (override the default Latin values) -->
    <Scale0Prefixes>…</Scale0Prefixes>
    <UnitsPrefixes>…</UnitsPrefixes>
    <TensPrefixes>…</TensPrefixes>
    <HundredsPrefixes>…</HundredsPrefixes>

</NumberScale>
```

`firstLetterUpperCase="true"` capitalises generated scale names (useful for German:
"Million", "Milliarde"). The `"(s)"` string in names is a plural marker.

---

### `<Replacements>` — global substitutions

Applied **after** raw text assembly and **before** variants.

```xml
<Replacements>
    <!-- scope omitted → Standalone: replaces only if the entire text = oldValue -->
    <Replacement oldValue="un mille" newValue="mille" />

    <!-- Anywhere: replaces all occurrences in the text -->
    <Replacement oldValue="vingt et " newValue="vingt-" scope="Anywhere" />

    <!-- LastWord: replaces only if oldValue is the last word -->
    <Replacement oldValue="un" newValue="une" scope="LastWord" />
</Replacements>
```

| Scope | Behaviour |
|-------|-----------|
| `Standalone` (default) | Replaces if the entire text equals `oldValue`. |
| `Anywhere` | Replaces all substring occurrences. |
| `LastWord` | Replaces `oldValue` only if it matches the last word (preceded by a space, hyphen, or start of string). |

---

### `<Exceptions>` — irregular forms

Checked with **absolute priority** before the grouping algorithm. Useful for numbers
whose form is completely irregular.

```xml
<Exceptions>
    <Number value="1"  string="un" />      <!-- form inside a group (≠ "et un") -->
    <Number value="11" string="onze" />
    <Number value="71" string="soixante onze" />
    <!-- … -->
</Exceptions>
```

---

### `<LanguageSpecifics>` — finalisation hook

The full or short type name of an `INumberToStringLanguageSpecifics` implementation
called as the last step of the pipeline. Can be pre-registered via
`RegisterLanguageSpecifics()` to avoid reflection-based lookup.

```xml
<LanguageSpecifics>GermanNumberToStringLanguageSpecifics</LanguageSpecifics>
```

---

### `<Fractions>` — decimal denominator suffixes

Allow the decimal part of a number to be expressed with a named denominator.
`"(s)"` is a plural marker.

```xml
<Fractions>
    <Fraction digits="1" string="dixième(s)" />    <!-- 0.5 → "cinq dixièmes" -->
    <Fraction digits="2" string="centième(s)" />   <!-- 0.25 → "vingt-cinq centièmes" -->
    <Fraction digits="3" string="millième(s)" />
</Fractions>
```

---

### `<Ordinals>` — ordinal conversion

Required to enable `ConvertOrdinal()`. Resolution priority:
`OrdinalException` → `Ordinal` rule (last word) → suffix (± strip trailing string).

```xml
<Ordinals suffix="ième" removeTrailing="e">

    <!-- Whole-number exceptions (highest priority) -->
    <OrdinalException value="1" string="premier" />

    <!-- Rules on the last word of the cardinal -->
    <Ordinal from="un"   to="unième" />
    <Ordinal from="cinq" to="cinquième" />
    <Ordinal from="neuf" to="neuvième" />

    <!-- All others: last word + strip "e" + "ième"  -->
    <!-- "quatre" → "quatr" + "ième" → "quatrième"  -->
    <!-- "mille"  → "mill"  + "ième" → "millième"   -->

</Ordinals>
```

| Attribute | Description |
|-----------|-------------|
| `suffix` | Suffix added when no rule matches. |
| `removeTrailing` | String to strip from the end of the last word before adding `suffix` (only when the word ends with this value). |

---

### `<Variants>` — morphological variants

Declares the variation dimensions and associated replacement rules.
Activated by calls to `Convert(number, "dimension=value", …)`.

```xml
<Variants>

    <!-- 1. Dimension declarations (must precede Variant elements) -->
    <!-- The first value of each dimension is the DEFAULT -->
    <Dimension name="gender" values="masculin,feminin" />
    <!-- Multi-dimension example: -->
    <!-- <Dimension name="kasus" values="nominativ,akkusativ,dativ,genitiv" /> -->

    <!-- 2. Variant rules -->
    <!-- Variant with no attributes = wildcard (always applied first) -->
    <!-- Variant with N attributes = N constraints → higher priority -->

    <Variant gender="feminin">
        <!-- scope="LastWord": replaces "un" only if it is the last word -->
        <!-- "un million" → last word = "million" ≠ "un" → no replacement -->
        <!-- "vingt et un" → last word = "un" → "vingt et une"            -->
        <Replacement oldValue="un" newValue="une" scope="LastWord" />
    </Variant>

    <!-- 2-constraint example (higher priority than genus alone):
         see DE configuration for the full genus × kasus implementation -->
    <!-- <Variant kasus="akkusativ" genus="maskulin">
        <Replacement oldValue="ein" newValue="einen" scope="LastWord" />
    </Variant> -->

</Variants>
```

**Cascade rules**: variants are applied in ascending order of constraint count. A 2-constraint
variant can therefore override the result of a 1-constraint variant. Undeclared dimensions
and unknown values are silently ignored.

---

## Related packages

- `omy.Utils` — contains `NumberToStringConverter`, `NumberToStringConverterOptions`, and all built-in culture XML configurations.
