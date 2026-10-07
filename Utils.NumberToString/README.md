# omy.Utils.NumberToString

Number-to-string conversion for multiple languages and cultures, with support for ordinals, morphological variants (gender, case…), and currency amounts.

## Install

First published as `2.0.0-rc.1`; the current candidate is `2.0.0-rc.2`. No stable release exists yet,
so `dotnet add package` requires an explicit version (NuGet does not install a prerelease by default):

```bash
dotnet add package omy.Utils.NumberToString --version 2.0.0-rc.2
```

## Supported frameworks

- net8.0

## Supported cultures

| Code | Language | Ordinals | ClockTime (step, cycle) | Variants | Inherited / local | Known limitations |
|------|----------|----------|-------------------------|----------|-------------------|-------------------|
| EN, EN-us | English | ✓ declarative | ✓ 5 min, 12 h | — | local | No day-part wording; only midnight/noon are special |
| EN-GB, EN-uk | British English | ✓ inherited | ✓ inherited | — | child of EN | Same as EN |
| FR, FR-fr, FR-ca | French | ✓ declarative | ✓ 5 min, 24 h | gender | local | — |
| FR-be | Belgian French (septante / quatre-vingts / nonante) | ✓ merged | ✓ inherited | gender | child of FR | — |
| FR-ch | Swiss French (septante / huitante / nonante) | ✓ inherited | ✓ inherited | gender | child of FR | No cantonal `octante` |
| DE, de-DE, de-AT | German | ✓ declarative (zero: `nullte`, Duden) | ✓ 5 min, 12 h | genus × kasus | local | — |
| de-CH, de-LI | Swiss German | ✓ merged | ✓ inherited | genus × kasus | child of DE | — |
| NL | Dutch | ✓ declarative | ✓ 5 min, 12 h | — | local | No day-part wording |
| DA, DA-DK | Danish | ✓ plugin (int range) | ✓ 5 min, 12 h | gender (fælleskøn/intetkøn) | local | Ordinals above `int.MaxValue` fail closed |
| NO, NB, NB-NO | Norwegian Bokmål | ✓ plugin (int range) | ✓ 5 min, 12 h | gender (hankjønn/hunkjønn/intetkjønn) | local | Ordinals above `int.MaxValue` fail closed |
| SV, SV-SE | Swedish | ✓ plugin (int range) | ✓ 5 min, 12 h | — | local | Ordinals above `int.MaxValue` fail closed |
| BG, BG-BG | Bulgarian | ✓ plugin, gendered | ✓ 15 min, 12 h | gender (standalone/masculine/feminine/neuter) | local | Unverified compound round thousands/millions fail closed |
| HR, HR-HR | Croatian | ✓ declarative (last word) + range plugin | ✓ 15 min, 12 h | — | local | Masculine nominative ordinals only; round scale ordinals other than tisućiti (1 000), milijunti (1 000 000) and milijarditi (1 000 000 000) fail closed, including 10^12 and above |
| HU, HU-HU | Hungarian | ✓ plugin (int range) | ✓ 15 min, 12 h | — | local | — |
| CS, CS-CZ | Czech | ✓ plugin, gender × case | ✓ 15 min, 12 h | gender × case | local | Ordinals verified up to 9 999 (round millions/milliards: one only) |
| SK, SK-SK | Slovak | ✓ plugin, gender × case | ✓ 15 min, 12 h | gender × case | local | Ordinals verified up to 9 999 and one million |
| PL | Polish | ✓ plugin + declarative | ✓ 5 min, 12 h | rodzaj × przypadek | local | No ordinal of zero |
| RU | Russian | ✓ declarative (zero: `нулевой`) | ✓ 15 min, 12 h | gender × case | local | — |
| UK, UK-UA | Ukrainian | ✓ plugin, gender × case | ✓ 15 min, 12 h | gender × case | local | Round thousands verified up to 10 000 |
| ES | Spanish | ✓ declarative | ✓ 5 min, 12 h | gender × form | local | No ordinal of zero |
| IT | Italian | ✓ validated productive forms through round trilione: 1–1999 except 1110–1910, round thousands to 999000, round multiples of milione/miliardo/bilione/biliardo/trilione; selected sourced analytic compounds (1010, 100001–100009) (`<OrdinalStem>`, `<OrdinalComposition>`, `<OrdinalScale>`, domain-guard plugin) | ✓ 5 min, 12 h | gender | local | Unsourced ordinals fail closed by design: zero, 1110–1910, the other non-round thousands above 1999 and the non-round values from a million (no canonical form established); millions are separate nouns joined by `e` ("due milioni e centomila") |
| PT | Portuguese | ✓ declarative | ✓ 5 min, 12 h (direct "e …") | gender | local | Deliberate direct numeric reading (no "para"/"menos" constructions); PT-PT/PT-BR not split; no ordinal of zero |
| GL, gl-ES | Galician | ✓ declarative | ✓ 5 min, 12 h | gender | local | No ordinal of zero |
| RO, RO-RO | Romanian | ✓ plugin (DOOM) | ✓ 15 min, 12 h | gen | local | Ordinals up to 999 999 and one million (masculine) |
| CA, ca-ES | Catalan | ✓ declarative | ✓ 15 min, 12 h | gender | local | Traditional quarters only; no ordinal of zero |
| ca-ES-valencia | Valencian | ✓ inherited | ✓ 5 min, 12 h, own section | gender | child of CA | Invariable `dos` in ClockTime only |
| EL | Greek | ✓ declarative | ✓ 15 min, 12 h | gender | local | Masculine cardinal forms (ένας) not modelled; no ordinal of zero |
| FI | Finnish | ✓ declarative (zero: nollas) | ✓ 15 min, 12 h | case | local | — |
| AR | Arabic | ✓ 1–99, 100, 1000 | ✓ 15 min, 12 h | gender | local | Other ordinals above 99 fail closed; thousands take the form their multiplier governs (ألف, ألفان, ثلاثة آلاف, أحد عشر ألفًا) |
| HE | Hebrew | ✓ 1–10 adjectives, above ten the agreeing cardinal (masculine by default, also for compounds ending in a teen) | ✓ 15 min, 12 h | gender (standalone/zachar/nekeva) | local | No ordinal of zero |
| FA, FA-IR | Persian | ✓ declarative | ✓ 15 min, 12 h | — | local | — |
| TR, TR-TR | Turkish | ✓ declarative (vowel harmony) | ✓ 15 min, 12 h | case (nominative/accusative/dative) | local | — |
| HI | Hindi | ✓ declarative | ✓ 15 min, 12 h | gender | local | No lakh/crore grouping |
| JA | Japanese | ✓ prefix 第 | ✓ 5 min, 12 h | — | local | No 午前/午後 |
| KO | Korean | ✓ prefix 제 | ✓ 5 min, 12 h | — | local | Native hour words only in ClockTime; no 오전/오후 |
| ZH | Chinese | ✓ prefix 第 | ✓ 15 min, 12 h | — | local | `两` only in ClockTime; no 上午/下午 |
| VN, VI, VI-VN | Vietnamese | ✓ prefix thứ | ✓ 15 min, 12 h | — | local | No sáng/chiều/tối |
| ID, ID-ID | Indonesian | ✓ plugin | ✓ 5 min, 12 h | — | local | No day-part wording |
| MS, MS-MY | Malay (lapan, bilion, trilion) | ✓ plugin | ✓ 5 min, 12 h, own section | — | child of ID | No day-part wording |
| EU, eu-ES | Basque | ✓ declarative | ✓ 15 min, 12 h | — | local | Clock-case forms only in ClockTime |
| SW, SW-KE, SW-TZ | Swahili | — deferred (noun-class concord) | ✓ 15 min, 12 h, six-hour offset | — | local | No asubuhi/mchana/jioni/usiku |
| ZU | Zulu | — deferred (noun-class policy) | ✓ 15 min, 12 h | — | local | Hour forms only in ClockTime |
| EE | Ewe | ✓ declarative `-lia` on the last element, first `gbãtɔ` + zero guard | — deferred (no sourced minute convention) | — | local | Cardinals validated to 999 999 999 with the static scales `akpe` and `miliɔn` (scale noun first: `akpe eve` = 2000, `miliɔn alafa eve` = 200 000 000, `multiplierPosition="afterScale"`); no ordinal of zero; `biliɔn`/`triliɔn` not opened (NTS-23) |
| WO | Wolof | ✓ declarative `-éel` + plugin guard | — deferred (competing conventions) | — | local | Cardinals to 999 999; no ordinal of zero nor of the round thousands |

"plugin" means an `IOrdinalLanguageSpecifics` implementation; values it does not implement fail
closed with `NotSupportedException` rather than returning a cardinal. The declarative pipeline
never returns the cardinal for zero either: without a zero exception, word rule, suffix or prefix,
`ConvertOrdinal(0)` throws `NotSupportedException` (NTS-12). Sources and decisions per
configuration: `docs/NTS-08-linguistic-sources.md`.

---

## Basic conversion

```csharp
using Utils.NumberToString;

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
frBe.ConvertOrdinal(80);  // "quatre-vingtième"   ← FR-be ordinal exception (not derived from "quatre-vingts")
frBe.ConvertOrdinal(90);  // "nonantième"

NumberToStringConverter frCh = NumberToStringConverter.GetConverter("FR-ch");
frCh.ConvertOrdinal(80);  // "huitantième"        ← Swiss 80 + removeTrailing="e"
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

```csharp
NumberToStringConverter de = NumberToStringConverter.GetConverter("DE");

de.ConvertOrdinal(1);    // "erste"             ← irregular
de.ConvertOrdinal(3);    // "dritte"            ← irregular
de.ConvertOrdinal(7);    // "siebte"            ← irregular
de.ConvertOrdinal(2);    // "zweite"            ← word rule
de.ConvertOrdinal(20);   // "zwanzigste"        ← suffix "ste"
de.ConvertOrdinal(21);   // "einundzwanzigste"  ← fused compound + suffix
de.ConvertOrdinal(1000); // "tausendste"
de.ConvertOrdinal(1001); // "tausend erste"     ← word rule "ein" → "erste"
```

```csharp
NumberToStringConverter es = NumberToStringConverter.GetConverter("ES");

es.ConvertOrdinal(1);                      // "primero"
es.ConvertOrdinal(10);                     // "décimo"
es.ConvertOrdinal(20);                     // "vigésimo"
es.ConvertOrdinal(1,  "gender=femenino");  // "primera"
es.ConvertOrdinal(20, "gender=femenino");  // "vigésima"
```

```csharp
NumberToStringConverter it = NumberToStringConverter.GetConverter("IT");

it.ConvertOrdinal(1);                        // "primo"
it.ConvertOrdinal(11);                       // "undicesimo"
it.ConvertOrdinal(20);                       // "ventesimo"
it.ConvertOrdinal(1000);                     // "millesimo"
it.ConvertOrdinal(1,    "gender=femminile"); // "prima"
it.ConvertOrdinal(1000, "gender=femminile"); // "millesima"
```

```csharp
NumberToStringConverter pt = NumberToStringConverter.GetConverter("PT");

pt.ConvertOrdinal(1);                          // "primeiro"
pt.ConvertOrdinal(11);                         // "décimo primeiro"   ← compound exception
pt.ConvertOrdinal(1000);                       // "milésimo"
pt.ConvertOrdinal(1,  "gender=feminino");      // "primeira"
pt.ConvertOrdinal(21, "gender=feminino");      // "vinte e primeira"  ← feminine compound
pt.ConvertOrdinal(22, "gender=feminino");      // "vinte e segunda"
```

```csharp
NumberToStringConverter ca = NumberToStringConverter.GetConverter("CA");

ca.ConvertOrdinal(1);                      // "primer"          ← exception
ca.ConvertOrdinal(5);                      // "cinquè"          ← word rule
ca.ConvertOrdinal(20);                     // "vintè"           ← suffix "è" (trailing "a" stripped)
ca.ConvertOrdinal(1,  "gender=femení");    // "primera"
ca.ConvertOrdinal(21, "gender=femení");    // "vint-i-unena"    ← feminine + suffix "ena"
ca.ConvertOrdinal(22, "gender=femení");    // "vint-i-dosena"   ← word rule "dues" → "dosena"
```

```csharp
NumberToStringConverter gl = NumberToStringConverter.GetConverter("GL");

gl.ConvertOrdinal(1);                      // "primeiro"
gl.ConvertOrdinal(12);                     // "duodécimo"        ← unique to Galician
gl.ConvertOrdinal(20);                     // "vixésimo"
gl.ConvertOrdinal(1,  "gender=feminino");  // "primeira"
gl.ConvertOrdinal(21, "gender=feminino");  // "vinte e primeira" ← "unha" → "primeira"
```

```csharp
NumberToStringConverter he = NumberToStringConverter.GetConverter("HE");

he.ConvertOrdinal(1);                    // "ראשון"   ← masculine default
he.ConvertOrdinal(10);                   // "עשירי"
he.ConvertOrdinal(1, "gender=nekeva");   // "ראשונה"  ← feminine
he.ConvertOrdinal(3, "gender=nekeva");   // "שלישית"
he.ConvertOrdinal(20);                   // "עשרים"   ← above 10: cardinal fallback
```

```csharp
// Prefix ordinals (ZH, JA, KO, VN)
NumberToStringConverter.GetConverter("ZH").ConvertOrdinal(1);   // "第一"
NumberToStringConverter.GetConverter("JA").ConvertOrdinal(3);   // "第三"
NumberToStringConverter.GetConverter("KO").ConvertOrdinal(2);   // "제이"
NumberToStringConverter.GetConverter("VN").ConvertOrdinal(1);   // "thứ nhất" ← exception, then prefix

// Suffix on the last element of the cardinal (WO)
NumberToStringConverter.GetConverter("WO").ConvertOrdinal(12);    // "fukk ak ñaaréel"
NumberToStringConverter.GetConverter("WO").ConvertOrdinal(2001);  // "ñaari junni ak bennéel"
NumberToStringConverter.GetConverter("WO").ConvertOrdinal(1);   // "bu njëkk" ← suppletive

// Scale noun before its multiplier: the suffix lands on the multiplier (EE)
NumberToStringConverter.GetConverter("EE").Convert(2001);         // "akpe eve kple ɖeka"
NumberToStringConverter.GetConverter("EE").ConvertOrdinal(21);    // "blaeve vɔ ɖekɛlia"
NumberToStringConverter.GetConverter("EE").ConvertOrdinal(2000);  // "akpe evelia"
NumberToStringConverter.GetConverter("EE").ConvertOrdinal(1);     // "gbãtɔ" ← suppletive
```

### `SupportsOrdinals`

```csharp
INumberToStringConverter conv = NumberToStringConverter.GetConverter("DE");
if (conv.SupportsOrdinals)
    Console.WriteLine(conv.ConvertOrdinal(5));  // "fünfte"
```

`SupportsOrdinals` returns `false` for languages that have no ordinal configuration (ZU…) and for any `INumberToStringConverter` implementation that does not override the default.

> **Ordinal pipeline**: word-level rules are matched against the raw cardinal text, before
> `AdjustFunction` and `INumberToStringLanguageSpecifics.FinalizeWriting` are applied.
> `AdjustFunction` (and `FinalizeWriting`) then run on the ordinal result. This means a
> converter with an uppercase `AdjustFunction` correctly produces `"TWENTY-FIRST"`, not
> `"TWENTY-ONEth"`.

> **Languages without ordinal support**: SW (Swahili) and ZU (Zulu) are deferred because of the
> noun-class concord.
> Their ordinals require an obligatory noun-class concord and have no standalone form; they are
> deliberately deferred (see `docs/NTS-08-linguistic-sources.md`).
> Romanian ordinals are supported through `RomanianOrdinalLanguageSpecifics` (DOOM forms) for
> 1–999 999 and one million (masculine); other values fail closed.
> For languages that have ordinals, `converter.SupportsOrdinals` returns `true`.

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
it.Convert(21, "gender=femminile"); // "ventuno" ← "ventuno ballerine" (Treccani)
```

Compound cardinals are written as one word through `<Fusion>` rules (see
[`<Fusion>`](#fusion--morphological-composition-at-a-junction)): `ventuno`, `ventitré`,
`ventotto`, `centottanta`, `duemila`, `milletré`. From a million the scale words are separate
nouns with a plural, joined to the lower groups by `e` (Treccani): `un milione`, `due milioni e
centomila`, `un milione e uno`, `ventuno milioni`, `un miliardo` (NTS-14). Soldered compounds keep `-uno` in the
feminine, as Treccani records for plural feminine nouns. Compound ordinals are formed by
`<OrdinalStem>` rules (see [`<OrdinalStem>`](#ending-rewrite-before-the-suffix--ordinalstem)):
`ventunesimo`, `ventitreesimo`, `ventiseiesimo`, `centunesimo`, `milleunesimo`, `duemillesimo`,
in both genders (`ventunesima`). After a hundred, `dieci` keeps its lexical ordinal (`centodecimo`,
`duecentodecimo`, Crusca) through exact word rules. The attested analytic forms are composed from
the ordinals of their parts (see [`<OrdinalComposition>`](#analytic-ordinals--ordinalcomposition)):
`millesimo decimo` (1010), `centomillesimoprimo` (100001). From a million the ordinal is formed on
the scale noun (see [`<OrdinalScale>`](#ordinals-of-round-scale-values--ordinalscale)):
`milionesimo`, `duemilionesimo`, `diecimilionesimo`, `miliardesimo`, `bilionesimo`,
`biliardesimo`, `trilionesimo`, `novetrilionesimo` (the largest round value of a `long`). Zero,
`1110`–`1910`, the other non-round thousands above 1999 (`2001`) and the non-round values from a
million (`1000001`) fail closed with `NotSupportedException`. This is a deliberate linguistic
limitation, not pending work: the engine could compose these values, but no consulted normative
source establishes a canonical composition or spelling for them.

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

FR-be uses septante/quatre-vingts/nonante and FR-ch uses septante/huitante/nonante instead of
soixante-dix/quatre-vingts/quatre-vingt-dix, but the gender rule is identical to FR: the `gender`
dimension (masculin/féminin) is available.

```csharp
NumberToStringConverter frBe = NumberToStringConverter.GetConverter("FR-be");

frBe.Convert(71);                   // "septante et un"
frBe.Convert(71, "gender=feminin"); // "septante et une"
frBe.Convert(81, "gender=feminin"); // "quatre-vingt une"
frBe.Convert(91, "gender=feminin"); // "nonante et une"

NumberToStringConverter frCh = NumberToStringConverter.GetConverter("FR-ch");
frCh.Convert(81, "gender=feminin"); // "huitante et une"

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

The multiplier before אלף/אלפים agrees with the scale noun, not with the selected gender; the
variant only reaches the lower group (NTS-11):

```csharp
he.Convert(3000, "gender=nekeva");  // "שלושת אלפים"
he.Convert(21000, "gender=nekeva"); // "עשרים ואחד אלף"
he.Convert(1001, "gender=nekeva");  // "אלף ואחת"
```

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

**Unknown dimensions**: if the caller passes a dimension or a value not declared for a language, the call is rejected with `ArgumentException` naming the allowed dimensions or values.

### Languages with no morphological variants

The following languages have no declared variants, either because their numeral morphology
is invariable in common contexts, or because the morphological distinction is not yet implemented.

| Code | Language | Reason |
|------|----------|--------|
| EN | English | Numbers are invariable (no gender or case) |
| NL | Dutch | Numbers are invariable |
| SV | Swedish | The clock hour "ett" is already the cardinal |
| HR | Croatian | Gendered cardinals and ordinals not modelled yet |
| HU | Hungarian | Numbers are invariable |
| FA | Persian | Numbers are invariable |
| KO | Korean | Numbers are invariable; native clock hours are ClockTime-only forms |
| ZH | Chinese | No inflection; the clock form 两 is ClockTime-only |
| JA | Japanese | No inflection |
| VN | Vietnamese | No inflection |
| ID, MS | Indonesian, Malay | No inflection |
| EU | Basque | No grammatical gender (language isolate); clock-case forms are ClockTime-only |
| SW | Swahili | Noun-class concords not modelled (ordinals deferred) |
| ZU | Zulu | Noun-class concords not modelled (ordinals deferred) |
| EE | Ewe | Not yet implemented |
| WO | Wolof | Not yet implemented |

For all these languages, `VariantDimensions` returns an empty list; passing a variant to
`Convert()` is rejected with `ArgumentException` (unknown variant dimension).

---

## Currency

```csharp
using Utils.NumberToString;

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

## ForcedVariants — constituent-local grammatical constraints

A configured lexical constituent (a time unit, a currency unit/subunit, a
fraction denominator term) may own an intrinsic grammatical constraint on the
numeric fragment it governs. French "heure" is feminine, so `21 heures` must
render as `vingt et une heures`, not `vingt et un heures` — but the caller of
`Convert(TimeSpan)` should not need to know that "heure" is feminine.

`ForcedVariants` let the constituent itself declare that constraint:

```csharp
NumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");

fr.Convert(21);                       // "vingt et un"        (ordinary cardinal: masculine default)
fr.Convert(TimeSpan.FromHours(21));   // "vingt et une heures" (the "hour" unit forces gender=feminin)
```

No caller variant is required — the built-in French configuration declares
`forceVariants="gender=feminin"` on the `hour`/`minute`/`second` time units.

### Precedence

For the numeric fragment governed by a constituent, the effective variant
query is computed per dimension as:

```
language defaults  →  caller-supplied variants  →  constituent ForcedVariants
```

Each layer overrides the previous one **only for the dimensions it mentions**.
A constituent forcing `gender` does not erase a caller-supplied `case`:

```csharp
// "gender=masculin" from the caller does not override the hour unit's
// intrinsic gender=feminin — the constituent wins locally ("forced means forced").
fr.Convert(TimeSpan.FromHours(21), "gender=masculin"); // still "vingt et une heures"
```

### Locality — no leakage

ForcedVariants apply only while building the fragment governed by the
constituent that declares them. They never affect another fragment in the
same phrase, another call, or the converter's global defaults:

```csharp
fr.Convert(1);                        // "un"       — global default stays masculine
fr.Convert(new TimeSpan(1, 0, 0));    // "une heure" — local to the "hour" fragment only
```

### Not just French

The same mechanism, unmodified, also covers Portuguese, Galician, and Catalan,
whose `hour` noun is feminine — each declares `forceVariants="gender=..."` on
its `hour` unit only:

```csharp
NumberToStringConverter pt = NumberToStringConverter.GetConverter("PT");
pt.Convert(2);                       // "dois"
pt.Convert(new TimeSpan(2, 0, 0));   // "duas horas"      (the "hour" unit forces gender=feminino)

NumberToStringConverter ca = NumberToStringConverter.GetConverter("CA");
ca.Convert(21);                      // "vint-i-un"
ca.Convert(TimeSpan.FromHours(21));  // "vint-i-una hores" (the "hour" unit forces gender=femení)
```

Spanish (`ES`) also has time-unit support, but it needed one more dimension:
masculine attributive numeral apocope ("uno"→"un", "veintiuno"→"veintiún")
applies to compound counts (21, 31, …), not just count 1, which `Count1Form`
alone cannot express. A `form` dimension (`standalone`/`attributive`),
forced by the time units alongside `gender`, activates the correct existing
`gender`-specific rule for every count uniformly — see the "Not just French"
example turn into "not just gender" in `NumberConvertionConfiguration.ES.xml`
and `DONE-2026-08-25(1).md`:

```csharp
NumberToStringConverter es = NumberToStringConverter.GetConverter("ES");
es.Convert(21);                            // "veintiuno"        (standalone: unaffected)
es.Convert(new TimeSpan(0, 21, 0));        // "veintiún minutos" (minute forces form=attributive)
es.Convert(TimeSpan.FromHours(21));        // "veintiuna horas"  (hour forces gender=femenino,form=attributive)
```

### Currency: independent unit and subunit

`CurrencyDefinition.UnitForcedVariants` and `SubunitForcedVariants` let a
single currency force different grammar for its main unit and its subunit —
one phrase, two independent local queries:

```csharp
var euro = new CurrencyDefinition
{
    UnitSingular = "euro", UnitPlural = "euros",
    SubunitSingular = "centime", SubunitPlural = "centimes",
    // Masculine is already the FR default: no forcing needed.
};
var livre = new CurrencyDefinition
{
    UnitSingular = "livre", UnitPlural = "livres",
    SubunitSingular = "sou", SubunitPlural = "sous",
    UnitForcedVariants = ForcedVariantSet.Create(("gender", "feminin")),
};

fr.ConvertCurrency(21m, euro);   // "vingt et un euros"
fr.ConvertCurrency(21m, livre);  // "vingt et une livres"
```

### Configuring ForcedVariants

- **XML**: `forceVariants="dimension=value"` or `forceVariants="dimension=value,dimension2=value2"`
  on `<Unit>` (inside `<TimeUnits>`) or `<Fraction>`, reusing the same
  `dimension=value` vocabulary as caller-supplied variants.
- **Programmatic**: `NumberToStringConverterOptions.TimeUnitForcedVariants` /
  `FractionForcedVariants` (keyed like `TimeUnits`/`Fractions`), or
  `CurrencyDefinition.UnitForcedVariants`/`SubunitForcedVariants` — all typed
  `ForcedVariantSet`, built via `ForcedVariantSet.Create(("dimension", "value"), ...)`.

A dimension may be given by its canonical name or by its declared `localName`
alias (e.g. French `genre` for `gender`) — both are canonicalized to the
dimension's canonical name before the forced set is used, so `genre=feminin`
and `gender=feminin` behave identically. Forcing the same dimension twice
through a mix of its canonical name and an alias (e.g.
`gender=feminin,genre=masculin`) is rejected as a duplicate, exactly like
forcing it twice through the canonical name alone.

Unknown dimensions/values, malformed syntax, and duplicate dimensions are
rejected deterministically (`NumberToStringConfigurationException`) — for
time units and fraction terms at converter construction time, and for
`CurrencyDefinition` at the start of `ConvertCurrency`, before any fragment is
rendered.

---

## Special hours — `SpecialHourRule`

Many languages have a dedicated word for a specific hour of the day instead
of a numeral — "midnight"/"noon" in English, "minuit"/"midi" in French. This
is a generic, per-hour mechanism — not two hardcoded `Noon`/`Midnight`
properties — so any hour, and any word, can be configured for any language:

```xml
<TimeUnits>
    <Unit name="hour" singular="hour" plural="hours" />
    <Unit name="minute" singular="minute" plural="minutes" />
    <Unit name="second" singular="second" plural="seconds" />

    <SpecialHour hour="0" value="midnight" />
    <SpecialHour hour="12" value="noon" />
</TimeUnits>
```

- **`wholeHour="false"`** (default): the word replaces the hour only when the
  minute and second components are both zero — `12:00:00`; sub-second
  precision (like the rest of this method) is ignored, so `12:00:00.500` still
  counts as zero minutes/seconds. Any non-zero minute or second falls back to
  the ordinary numeral hour: `12:15` → `"twelve hours fifteen minutes"`. This
  is the right choice for English, where "noon fifteen minutes" would not
  read naturally.
- **`wholeHour="true"`**: the word replaces the hour for the *entire* hour —
  `12:00:00` through `12:59:59…` — and a non-zero minute/second is still
  appended after it as usual. This reads naturally in French, so the built-in
  French configuration uses it:

```xml
<SpecialHour hour="0" value="minuit" wholeHour="true" />
<SpecialHour hour="12" value="midi" wholeHour="true" />
```

```text
12:00 → "midi"
12:15 → "midi quinze minutes"
00:00 → "minuit"
00:30 → "minuit trente minutes"
```

Only `Convert(TimeOnly)` and the time portion of `Convert(DateTime)` apply
`SpecialHours` — never `Convert(TimeSpan)`. A 12-hour *duration* is not
"noon"; it has no time-of-day meaning.

Both overloads default to applying configured special hours, and take an
explicit `bool` — not an optional parameter — specifically so a bare
`converter.Convert(time)` call stays unambiguous against the existing
`params string[] variants` overload:

```csharp
converter.Convert(new TimeOnly(12, 0));                       // "noon"
converter.Convert(new TimeOnly(12, 0), replaceSpecialHours: false); // "twelve hours"
```

This keeps binary compatibility, with one narrow source-level exception: a
call site written with an untyped `default` literal standing in for
`variants` — `converter.Convert(time, default)` — becomes an ambiguous
overload call (`CS0121`), because the untyped `default` literal converts
equally well to the new overload's `bool` and the original `string[]`. Use
`[]` or omit `variants` entirely to keep such a call site compiling — **not**
`default(string[])`, which compiles but passes a `null` array that crashes
with `NullReferenceException` the moment any variant handling runs, since
`variants` is iterated without a null check.

**Programmatic**: `NumberToStringConverterOptions.SpecialHours`, a list of
`SpecialHourRule(int Hour, string Value, bool WholeHour = false)`. At most one
rule per `Hour` (0-23); a converter constructed with a duplicate, an
out-of-range hour, or an empty `Value` throws at construction time.

---

## Lexical form selection — `ILexicalFormSelector`

`ForcedVariants` constrains the grammar of the NUMBER a constituent governs.
A separate, complementary concern is which FORM OF THE UNIT WORD itself
applies for a given count — the current `Singular`/`Plural`/`Count1Form`
model assumes a binary choice, which is not enough for every language (e.g.
Russian "час"/"часа"/"часов" for 1 / 2–4 / 5+). `ILexicalFormSelector` makes
that choice extensible:

```csharp
public interface ILexicalFormSelector
{
    string SelectForm(LexicalFormContext context);
}
```

A selector returns a **form key** (`"singular"`, `"plural"`, `"one"`,
`"few"`, …) — never the localized word. The key-to-word mapping is owned by
configuration (`<Forms><Form key="..." value="..."/></Forms>` in XML, or
`NumberToStringConverterOptions.TimeUnitForms` programmatically), not by the
selector. Every unit that configures no selector uses
`DefaultLexicalFormSelector` (`AbsoluteValue == 1 → "singular"`, else
`"plural"`) — exactly today's behavior, so existing configurations are
unaffected.

```csharp
var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
{
    TimeUnitForms = new Dictionary<string, LexicalFormSet>
    {
        ["hour"] = LexicalFormSet.Create(("one", "час"), ("few", "часа"), ("many", "часов")),
    },
    TimeUnitFormSelectors = new Dictionary<string, ILexicalFormSelector>
    {
        ["hour"] = new MyCountBucketSelector(), // your ILexicalFormSelector implementation
    },
};
```

XML configuration resolves a `formSelector="..."` type name — while loading
configuration, never on the conversion hot path — through a single
resolution registry backed by the repository-standard
`Utils.Collections.CachedLoader`: the built-in `"default"` alias and any
name passed to `RegisterLexicalFormSelector` are preloaded directly into
that registry, so they never touch reflection; any other type name is
resolved by reflection on first use and the resulting *activation strategy*
is cached per distinct type name, so later uses of the same name skip
reflection. Register a name in advance to skip reflection entirely:

```csharp
NumberToStringConverter.RegisterLexicalFormSelector("my-company:count-bucket", new MyCountBucketSelector());
```
```xml
<Unit name="hour" singular="hour" plural="hours" formSelector="my-company:count-bucket">
  <Forms>
    <Form key="few" value="..." />
  </Forms>
</Unit>
```

A selector type may optionally declare a constructor accepting a
`LexicalFormSelectorConfiguration` (type name, language identifier, and
optional selector-specific `Configuration`) instead of a parameterless one;
implementations must be stateless/thread-safe after construction, since a
single instance is shared and may be called concurrently. A form key the
selector returns but the unit doesn't configure is a deterministic
`NumberToStringConfigurationException` (`"UNTS007"`), not a silent fallback.

### Selector-specific XML configuration

A unit that needs to pass its own configuration to a custom selector (not
just a type name) uses the structured `<LexicalFormSelector>` element instead
of the plain `formSelector` attribute:

```xml
<Unit name="hour" singular="hour" plural="hours">
  <LexicalFormSelector type="MyCompany.CountBucketSelector, MyCompany.Assembly">
    <Configuration threshold="5" />
  </LexicalFormSelector>
  <Forms>
    <Form key="few" value="часа" />
  </Forms>
</Unit>
```

The `<Configuration>` subtree's shape is owned entirely by the selector — the
core library never interprets its content, only hands it to the selector's
`LexicalFormSelectorConfiguration.Configuration` (an `XElement`) as-is. Units
that need no configuration keep using the concise `formSelector="..."`
attribute unchanged; it is ignored when `<LexicalFormSelector>` is present.

**Reflection is cached per type name, not per configured instance**: resolving
`"MyCompany.CountBucketSelector, ..."` for two units with two *different*
`<Configuration>` subtrees performs the expensive type/constructor lookup
once (cached as a reusable `Func<LexicalFormSelectorConfiguration,
ILexicalFormSelector>` activation strategy, never as a shared instance), then
re-invokes that cached activator with each unit's own configuration — so the
same selector implementation can be reused with different settings across
units without paying reflection cost twice, and without one unit's
configuration leaking into another's.

This activator cache is a plain `CachedLoader<string, Func<...>>` over a
`ConcurrentDictionary`, not a custom locking scheme: its check-then-load
sequence is not atomic, so under a race, first-time resolution of the same
not-yet-cached type name may run the reflection lookup more than once. Each
run is deterministic and side-effect-free, so this is harmless — only one
resulting activator ends up cached, and every resolution still returns a
correctly-configured selector — but it means the guarantee is "the loader
converges to one cached activator per type name," not "the loader runs at
most once, ever."

Programmatically, an already-constructed `ILexicalFormSelector` instance
never triggers reflection — reflection only ever resolves configured type
*names* (XML `formSelector`/`<LexicalFormSelector type="...">`, or a type
name passed to `RegisterLexicalFormSelector`), never instances supplied via
`NumberToStringConverterOptions.TimeUnitFormSelectors` or an instance passed
to `RegisterLexicalFormSelector` directly. A third `RegisterLexicalFormSelector`
overload accepts `Func<LexicalFormSelectorConfiguration, ILexicalFormSelector>`
so a registered name can still honor each unit's own configuration:

```csharp
NumberToStringConverter.RegisterLexicalFormSelector(
    "my-company:count-bucket",
    config => new MyCountBucketSelector(config.Configuration));
```

### `TimeUnitForms` / `TimeUnitFormSelectors` — effective, not override-only

`NumberToStringConverter.TimeUnitForms` and `.TimeUnitFormSelectors` report
the **effective** state for every key in `TimeUnits` — including units that
configured no override at all, which show a synthesized
`{"singular": ..., "plural": ...}` form set and `DefaultLexicalFormSelector`
respectively. A unit with an explicit override shows its singular/plural
merged with the override, not the override alone. This mirrors what
`FormatTimeUnit` actually uses at conversion time, so inspecting these
properties tells you exactly what a given unit will render.

**Spanish does not use this mechanism** — its "hora"/"minuto"/"segundo" only
ever need ordinary singular/plural word forms. Its attributive apocope
(`21 minutos` → `veintiún minutos`, not `veintiuno minutos`) is a
`ForcedVariants` numeral-grammar concern instead, solved with a `form`
dimension (`standalone`/`attributive`) — see the built-in
`NumberConvertionConfiguration.ES.xml`. `ILexicalFormSelector` exists for the
UNIT WORD; keep that distinction in mind before reaching for it.

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

The multiplier tables of [`<Groups onScale>`](#groups-onscale--multiplier-tables-and-multiplierposition)
and the multiplier position are available programmatically too (a clone copies both):

```csharp
var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
{
    ScaleScopedGroups = [new ScaleScopedGroups("1", multiplierTables)], // levels 1..3, like Groups
    ScaleMultiplierPosition = ScaleMultiplierPosition.AfterScale,       // "thousand two"
};
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

### `IOrdinalLanguageSpecifics`

When the XML pipeline is insufficient (e.g. Semitic root-pattern morphology), implement `IOrdinalLanguageSpecifics` alongside `INumberToStringLanguageSpecifics`. `TryConvertOrdinal` is called first; returning `false` falls back to the XML pipeline.

```csharp
public class MyOrdinalSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    public string FinalizeWriting(string lang, string text) => text;

    public bool TryConvertOrdinal(
        int number,
        IReadOnlyDictionary<string, string> variants,
        out string? result)
    {
        if (number == 0) { result = null; return false; }
        result = $"ordinal_{number}";
        return true;
    }
}
```

Built-in implementations:

| Plugin | Languages | Range |
|--------|-----------|-------|
| `PolishOrdinalLanguageSpecifics` | PL (20 and above; XML below) | 20–999 |
| `IndonesianOrdinalLanguageSpecifics`, `MalayOrdinalLanguageSpecifics` | ID, MS | `long` |
| `DanishOrdinalLanguageSpecifics`, `NorwegianOrdinalLanguageSpecifics`, `SwedishOrdinalLanguageSpecifics` | DA, NO, SV | `int` |
| `BulgarianOrdinalLanguageSpecifics` | BG | `int`, verified round compounds only |
| `HungarianOrdinalLanguageSpecifics` | HU | `int` |
| `CzechOrdinalLanguageSpecifics`, `SlovakOrdinalLanguageSpecifics`, `UkrainianOrdinalLanguageSpecifics` | CS, SK, UK | see the language matrix |
| `RomanianOrdinalLanguageSpecifics` | RO | 1–999 999, one million |
| `ArabicOrdinalLanguageSpecifics` | AR (20 and above; XML below) | 20–99, 100, 1000 |

A value a plugin does not implement either falls back to configured XML ordinal rules or, when
none exist, fails closed with `NotSupportedException`. `ArabicOrdinalLanguageSpecifics` throws
`NotSupportedException` itself above 99 so the XML rules for 1–19 never produce a cardinal
fallback for larger values.

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

## Significant-digits precision

Round a number to N most significant digits before converting, using standard rounding (≥ 5 rounds up):

```csharp
NumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");

fr.Convert(123456789);        // "cent vingt trois millions quatre cent cinquante six mille sept cent quatre-vingt-neuf"
fr.Convert(123456789, 3);     // "cent vingt trois millions"         (→ 123 000 000)
fr.Convert(123456789, 2);     // "cent vingt millions"               (→ 120 000 000)
fr.Convert(123456789, 1);     // "cent millions"                     (→ 100 000 000)
```

The rounding is done by `MathEx.RoundToSignificantDigits` (from `omy.Utils.Mathematics`) and then
delegates to the normal `Convert` pipeline, so variants work as expected:

```csharp
fr.Convert(123456789, 3, "gender=feminin"); // "cent vingt trois millions" (no gender change at this scale)
```

---

## Conversion pipeline

```
number
  → ConvertRaw:
      for each group (millions, thousands, units, …):
          ConvertGroup                    (digit text for this group: the Groups onScale table covering N, else Groups)
          Trigger group(N)                (optional: replacements on digit text)
          add scale name                  (ScaleForm selector when configured, else singular/plural;
                                           after the digits, or before them with multiplierPosition="afterScale")
          Replacements with onScale=N     (per-group rules, filtered by onValue)
          Trigger groupWithScale(N)       (optional: replacements on digit+scale text)
          push to stack
      assemble all groups
      Replacements without onScale        (global rules, filtered by onValue)
  → AdjustFunction      (optional user-supplied transformation)
  → ApplyVariantRules   (morphological replacements, least to most specific)
  → Trigger end         (optional: replacements on fully assembled text)
  → FinalizeWriting     (INumberToStringLanguageSpecifics)
  → sign wrapping       (Minus template if negative)
```

**Ordinal pipeline** (via `ConvertOrdinal`):

```
number
  → IOrdinalLanguageSpecifics plugin (when configured; may form the ordinal or reject the value)
  → OrdinalExceptions      (integer-level early exit, e.g. 1 → "premier")
  → OrdinalComposition     (ordinal(head) + separator + ordinal(tail), each part from the plugin step again)
  → OrdinalScale           (round scale value: multiplier cardinal + singular scale noun, instead of the two steps below)
  → ConvertRaw + Triggers group/groupWithScale (same as cardinal)
  → ApplyVariantRules      (default variant values)
  → ordinal Replacements   (base, then the selected ordinal variant; ordinal-only)
  → ApplyOrdinalTransform  (exact word rules, OrdinalStem / removeTrailing + suffix on last word)
  → AdjustFunction         (user transform + FinalizeWriting)
  → Trigger end
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
        <Trigger executeAt="…">…</Trigger>     <!-- optional, one per position -->

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
| `baseOn` | | Culture code of a base language to inherit from. All settings are inherited and can be selectively overridden. Chains (A → B → C) are supported; the base must appear earlier in the same file or in a previously loaded file. An empty element (e.g. `<Replacements />`) explicitly overrides the base with an empty list. |
| `groupConnector` / `groupConnectorThreshold` | | Word inserted between the last two groups when the lowest group's value is below the threshold (e.g. English "one thousand **and** one" — `groupConnector="and" groupConnectorThreshold="100"`). |
| `intraGroupConnector` / `intraGroupConnectorThreshold` | | Word inserted between the hundreds digit and the remainder within a group of 3, when hundreds are present and the remainder is below the threshold (e.g. Vietnamese 101 → "một trăm **linh** một" — `intraGroupConnector="linh" intraGroupConnectorThreshold="10"`). |
| `scaleConnector` / `scaleConnectorThreshold` | | Word inserted between a group's text and its scale name (thousand/million/…) when the group's value is at or above the threshold (e.g. Romanian 20 000 → "douăzeci **de** mii" — `scaleConnector="de" scaleConnectorThreshold="20"`). |
| `multiplierPosition` | | `beforeScale` (default, "two thousand") or `afterScale`: the scale noun precedes its multiplier (Ewe 2000 → "akpe eve"). See [`<Groups onScale>` and `multiplierPosition`](#groups-onscale--multiplier-tables-and-multiplierposition). |

---

### `baseOn` — language inheritance

`baseOn` lets a `<Language>` element inherit all settings from a base language and override only
the differences. The base must appear earlier in the same file or in a previously loaded file.
Inheritance chains (A → B → C) are fully supported.

Definitions loaded by `ReadConfiguration` can be reused by later `ReadConfiguration` calls, but
they are not globally registered converters and are not visible to `RegisterConfigurations`.

```xml
<!-- Standard German -->
<Language groupSize="3" …>
    <Culture>DE</Culture>
    <Replacements>
        <Replacement oldValue="ein tausend" newValue="tausend" />
    </Replacements>
    …
</Language>

<!-- Swiss German: inherits DE, removes the contraction for 1 000 -->
<Language baseOn="DE">
    <Culture>de-CH</Culture>
    <Culture>de-LI</Culture>
    <!-- Empty element overrides the base list with an empty one -->
    <Replacements />
</Language>
```

**Merge rules**:
- `Culture`: not inherited. Every `<Language>` declares at least one `<Culture>` (XSD `minOccurs="1"`) and only those are registered for the child. A regional culture must be declared once, in the child only, never also in its general parent (a culture declared by two built-in documents is a retained initialization failure).
- Scalar attributes (`groupSize`, `separator`, `groupSeparator`, `zero`, `minus`, `decimalSeparator`, `fractionSeparator`, `maxNumber`, `groupConnector`, `intraGroupConnector`, `scaleConnector` and their thresholds, `multiplierPosition`): child wins; absent child attributes inherit from the base.
- `<Groups onScale="…">` tables are merged by range: a child table whose range is identical to an inherited one (compared in canonical form) replaces it, a disjoint range is added, and any other overlap is rejected. The default `<Groups>` follows the rule below independently.
- Sections replaced as a whole when the child declares them (`Groups`, `Exceptions`, `Replacements`, `Fractions`, `Variants`, `YearFormat`, `Multiplicatives`, `TimeUnits`, `ClockTime`, `DateFormat`, `LanguageSpecifics`): the child's section replaces the base's completely, nothing is merged inside it. Omitted sections are inherited. An empty element (e.g. `<Replacements />`) explicitly overrides with an empty list.
- `Fusion` rules belong to their `<Digit>` and therefore follow the `Groups` rule: a child that omits `<Groups>` inherits the base's digits together with their fusions; a child that declares `<Groups>` replaces every digit, and the base's fusions are not merged into it.
- `Trigger` elements are currently **not** inherited: a child that needs the base's triggers must redeclare them (an absent `<Trigger>` list is read as an empty one). No built-in configuration uses triggers.
- `NumberScale`: merged field by field, not replaced wholesale. A child may declare only the sub-elements it needs to override (e.g. `StaticNames`, `Suffixes`) while `startIndex`, `firstLetterUpperCase`, `groupSeparator`, `voidGroup`, and the `Scale0Prefixes`/`UnitsPrefixes`/`TensPrefixes`/`HundredsPrefixes` prefix tables independently fall back to the base when absent in the child. For example, `MS` (Malay) declares only `StaticNames`/`Suffixes` and still inherits `ID`'s `startIndex` and prefix tables unchanged.
- `Ordinals`: not replaced wholesale. `OrdinalException` entries are merged by `value`, and `OrdinalRule` and `OrdinalStem` entries by `from` (child wins on key conflicts, new keys are appended). `suffix`, `prefix`, `removeTrailing`, and `OrdinalVariants` fall back to the base when absent in the child (`OrdinalVariants`, when present, replaces the base's block). For example, `FR-be` declares only `<OrdinalException value="80" string="quatre-vingtième"/>` and keeps every other French ordinal rule.

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

#### `<Groups onScale>` — multiplier tables, and `multiplierPosition`

Two independent primitives describe how a scale multiplier (the "two" of "two thousand") is written:

| Primitive | Decides |
|---|---|
| `<Groups onScale="…">` | how the multiplier is **built** (its digit tables) |
| `multiplierPosition` | how the multiplier is **assembled** with the scale noun (before or after it) |

A language declares one default `<Groups>` and, only when the multiplier of some scales is built
with a different grammar than the standalone cardinal, extra `<Groups onScale="…">` tables:

```xml
<Language … multiplierPosition="afterScale">
    <Groups>                       <!-- ordinary cardinal rendering -->
        <Group level="1"> … 1 = "one", 2 = "two" … </Group>
        <Group level="2"> … </Group>
        <Group level="3"> … </Group>
    </Groups>
    <Groups onScale="1..">         <!-- rendering of the multiplier inside scales 1 and above -->
        <Group level="1"> … 1 = "alpha", 2 = "beta" … </Group>
        <Group level="2"> … </Group>
        <Group level="3"> … </Group>
    </Groups>
    <NumberScale><StaticNames><Scale value="0" string="" /><Scale value="1" string="grand" /></StaticNames></NumberScale>
</Language>
```

With these tables, 2 → "two" but 2000 → "grand beta"; with `multiplierPosition="beforeScale"`
(the default) 2000 would be "beta grand", and without the scoped table "grand two".

- `onScale` uses the range syntax of `Replacement onScale` (`1`, `2..4`, `1..`). Every covered index
  is at least 1 (scale 0 is the standalone cardinal); two tables never cover the same index
  (overlaps and duplicate ranges are rejected at load). A table declares the same levels as the
  default one and replaces it as a whole: a multiplier never mixes units of one table with tens of
  another. Its digits carry their own `<Fusion>` rules, so a fusion declared there never affects the
  standalone cardinal. Whole-number `<Exceptions>` are shared by every table.
- After the multiplier text, the pipeline is unchanged: scale noun, `Replacement onScale` and scale
  variant rules (with `onValue` evaluated on the numeric multiplier), then `groupWithScale` triggers;
  `group` triggers see the text of the scoped table.
- Ranges are compiled once at load into a scale-index lookup; conversions do not parse, sort or allocate.
- Declare a scoped table only for forms that really differ: Ewe (`akpe eve`, 2000) needs
  `multiplierPosition="afterScale"` only, because its multipliers are the standalone cardinals.
- With `afterScale` the ordinal suffix lands on the multiplier (Ewe `akpe evelia`, never `akpelia`).
  [`<OrdinalScale>`](#ordinals-of-round-scale-values--ordinalscale), which assembles "multiplier +
  noun" itself from the standalone cardinal, is rejected with `afterScale` and on a scale covered by a
  scoped table.
- Programmatic equivalents: `NumberToStringConverterOptions.ScaleScopedGroups` and
  `ScaleMultiplierPosition`; the converter exposes copies through `ScaleScopedGroups` and
  `ScaleMultiplierPosition`, while `Groups` remains the default table.

#### `<Fusion>` — morphological composition at a junction

`buildString` places the lower sub-group into a template, which cannot express languages where
joining two numerals changes the words at the boundary. A `<Digit>` (levels 2 and above) may
declare `<Fusion>` children that replace `buildString` for selected values of the lower
sub-group:

- the **left constituent** is the digit's `string` (`venti`, `cento`);
- the **right constituent** is the already built text of the lower sub-group (`uno`, `ottanta`);
- when at least one rule matches, the result is `left + right` concatenated directly, after the
  rule's transformations; `buildString` is not used for that junction;
- without a matching rule, `buildString` is used exactly as before.

| Attribute | Meaning |
|---|---|
| `for` (required) | Values of the lower sub-group, in the shared range syntax (`1`, `1,8`, `1..9`, `80..89`). Every value must lie in the sub-group domain `[1, 10^(level-1) − 1]` (no open bounds). Supported up to group level 6. |
| `removeLeft` | Suffix removed from the left constituent (`venti` → `vent`). It must be present: a mismatch is rejected at load. |
| `removeRight` | Prefix removed from the right constituent. It must be present on every matched right constituent. |
| `left` | Replacement form of the left constituent for this fusion. |
| `right` | Replacement form of the right constituent for this fusion (`tre` → `tré` inside `ventitré`, while `3` alone stays `tre`). |

On each edge the override (`left`/`right`) is applied first, then the removal. Every attribute
that is present must be non-empty.

**Cumulative rules and precedence.** Every rule matching a value contributes, from the least
specific to the most specific. Specificity is the number of sub-group values the range covers
(fewer values = more specific); XML order never matters. A property absent from a more specific
rule keeps the value set by a less specific one; a property it sets overrides it. Only a range
nested in another may override it: two overlapping rules whose ranges cross (neither contains the
other, such as `1..5` and `4..6`) and that assign different values to the same property are
rejected at load, as are two rules with the same canonical range on one digit. Crossing rules that
touch different properties, or agree on a shared one, still combine.

```xml
<Digit digit="2" string="venti" buildString="venti *">
    <Fusion for="1..9" />                   <!-- solder 21-29: ventidue, ventisei… -->
    <Fusion for="1,8" removeLeft="i" />     <!-- ventuno, ventotto -->
    <Fusion for="3" right="tré" />          <!-- ventitré -->
</Digit>
```

For `23`, `1..9` enables the fusion and `3` adds `right="tré"`; for `21`, `1,8` adds
`removeLeft="i"`. The built-in Italian configuration solders through `buildString="venti*"`
instead of the general rule, so it only declares the junctions that change:

```xml
<Group level="2">
    <Digit digit="2" string="venti" buildString="venti*">
        <Fusion for="1,8" removeLeft="i" />   <!-- ventuno, ventotto -->
        <Fusion for="3" right="tré" />        <!-- ventitré -->
    </Digit>
    <Digit digit="3" string="trenta" buildString="trenta*">
        <Fusion for="1,8" removeLeft="a" />   <!-- trentuno, trentotto -->
        <Fusion for="3" right="tré" />        <!-- trentatré -->
    </Digit>
    <!-- … quaranta … novanta alike -->
</Group>
<Group level="3">
    <Digit digit="1" string="cento" buildString="cento*">
        <Fusion for="3" right="tré" />        <!-- centotré -->
        <Fusion for="80..89" removeLeft="o" /> <!-- centottanta, centottantatré -->
    </Digit>
    <!-- centouno, centootto keep the vowel: plain buildString -->
</Group>
```

**Scope.** A fusion is an *intra-group* junction resolved while one positional group is composed.
It never applies between a group and its scale name, nor between two scale groups: use
`Replacement` (with `onScale`/`onValue`), `groupConnector` or `scaleConnector` there — the
Italian thousands (`duemila`, `milletré`) and the Hebrew and Arabic thousands connectors are
configured that way. Lexicalized forms (Hindi `इक्कीस` for 21) belong in `<Exceptions>`.

**Difference with `Replacement`.** A `Replacement` rewrites the produced text by pattern, without
knowing which numerals met at a boundary; a `Fusion` is tied to one digit and to the numeric value
of the lower sub-group, so it only fires on the junction it describes.

**Validation.** Fusions are compiled once at construction into an immutable per-level, per-digit
table indexed by the sub-group value (no range parsing on the conversion path; nothing at all for
a language without fusions). Load fails on: a missing or empty range, values outside the domain,
empty attributes, duplicate ranges, crossing ranges overriding the same property, a fusion on level 1, a fusion that
overlaps the `intraGroupConnector` range, a fusion in a scale prefix table, or a `removeLeft`/
`removeRight` edge absent from the actual constituent. A value whose whole number is an
`<Exceptions>` entry, or whose lower constituent is empty, never reaches the composition step, so
a rule covering it is inert there.

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
"Million", "Milliarde"). The `"(s)"` string in names is a plural marker. `groupSeparator` joins the
prefix and the suffix of generated names (default `lli`: "million"); Italian uses `li` for
"milione", "miliardo", "bilione".

#### Scale lexical forms — `<ScaleForm>`

When a language needs more than singular/plural for a scale noun, `<ScaleForm>` (inside
`<NumberScale>`) declares named forms and an `ILexicalFormSelector` — the same mechanism as
[time units](#lexical-form-selection--ilexicalformselector):

```xml
<NumberScale firstLetterUpperCase="false">
    <StaticNames>…<Scale value="1" string="ألف" /></StaticNames>
    <ScaleForm scale="1" formSelector="ArabicScaleLexicalFormSelector">
        <Forms>
            <Form key="singular" value="ألف" />
            <Form key="dual" value="ألفان" />
            <Form key="plural" value="آلاف" />
            <Form key="singularAccusative" value="ألفًا" />
        </Forms>
    </ScaleForm>
</NumberScale>
```

- The **scale name** (`NumberScale.GetScaleName`) stays the base lexical name; the **form key** is
  chosen per conversion by the selector and resolved to a word from `<Forms>`.
- The selector receives `LexicalFormContext.Value` = the group multiplier (3 for 3 000) and
  `Variants` = the effective variant query. It returns a key only; words stay in XML.
- `singular`/`plural` are synthesized from the scale name and may be overridden; a scale with
  forms but no selector uses `DefaultLexicalFormSelector`. A key with no form throws
  `NumberToStringConfigurationException` `UNTS007`.
- A scale without `<ScaleForm>` keeps `GetScaleName(index).ToPlural(multiplier)` byte for byte.
- Selectors are resolved once while loading (`formSelector` attribute or a structured
  `<LexicalFormSelector type="…">`), never during conversion. Indices the scale cannot name and
  duplicate `scale` entries are rejected; `baseOn` merges entries by scale index.
- Programmatic: `NumberToStringConverterOptions.ScaleForms` / `ScaleFormSelectors`; the converter
  exposes read-only `ScaleForms` / `ScaleFormSelectors` snapshots of the **effective** state.
  `new NumberToStringConverterOptions(converter)` copies only the explicitly configured forms and
  selectors (like `TimeUnitForms`), so a clone given another `Scale` re-synthesizes its own
  singular/plural names.
- Changing the multiplier text itself (e.g. dropping "one"/"two" before Arabic ألف/ألفان) stays the
  job of `onScale` replacements.

```csharp
var ar = NumberToStringConverter.GetConverter("AR");
ar.Convert(2000);    // "ألفان"
ar.Convert(3000);    // "ثلاثة آلاف"
ar.Convert(11000);   // "أحد عشر ألفًا"
ar.Convert(101000);  // "مائة وألف"
```

---

### `<Replacements>` — substitutions

Rules fire either per-group (with `onScale`) or on the final assembled string (without `onScale`).

```xml
<Replacements>
    <!-- scope omitted → Standalone: replaces only if the entire text = oldValue -->
    <!-- fires on the final assembled string (no onScale) -->
    <Replacement oldValue="un mille" newValue="mille" />

    <!-- Anywhere: replaces all occurrences in the text -->
    <Replacement oldValue="vingt et " newValue="vingt-" scope="Anywhere" />

    <!-- LastWord: replaces only if oldValue is the last word -->
    <Replacement oldValue="un" newValue="une" scope="LastWord" />

    <!-- onScale=1: fires per-group on the thousands group text ("digit + scale") -->
    <!-- onValue=1: further restricts to when the thousands digit value is exactly 1 -->
    <!-- "ein tausend" → "tausend" only for 1 000; 21 000 is unaffected -->
    <Replacement oldValue="ein tausend" newValue="tausend" onScale="1" onValue="1" />
</Replacements>
```

#### `scope` values

| Scope | Behaviour |
|-------|-----------|
| `Standalone` (default) | Replaces if the entire text equals `oldValue`. |
| `Anywhere` | Replaces all substring occurrences. |
| `LastWord` | Replaces `oldValue` only if it matches the last word (preceded by a space, hyphen, or start of string). |
| `StartsWith` | Replaces if the text starts with `oldValue`. |
| `EndsWith` | Replaces if the text ends with `oldValue`. |

#### `onScale` — per-group firing

`onScale` restricts the rule to the per-group pass for one or more scale groups. It accepts the same comma-separated range syntax as `onValue`:

| Expression | Matches |
|------------|---------|
| `1` | Only the thousands group |
| `1..3` | Thousands, millions, and billions |
| `2..` | Millions and above |
| `1,3` | Thousands and billions only |

The rule then sees `"digit-text + separator + scale-name"` (e.g. `"ein tausend"`) rather than the fully assembled string. Without `onScale`, the rule fires on the final assembled string.

#### `onValue` — numeric value filter

`onValue` restricts the rule to specific numeric values. Syntax: comma-separated segments.

| Segment | Matches |
|---------|---------|
| `1` | Exactly 1 |
| `1..3` | 1, 2, or 3 (inclusive range) |
| `..5` | Any value ≤ 5 |
| `5..` | Any value ≥ 5 |
| `1,5..10` | 1, or 5 through 10 |

With `onScale`: the value is the per-group digit value (0–999 for 3-digit groups).  
Without `onScale`: the value is the full absolute number, applied in the final pass.

```xml
<!-- Fires for the thousands group (onScale=1) only when its digit value is 1 -->
<!-- 1 000 → "ein tausend" → "tausend";  21 000 → "einundzwanzig tausend" (unchanged) -->
<Replacement oldValue="ein tausend" newValue="tausend" onScale="1" onValue="1" />

<!-- Fires on the final string only for numbers 1–10 -->
<Replacement oldValue="ein" newValue="ett" onValue="1..10" />
```

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

Required to enable `ConvertOrdinal()`.

**Resolution order** (highest to lowest priority):
1. Active variant exceptions — from `<OrdinalVariants>`, most-specific constraint first.
2. Base `<OrdinalException>` — whole-number match.
   - then an applicable `<OrdinalComposition>` (ordinal of head + separator + ordinal of tail, each
     resolved from step 1 again), and, for a round scale value covered by `<OrdinalScale>`, the
     scale-noun text replaces the cardinal for the steps below.
3. Active variant word rules — from `<OrdinalVariants>`, most-specific first.
4. Base `<Ordinal>` word rule — last-word match.
5. Default suffix (base or variant), after rewriting the ending of the last word with the longest
   matching `<OrdinalStem>` rule — or, when no stem rule matches, after the `removeTrailing` strip.

Steps 3–5 act on the cardinal after the ordinal `<Replacement>` rules (see
[Ordinal-only replacements](#ordinal-only-replacements--replacement-inside-ordinals)).

```xml
<Ordinals suffix="ième" removeTrailing="e">

    <!-- Whole-number exceptions (checked before word rules) -->
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
| `suffix` | Suffix added to the last word when no word rule matches. |
| `removeTrailing` | String to strip from the end of the last word before adding `suffix` (only when the word actually ends with this value and no `<OrdinalStem>` rule matched). |
| `prefix` | String prepended to the entire ordinal result (e.g. `"第"` for Chinese, `"thứ "` for Vietnamese). May be combined with exceptions, which receive the prefix too; suffix and word rules are ignored when `prefix` is set. |

```xml
<!-- Prefix-based ordinals (ZH, JA, KO, VN) -->
<Ordinals prefix="第">
    <!-- All numbers: "第" + cardinal -->
</Ordinals>

<!-- Mixed prefix + exception (VN) -->
<Ordinals prefix="thứ ">
    <OrdinalException value="1" string="nhất" />
    <!-- 1 → "thứ nhất" (exception, then prefix); 2 → "thứ hai" (prefix + cardinal) -->
</Ordinals>
```

#### Ordinal-only replacements — `<Replacement>` inside `<Ordinals>`

`<Replacement>` elements placed in `<Ordinals>` (base) or in an ordinal `<Variant>` reuse the
cardinal replacement syntax (`oldValue`, `newValue`, `scope` = `Standalone`, `Anywhere`,
`StartsWith`, `EndsWith`, `LastWord`) but act **only on the ordinal pipeline**:

```
OrdinalException (whole number)          ← bypasses everything below
  ↓
ConvertRaw cardinal → cardinal VariantRules
  ↓
ordinal <Replacement>: base, then the selected ordinal variant
  ↓
exact <Ordinal from to> → <OrdinalStem> → removeTrailing → suffix / prefix
  ↓
adjustment, end triggers, finalization
```

- They see the cardinal already assembled and transformed by the cardinal `<Variants>` rules, and
  never change `Convert(...)`.
- The selected ordinal variant includes a dimension default injected when the caller passes no
  variant, so a rule declared in the default variant applies to `ConvertOrdinal(n)`.
- `onScale`/`onValue` filters and form-variant children are rejected: the rules act on the whole
  phrase.
- `baseOn`: a derived language declaring base ordinal replacements replaces the inherited list
  (like the cardinal `<Replacements>`); variant replacements follow `<OrdinalVariants>`, which a
  derived language replaces as a whole.
- Programmatic: `NumberToStringConverterOptions.OrdinalReplacements` and
  `OrdinalVariantRule.Replacements` (new constructor overload; the historical one is unchanged).

Hebrew (NTS-14): the ordinal above ten is the masculine agreeing cardinal, but the standalone
cardinal ends compounds with the counting teen; the default variant rewrites the end of the phrase:

```xml
<OrdinalVariants>
    <Variant type="gender" variant="standalone">
        <Replacement oldValue="אחת עשרה" newValue="אחד עשר" scope="EndsWith" />
        <!-- … the other teens … -->
    </Variant>
</OrdinalVariants>
<!-- ConvertOrdinal(111) → "מאה ואחד עשר"; Convert(111) → "מאה ואחת עשרה" -->
```

#### Ending rewrite before the suffix — `<OrdinalStem>`

`<OrdinalStem from="…" to="…" />` rewrites the **ending** of the last word before the ordinal
suffix is appended. It complements the two existing mechanisms:

| Rule | Acts on | Suffix appended? |
|------|---------|------------------|
| `<Ordinal from="x" to="y">` | the **whole** last word (exact match) | no — `to` is the complete ordinal |
| `<OrdinalStem from="x" to="y">` | the **ending** of the last word | yes — the effective suffix follows |
| `removeTrailing="x"` | one fixed ending | yes — historical fallback |

Semantics:

- `from` is required and must not be empty; `to` is required but may be empty (`to=""` removes
  the matched ending).
- Matching is ordinal and case-sensitive (`EndsWith`, no regular expression).
- When several rules match, the **longest `from` wins**; declaration order is irrelevant. Two rules
  with the same `from` are rejected at load.
- `<OrdinalException>` and exact `<Ordinal>` word rules keep priority; stem rules act only on the
  suffixed path (a prefix-only configuration ignores them).
- When a stem rule matches, `removeTrailing` is not applied to that word; when none matches,
  `removeTrailing` behaves exactly as before. A configuration without `<OrdinalStem>` is unchanged.
- The **effective suffix** — the `<OrdinalVariants>` `suffix` override when a variant is selected,
  otherwise the base `suffix` — is appended after the rewrite, so the base stem rules serve every
  variant. Stem rules are declared on `<Ordinals>` only.
- Under `baseOn`, stem rules are merged by `from`: inherited rules are kept, a child rule with the
  same `from` replaces the parent's, new child rules are added.
- The converter snapshots the rules at construction (`OrdinalStemRules`, sorted longest first);
  mutating the source collection afterwards has no effect.

Italian (NTS-13): the cardinal loses its final vowel before `-esimo`, except after `tre` (accent
dropped) and `sei`, and the plural `-mila` of round thousands becomes `mill-`:

```xml
<Ordinals suffix="esimo">
    <OrdinalStem from="centouno" to="centun" /> <!-- centouno     → centunesimo (Treccani) -->
    <OrdinalStem from="mila" to="mill" />       <!-- duemila      → duemillesimo -->
    <OrdinalStem from="tré" to="tre" />         <!-- ventitré     → ventitreesimo -->
    <OrdinalStem from="sei" to="sei" />         <!-- ventisei     → ventiseiesimo -->
    <OrdinalStem from="a" to="" />              <!-- trenta       → trentesimo -->
    <OrdinalStem from="e" to="" />              <!-- diciassette  → diciassettesimo -->
    <OrdinalStem from="i" to="" />              <!-- undici       → undicesimo -->
    <OrdinalStem from="o" to="" />              <!-- ventuno      → ventunesimo -->
    <OrdinalVariants>
        <!-- same stems, feminine suffix: ventunesima, ventitreesima, duemillesima -->
        <Variant type="gender" variant="femminile" suffix="esima" />
    </OrdinalVariants>
</Ordinals>
```

```csharp
var it = NumberToStringConverter.GetConverter("IT");
it.ConvertOrdinal(23);                       // "ventitreesimo"
it.ConvertOrdinal(26, "gender=femminile");   // "ventiseiesima"
it.ConvertOrdinal(2000);                     // "duemillesimo"
it.ConvertOrdinal(110);                      // "centodecimo" ← exact <Ordinal> word rule wins over the stems
it.ConvertOrdinal(2001);                     // NotSupportedException (deliberate: no canonical form)
```

#### Ordinals of round scale values — `<OrdinalScale>`

The historical pipeline transforms the whole cardinal, which is wrong when the ordinal is not built
on it: the Italian cardinal of 1 000 000 is `un milione`, whose mechanical ordinal `un milionesimo`
is the fraction "one millionth"; the ordinal is `milionesimo`. `<OrdinalScale>` forms the ordinal of
a **round scale value** — multiplier × scale unit, every lower group zero — on the scale noun:

```xml
<Ordinals suffix="esimo">
    <!-- scale 1 = thousands, 2 = milione, 3 = miliardo, 4 = bilione, 5 = biliardo, 6 = trilione -->
    <OrdinalScale scales="2..6" multiplierSeparator="" />
</Ordinals>
```

| Value | Text built | Ordinal |
|-------|------------|---------|
| 1 000 000 | `milione` (multiplier one dropped) | `milionesimo` |
| 2 000 000 | `due` + `""` + `milione` | `duemilionesimo` |
| 10 000 000 | `dieci` + `""` + `milione` | `diecimilionesimo` |
| 1 000 000 000 | `miliardo` (highest scale, never `mille` × milione) | `miliardesimo` |
| 2 000 001 | not round: historical pipeline (or plugin rejection) | — |

- `scales` uses the range syntax (`2`, `2..4`, `2..`); every index must be ≥ 1 and nameable by the
  `<NumberScale>`, and two rules must not cover the same index. `multiplierSeparator` is required
  and may be empty.
- The multiplier is rendered exactly like its standalone cardinal with the caller's variants: the
  cardinal `<Variants>` rules apply to it (scale-specific ones inside the cardinal, global ones
  afterwards, `onValue` evaluated against the multiplier). The noun is the singular scale word
  (through `<ScaleForm>` when configured); the cardinal variant rules are not applied to the
  joined text. That text then goes through the ordinal `<Replacement>` rules, the word rules,
  `<OrdinalStem>`, `removeTrailing` and the **effective suffix**, so the feminine variant needs no
  extra rule (`duemilionesima`).
- Only the highest scale of the value is considered. Whole-number `<OrdinalException>` entries and
  `<OrdinalComposition>` keep precedence. Cardinals are never affected.
- `baseOn`: a derived language declaring `<OrdinalScale>` replaces the inherited list. Programmatic:
  `NumberToStringConverterOptions.OrdinalScaleRules` (`OrdinalScaleRule(Scales, MultiplierSeparator)`),
  snapshotted by the converter (`OrdinalScaleRules`).

#### Analytic ordinals — `<OrdinalComposition>`

Some ordinals are juxtapositions of two ordinals rather than the transformation of one cardinal:
Treccani gives `millesimo decimo` for 1010 and `centomillesimoprimo` for 100001 (the synthetic
`centomiladuesimo` being a partitive, i.e. a fraction). `<OrdinalComposition>` splits the value
**numerically** and joins the ordinals of the parts:

```xml
<OrdinalComposition for="1010" divisor="1000" separator=" " />            <!-- millesimo decimo -->
<OrdinalComposition for="100001..100009" divisor="1000" separator="" />   <!-- centomillesimoprimo -->
```

- head = value − value mod `divisor`, tail = value mod `divisor`; the ordinal is ordinal(head) +
  `separator` + ordinal(tail). A value of the range whose head or tail is zero is not composed and
  continues through the rest of the pipeline.
- Each part goes through the **whole** ordinal pipeline again — plugin, exceptions, other
  compositions, `<OrdinalScale>`, cardinal transformation — with the caller's variants, so both
  parts agree (`millesima decima`, `centomillesimaprima`). The cardinal text is never cut up.
- Both parts are strictly smaller than the value, so the recursion terminates without any runtime
  bookkeeping. Raw adjustment, end triggers and the language finalization run once, on the composed
  result; the sign and prefix are applied once.
- `for` uses the range syntax; every value must be ≥ 1 and the ranges of two rules must not overlap
  (one rule per value, independent of declaration order). `divisor` must be ≥ 2; `separator` is
  required and may be empty. Ranges are parsed once at load.
- `baseOn`: a derived language declaring `<OrdinalComposition>` replaces the inherited list.
  Programmatic: `NumberToStringConverterOptions.OrdinalCompositionRules`
  (`OrdinalCompositionRule(For, Divisor, Separator)`), snapshotted by the converter
  (`OrdinalCompositionRules`).

```csharp
var it = NumberToStringConverter.GetConverter("IT");
it.ConvertOrdinal(1010);                          // "millesimo decimo"
it.ConvertOrdinal(100001, "gender=femminile");    // "centomillesimaprima"
it.ConvertOrdinal(2_000_000);                     // "duemilionesimo"
it.ConvertOrdinal(1_000_000_000, "gender=femminile"); // "miliardesima"
it.ConvertOrdinal(2_000_000_000_000_000_000);     // "duetrilionesimo"
it.ConvertOrdinal(1_000_001);                     // NotSupportedException (deliberate: no canonical form)
```

#### Variant-specific ordinal rules — `<OrdinalVariants>`

`<OrdinalVariants>` lets a single ordinal configuration produce gender- or case-inflected forms.
Each `<Variant>` block targets one dimension value via `type=` (dimension name) and `variant=`
(value). The most-specific matching variant (most constraints) wins.

```xml
<Ordinals suffix="ième" removeTrailing="e">
    <OrdinalException value="1" string="premier" />
    <Ordinal from="cinq" to="cinquième" />

    <OrdinalVariants>
        <Variant type="gender" variant="feminin">
            <OrdinalException value="1" string="première" />
            <!-- overrides suffix for this variant only -->
            <!-- <suffix override> / <removeTrailing override> also available -->
        </Variant>
    </OrdinalVariants>
</Ordinals>
```

`<Variant>` attributes inside `<OrdinalVariants>`:

| Attribute | Description |
|-----------|-------------|
| `type` | Dimension name (e.g. `"gender"`, `"case"`) or its `localName` alias. |
| `variant` | Single dimension value this block applies to. |
| `values` | Comma-separated list of values — shorthand for declaring several identical blocks. |
| `suffix` | Suffix override for this variant (replaces the `<Ordinals>` base suffix). |
| `removeTrailing` | `removeTrailing` override for this variant. |

Nested `<Variant>` children inherit the parent constraint and add their own (cascade):

```xml
<OrdinalVariants>
    <Variant type="gender" variant="feminin">
        <OrdinalException value="1" string="prima" />
        <Variant type="case" variant="accusative">
            <OrdinalException value="1" string="primam" />  <!-- {gender=feminin, case=accusative} -->
        </Variant>
    </Variant>
</OrdinalVariants>
```

#### Compact multi-gender ordinals with `forms=`

When all dimension values share the same exception or word rule structure, write both forms inline
instead of duplicating them in `<OrdinalVariants>`. The `<Variant>` child uses the `forms=`
attribute with one form per dimension value **in declaration order**:

```xml
<Variants>
    <Dimension name="gender" values="masculin,feminin" />
    <!-- cardinal rules, if any -->
</Variants>
<Ordinals suffix="ième" removeTrailing="e">
    <!-- forms are positionally matched to Dimension/@values: masculin, feminin -->
    <OrdinalException value="1">
        <Variant type="gender" forms="premier,première" />
    </OrdinalException>
    <!-- gender-neutral rules stay as-is -->
    <Ordinal from="un" to="unième" />
</Ordinals>
```

The same syntax applies to word-level rules:

```xml
<Ordinals>
    <Ordinal from="uno">
        <Variant type="gender" forms="primero,primera" />
    </Ordinal>
</Ordinals>
```

**Default form**: when `string=` is absent from `<OrdinalException>` (or `to=` from `<Ordinal>`),
the **first form** in `forms=` order is automatically registered as the no-variant default.
`ConvertOrdinal(1)` (no gender) returns `"premier"` without any extra configuration.

**Empty entries**: an empty slot (e.g. `forms=",première"`) skips the corresponding dimension
value — no rule is generated for that position.

**When to use `<OrdinalVariants>` instead**: use it when a variant requires a suffix override,
or when some variants need word-form mappings that do not align position-for-position with the
dimension values (e.g. feminine-only cardinals `"una"/"duas"` that have no masculine counterpart
among the ordinal word rules).

---

### `<Variants>` — morphological variants

Declares the variation dimensions and associated replacement rules.
Activated by calls to `Convert(number, "dimension=value", …)`.

```xml
<Variants>

    <!-- 1. Dimension declarations — must precede all Variant elements.
            The FIRST value of each dimension is the default. -->
    <Dimension name="gender" localName="genus" values="maskulin,feminin,neutrum" />
    <Dimension name="case"   localName="kasus" values="nominativ,akkusativ,dativ,genitiv" />

    <!-- 2. One-constraint rule: applied when gender=feminin is active -->
    <Variant type="gender" variant="feminin">
        <!-- scope="LastWord": replaces "ein" only at the end of the number text -->
        <Replacement oldValue="ein" newValue="eine" scope="LastWord" />
    </Variant>

    <!-- Multi-value shorthand: both dativ and genitiv share the same body -->
    <Variant type="case" values="dativ,genitiv">
        <Replacement oldValue="eine" newValue="einer" scope="LastWord" />
    </Variant>

    <!-- Nested (2 constraints, higher priority): feminin + dativ -->
    <Variant type="gender" variant="feminin">
        <Variant type="case" variant="dativ">
            <Replacement oldValue="eine" newValue="einer" scope="LastWord" />
        </Variant>
    </Variant>

</Variants>
```

**Cascade rules**: variants are applied in ascending order of constraint count. A 2-constraint
variant can therefore override the result of a 1-constraint variant. Unrecognised dimension
names and unknown values passed by a caller are rejected with `ArgumentException`.

`<Dimension>` attributes:

| Attribute | Required | Description |
|-----------|----------|-------------|
| `name` | ✓ | Canonical English identifier used in API calls (`"gender"`, `"case"`). |
| `localName` | | Optional language-specific alias (e.g. `"genus"`, `"sijamuoto"`). Normalised to `name` internally. |
| `values` | ✓ | Comma-separated ordered list of valid values. The **first** value is the default. |

`<Variant>` attributes inside `<Variants>`:

| Attribute | Description |
|-----------|-------------|
| `type` | Dimension name (canonical or `localName`). |
| `variant` | Single value that must be active. Mutually exclusive with `values`. |
| `values` | Comma-separated list of values — shorthand for several identical blocks. |

`<Replacement>` elements inside `<Variant>` support child `<Variant>` nodes with `forms=`
for multi-dimensional replacements (see `FormVariantType` in the XSD):

```xml
<!-- German "ein" — four accusative/dative/genitive case forms for masculine -->
<Replacement oldValue="ein" scope="LastWord">
    <Variant type="gender" variant="maskulin">
        <Variant type="case" forms="eins,einen,einem,eines" />
    </Variant>
</Replacement>
```

---

### `<YearFormat>` — year conversion

Optional. When present, `ConvertYear(int)` uses a split-at-hundreds algorithm for year values
within the declared `<SplitRange>` elements. Years outside all ranges fall back to `Convert(year)`.

```xml
<YearFormat hundredWord="hundred" zeroConnector="oh">
    <!-- Years 1100–1999: split at hundreds — "nineteen hundred", "nineteen oh five", ... -->
    <SplitRange from="1100" to="1999" />
    <!-- Years 2010–2099: also split — "twenty ten", "twenty twenty-one", ... -->
    <SplitRange from="2010" to="2099" />
</YearFormat>
```

| Attribute | Description |
|-----------|-------------|
| `hundredWord` | Word appended when the year is a round century (e.g. `"hundred"` → `"nineteen hundred"`). |
| `zeroConnector` | Connector inserted before single-digit remainders (e.g. `"oh"` → `"twenty oh five"`). |
| `beforeChristSuffix` | Suffix appended to negative years instead of the `minus` template (e.g. `"BC"` → `ConvertYear(-44)` → `"forty-four BC"` instead of `"minus forty-four"`). |

`<SplitRange from="N" to="M" />` declares an inclusive range `[N, M]` of year values that
use the split algorithm. Multiple ranges may be declared; ranges outside the list fall back
to `Convert(year)`.

---

### `<Trigger>` — pipeline hooks

`<Trigger>` elements apply text replacements at a specific moment in the conversion pipeline,
optionally conditioned on active morphological variant values.

#### Execute positions — `executeAt`

| Value | When it fires | Sees |
|-------|--------------|------|
| `"group"` | After `ConvertGroup` for each digit group | Digit text only (no scale name yet) |
| `"group(N)"` | Same, but only for group N (0 = units, 1 = thousands, 2 = millions, …) | Digit text |
| `"group(N,M,…)"` | Same, restricted to the listed group indices | Digit text |
| `"groupWithScale"` | After per-group `Replacements`, before pushing | Digit+scale text |
| `"groupWithScale(N)"` | Same, restricted to group N | Digit+scale text |
| `"end"` | After full assembly, `AdjustFunction`, and `ApplyVariantRules` | Final assembled text |

> **Warning**: `group` and `groupWithScale` triggers also fire during `ConvertOrdinal`. If the trigger
> modifies a word that an ordinal word-rule targets (e.g. it replaces `"ein"` with something else),
> the ordinal transform may not match. Use `"end"` for post-ordinal corrections.

#### `<Replace>` — replacement rule

Each trigger contains one or more `<Replace>` elements. They are applied in declaration order,
independently of each other — each selects exactly one form and applies it once.

```xml
<!-- Simple unconditional replacement -->
<Trigger executeAt="end">
    <Replace from="et " to="&amp; " />
</Trigger>

<!-- Regex replacement -->
<Trigger executeAt="group(0)">
    <Replace from="^one$" to="uno" regex="true" />
</Trigger>
```

#### Variant-conditioned replacements

When a `<Replace>` has `<Variant>` children, the most specific matching form is selected
(same best-match algorithm as ordinal variants). The `to=` attribute becomes the unconditional
default used when no form matches the active variant query.

**Positional forms** — one form per dimension value in declaration order:

```xml
<Trigger executeAt="end">
    <!-- genus dimension: maskulin=eins, feminin=eine, neutrum=eins -->
    <Replace from="ein$" regex="true" to="eins">
        <Variant type="genus" forms="eins,eine,eins" />
    </Replace>
</Trigger>
```

**Single-value shorthand** with `value=` — overrides exactly one dimension value:

```xml
<Trigger executeAt="end">
    <!-- default "eins", but feminin → "eine" -->
    <Replace from="ein$" regex="true" to="eins">
        <Variant type="genus" variant="feminin" value="eine" />
    </Replace>
</Trigger>
```

**No default** — when `to=` is absent and no variant matches, the replacement is skipped entirely
(the regex is never evaluated):

```xml
<Trigger executeAt="group(0)">
    <!-- fires only for feminin; other variants are untouched -->
    <Replace from="uno" regex="false">
        <Variant type="gender" variant="feminin" value="una" />
    </Replace>
</Trigger>
```

#### `<Replace>` attributes

| Attribute | Required | Description |
|-----------|----------|-------------|
| `from` | ✓ | Text or regex pattern to match. |
| `to` | | Explicit unconditional fallback. May contain backreferences (`$1`, `${name}`) when `regex="true"`. When absent, an unmatched query performs no replacement. |
| `regex` | | `"true"` to treat `from` as a .NET regular expression. Default: `"false"` (literal match). |

The `<Variant>` children use the same `FormVariantType` syntax as `<Replacement>`, `<Ordinal>`,
and `<OrdinalException>`:

| Attribute | Description |
|-----------|-------------|
| `type` | Dimension name (canonical or `localName`). |
| `variant` | Single value — marks this node as a constraint leaf. Used with `value=` or nested children. |
| `forms` | Positional comma-separated forms, one per dimension value in declaration order. Leaf node syntax. |
| `value` | Single output form for the specific `variant` named by `variant=`. Shorthand for single-value overrides. |

---

## Related packages

- `omy.Utils` — contains `NumberToStringConverter`, `NumberToStringConverterOptions`, and all built-in culture XML configurations.
- `omy.Utils.Mathematics` — provides `MathEx.RoundToSignificantDigits` used by the significant-digits precision overload.


[Versioned API documentation](https://warny.github.io/All-purpose-Utilities/v2.0.0-rc.2/)
# Configuration validation and runtime contracts

Configuration completeness is validated only after all `baseOn` inheritance has been resolved.
Diagnostics identify the culture and logical configuration path. Languages without a large-number
scale must declare `maxNumber` below `10^groupSize`; finite scales likewise require a bound that can
be named by the configured scale tables.

`RegisterConfigurations` rejects normalized culture collisions by default. Use the overload taking
`DuplicateCulturePolicy` to keep an existing converter or atomically replace it. Runtime variants use
strict `dimension=value` syntax and reject unknown dimensions, values, and duplicates.

Language-specific finalization is applied to the complete public conversion result. Ordinal and
multiplicative bodies, including configured exceptions, pass through adjustment, final triggers, and
the language finalizer before their type prefix and sign are applied.

## Deterministic rule precedence

Variant constraints are normalized to canonical dimension names and compared case-insensitively. **Specificity** is the number of canonical dimensions constrained by a rule. **Priority** is any signed 32-bit integer and defaults to `0`; specificity is always considered before priority.

Unique ordinal and trigger-form selection uses specificity descending, then priority descending. An equal-ranked pair whose constraints do not contradict each other is rejected because both can match the same query. For example, `gender=female` and `number=plural` intersect and need different priorities, while `gender=female` and `gender=male` are mutually exclusive.

Cumulative `VariantRule` transformations run by specificity ascending and then priority ascending, so more specific transformations refine general ones and a higher priority runs later at equal specificity. Replacement order inside one rule remains an intentional sequence.

```xml
<OrdinalVariants>
    <Variant type="gender" variant="female" priority="100" suffix="th" />
</OrdinalVariants>
```

The `priority` attribute is optional and accepts the full `xs:int` range. During `baseOn`, inherited rules retain their priorities and child rules remain distinct. The merged configuration is rejected if inheritance creates an unresolved equal-rank intersection. Declaration order is never a tie-breaker in 2.0.

## Idiomatic clock-time conversion

`Convert(TimeOnly)` remains the exact hours/minutes/seconds representation configured by
`TimeUnits`. `ConvertClockTime(TimeOnly)` is a separate, intentionally idiomatic API: it rounds to
the configured minute step and applies a clock-position rule. `ConvertClockTime(DateTime)` follows
the existing `Convert(DateTime)` contract by including the date when `DateFormat` is available; a
rounding carry past 23:59 therefore uses the following date.

```csharp
var french = NumberToStringConverter.GetConverter("fr-FR");
string exact = french.Convert(new TimeOnly(1, 27));       // une heure vingt sept minutes
string clock = french.ConvertClockTime(new TimeOnly(1, 28)); // une heure et demie
```

A `ClockTime` section contains a positive minute `step` that divides 60 and complete, non-overlapping rules for
every reachable source-hour/minute position. Nearest rounding is used and an exact half-step rounds forward. Each
`range` uses an `IntRange<int>`-based syntax restricted in XML to comma-separated non-negative
values and inclusive ranges. Programmatic `IntRange<int>` values retain their complete native
syntax. Validation is deliberately strict: every member of a range must be in `0..59` and
divisible by `step`, so use `range="5,10"`, not `range="5-10"`, for a five-minute clock.

```xml
<ClockTime step="5" hourCycle="24">
  <Rule range="0" hourOffset="0" hourForm="timeUnit" pattern="{hour}" />
  <Rule range="5,10" hourOffset="0" hourForm="timeUnit"
        hourForceVariants="gender=feminin"
        amountReference="0" amountDirection="after" pattern="{hour} {amount}" />
  <Rule range="55" hourOffset="1" hourForm="timeUnit"
        amountReference="60" amountDirection="before" pattern="{hour} moins {amount}" />
</ClockTime>
```

`displayHourRange` optionally conditions a rule on the numeric hour that `{hour}` would render.
It is evaluated independently for each candidate after that rule's `hourOffset` and after
`hourCycle` projection (`1..12` for a 12-hour cycle, `0..23` for a 24-hour cycle), but before any
`SpecialHourRule` replaces the hour text. Consequently, multiple rules may share the same minute
`range` when their display-hour conditions are disjoint. The converter validates every reachable
source-hour/minute pair and snapshots both ranges while constructing a precompiled 1,440-entry
lookup; `ConvertClockTime` performs a direct O(1) lookup rather than searching rules.

```xml
<Rule range="30" displayHourRange="1" hourOffset="0"
      hourForm="cardinal" pattern="special one-thirty" />
<Rule range="30" displayHourRange="2" hourOffset="0"
      hourForm="cardinal" pattern="special two-thirty" />
<Rule range="30" displayHourRange="3-12" hourOffset="0"
      hourForm="cardinal" pattern="half {hour}" />
```

`hourOffset` selects the reference hour modulo 24. Special-hour rules match that 24-hour reference
first, so midnight and noon remain distinguishable. If no special hour applies, `hourCycle="12"`
projects `0` to `12`, `13` to `1`, and so on; `hourCycle="24"` (the default) keeps the reference
unchanged. `hourForm` is `cardinal`, `ordinal`, or
`timeUnit`; all reuse the converter's existing numeral, ordinal, unit-form, forced-variant, and
language-finalization pipelines. `{amount}` is optional. When present, both `amountReference` and
`amountDirection` are required: `before` computes `reference - minute`, while `after` computes
`minute - reference`. Literal wording—including quarters, halves, or fractions such as “un tiers”—
belongs in `pattern`; the engine only recognizes `{hour}` and `{amount}`.
Patterns are validated against that strict whitelist and compiled once through
`StringFormatBuilder`; inserted hour and amount text is never reparsed as template content.
The optional `specialHourPattern` uses the same placeholders and is selected only when a
configured `SpecialHourRule` actually replaces `{hour}`. This lets English use
`pattern="{hour} o'clock" specialHourPattern="{hour}"` for `one o'clock` versus `noon`;
disabling special-hour replacement continues to use the ordinary `pattern`.

Each rule may independently constrain the two numeric constituents with
`hourForceVariants` and `amountForceVariants`. They use the same comma-separated
`dimension=value` syntax as other forced variants, including declared dimension aliases. Values
are parsed and canonicalized once when the converter is built. For cardinal and ordinal forms the
precedence is language defaults, caller variants, then the rule's forcing. For `timeUnit`, the
unit's own forcing is inserted before the rule forcing: defaults < caller < `TimeUnit` <
`ClockTimeRule`. Overlaying is dimension-by-dimension, so an unforced caller dimension survives.
For example, a rule can render an ordinal hour as feminine genitive while independently rendering
its minute amount as a genitive cardinal. A forced ordinal counts as explicit variant intent even
when the caller supplies no variants. Forcing is applied only when a numeric `{hour}` is rendered;
a matching `SpecialHourRule` remains a literal lexical replacement. Configuration is rejected if
a forcing or amount calculation is declared without its corresponding placeholder.

`TimeUnit.Count1Form` remains the legacy literal count-one form for ordinary duration and exact-time
conversion. A non-empty `ClockTimeRule.HourForcedVariants` is more specific, so `hourForm="timeUnit"`
bypasses that literal and renders the numeral from the effective grammatical query. This makes a
rule such as German `genus=feminin,kasus=dativ` produce `einer Stunde`, rather than silently falling
back to the configured nominative `eine Stunde`.

Configured `SpecialHourRule` values are matched against the rule's 24-hour reference. An
exact-only rule applies only at rounded minute zero; a rule with `WholeHour=true` also applies to
an offset reference such as 11:55 → noon. Pass a `ClockTimeConversionOptions` plus an
`IEnumerable<string>` of variants to explicitly disable replacement. The three-argument overload
avoids introducing a new ambiguous `ConvertClockTime(time, default)` call. Variants propagate to
hour, amount, ordinal, and time-unit rendering. `TimeSpan` has no clock-time API because durations
do not have clock-face semantics.

With XML `baseOn`, an absent `ClockTime` inherits the complete parent section; a present section
replaces it as a whole. Rules are never merged individually. Programmatic options are validated
and snapshotted into the hour/minute lookup during converter construction.

`SupportsOrdinals` includes ordinal implementations supplied solely by
`IOrdinalLanguageSpecifics`. If neither XML ordinal rules nor such a plugin is available, every
concrete `ConvertOrdinal` overload fails closed with `NotSupportedException` rather than returning
an unchanged cardinal. A plugin may return `false` to use configured XML ordinal rules; when no
declarative ordinal fallback exists, declining a value also throws `NotSupportedException` instead
of silently returning its cardinal representation. This includes long values outside the default
int-only plugin bridge.

Versioned API documentation: https://warny.github.io/All-purpose-Utilities/v2.0.0-rc.2/
