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
| EN, EN-uk, EN-us | English | ✓ | — |
| FR, FR-fr, FR-ca | French | ✓ | gender (masculin/feminin) |
| FR-be, FR-ch | Belgian/Swiss French | — | — |
| DE | German | — | — |
| ES | Spanish | — | — |
| IT | Italian | — | — |
| PT | Portuguese | — | — |
| PL | Polish | — | — |
| NL | Dutch | — | — |
| RU | Russian | — | — |
| AR | Arabic | — | — |
| HE | Hebrew | — | — |
| ZH | Chinese | — | — |
| JA | Japanese | — | — |
| KO | Korean | — | — |
| HI | Hindi | — | — |
| EL | Greek | — | — |
| FI | Finnish | — | — |
| CA | Catalan | — | — |
| EU | Basque | — | — |
| GL | Galician | — | — |
| ZU | Zulu | — | — |
| EE | Ewe | — | — |
| WO | Wolof | — | — |

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

fr.ConvertOrdinal(1);    // "premier"       ← exception entière
fr.ConvertOrdinal(2);    // "deuxième"
fr.ConvertOrdinal(5);    // "cinquième"     ← règle de mot : cinq → cinquième
fr.ConvertOrdinal(9);    // "neuvième"      ← règle de mot : neuf → neuvième
fr.ConvertOrdinal(21);   // "vingt et unième"
fr.ConvertOrdinal(1000); // "millième"      ← stripTrailingE + suffix ième
```

---

## Morphological variants

Many languages inflect numbers for gender or grammatical case. Variants are declared per language in the XML configuration as named dimensions with ordered values. **The first declared value is the default** — calling `Convert` without parameters automatically uses it.

### French — genre grammatical

French has one variant dimension: **gender** (`masculin` / `feminin`).

```csharp
NumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");

// Sans paramètre → masculin (première valeur = défaut)
fr.Convert(1);   // "un"
fr.Convert(21);  // "vingt et un"

// Féminin explicite
fr.Convert(1,  "gender=feminin");   // "une"
fr.Convert(21, "gender=feminin");   // "vingt et une"
fr.Convert(31, "gender=feminin");   // "trente et une"
fr.Convert(61, "gender=feminin");   // "soixante et une"

// Le remplacement s'applique uniquement au DERNIER mot
// "million" n'est pas remplacé même en féminin
fr.Convert(1_000_000, "gender=feminin");    // "un million"
fr.Convert(1_000_021, "gender=feminin");    // "un million vingt et une"
```

### Lister les variantes disponibles pour une langue

```csharp
NumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");

foreach (var dimension in fr.VariantDimensions)
{
    Console.WriteLine($"{dimension.Name}: {string.Join(", ", dimension.Values)}");
    Console.WriteLine($"  défaut: {dimension.DefaultValue}");
}
// gender: masculin, feminin
//   défaut: masculin
```

Via l'interface `INumberToStringConverter` :

```csharp
INumberToStringConverter converter = NumberToStringConverter.GetConverter("FR");
bool supportsGender = converter.VariantDimensions
    .Any(d => d.Name.Equals("gender", StringComparison.OrdinalIgnoreCase));
```

### Architecture — variantes multi-dimensionnelles

Pour des langues avec plusieurs dimensions (ex. allemand : genre × cas), la configuration XML déclare chaque dimension et les règles de remplacement pour chaque combinaison :

```xml
<Variants>
  <!-- valeurs ordonnées : la première est le défaut -->
  <Dimension name="genus"  values="maskulin,feminin,neutrum" />
  <Dimension name="kasus"  values="nominativ,akkusativ,dativ,genitiv" />

  <!-- Variant appliqué quand genus=feminin (quelle que soit la valeur de kasus) -->
  <Variant genus="feminin">
    <Replacement oldValue="ein" newValue="eine" scope="LastWord" />
  </Variant>

  <!-- Variant plus spécifique : kasus=akkusativ ET genus=maskulin -->
  <Variant kasus="akkusativ" genus="maskulin">
    <Replacement oldValue="ein" newValue="einen" scope="LastWord" />
  </Variant>
</Variants>
```

```csharp
// Hypothétique — si la configuration allemande déclarait ces variantes :
de.Convert(1);                              // "eins"    (défaut: nominativ, maskulin)
de.Convert(1, "genus=feminin");             // "eine"
de.Convert(1, "kasus=akkusativ", "genus=maskulin"); // "einen"
```

**Règles de cascade** : les variantes avec moins de contraintes sont appliquées avant celles avec plus de contraintes. Une variante à 2 dimensions peut surcharger le résultat d'une variante à 1 dimension.

**Scope `LastWord`** : le remplacement ne s'applique que si `oldValue` correspond exactement au dernier mot du résultat (séparé par un espace ou un tiret). Cela empêche de modifier "ein" dans "eine Million" alors qu'on veut seulement infliger la règle sur l'unité terminale.

**Dimensions inconnues** : si le code appelant passe une dimension non déclarée pour une langue, elle est ignorée silencieusement — le résultat est identique à un appel sans variante.

---

## Monnaie

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

`SubunitDigits` (défaut 2) contrôle le nombre de décimales pour les sous-unités.

---

## Personnalisation via `NumberToStringConverterOptions`

Cloner et modifier un convertisseur existant :

```csharp
var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
{
    AdjustFunction = text => text.ToUpperInvariant(),
    MaxNumber      = new BigInteger(999_999_999),
};
var converter = new NumberToStringConverter(options);

converter.Convert(42);    // "FORTY-TWO"
converter.Convert(1_000_000_000); // throws ArgumentOutOfRangeException
```

---

## Enregistrer des configurations XML supplémentaires

```csharp
// Charger une ou plusieurs configurations XML au démarrage
NumberToStringConverter.RegisterConfigurations([myXmlConfig]);

// Les cultures en doublon sont ignorées silencieusement (première inscription gagnante)
NumberToStringConverter.RegisterConfigurations([xmlA, xmlA]); // ne lève pas d'exception
```

---

## `INumberToStringLanguageSpecifics`

Hook de post-traitement appliqué **après** les variantes, en dernière étape du pipeline. Utile quand une règle grammaticale nécessite un contexte qui ne peut pas être exprimé par de simples remplacements.

```csharp
public class UpperCaseSpecifics : INumberToStringLanguageSpecifics
{
    public string FinalizeWriting(string languageIdentifier, string text)
        => text.ToUpperInvariant();
}
```

### Enregistrement factory

Enregistrer une instance nommée évite la recherche par réflexion au moment de la désérialisation XML :

```csharp
NumberToStringConverter.RegisterLanguageSpecifics(
    nameof(MyLanguageSpecifics),
    new MyLanguageSpecifics());
```

La valeur du nœud `<LanguageSpecifics>` dans le XML doit correspondre au nom (complet ou court) passé ici.

### `GermanNumberToStringLanguageSpecifics`

Fourni dans le package. Il corrige les formes "ein" → "eins" (nombre isolé) et "ein Million" → "eine Million" :

```csharp
NumberToStringConverter de = NumberToStringConverter.GetConverter("DE");

de.Convert(1);          // "eins"        ← ein isolé → eins
de.Convert(21);         // "einundzwanzig"
de.Convert(1_000_000);  // "eine Million" ← ein + nom féminin
de.Convert(2_000_000);  // "zwei Millionen"
```

---

## Pipeline de conversion

```
number
  → ConvertRaw          (groupes de chiffres + exceptions + remplacements de langue)
  → AdjustFunction      (transformation optionnelle fournie par l'utilisateur)
  → ApplyVariantRules   (remplacements morphologiques du moins au plus spécifique)
  → FinalizeWriting     (INumberToStringLanguageSpecifics)
  → wrapping signe      (template Minus si négatif)
```

Les variantes sont appliquées *avant* `FinalizeWriting`. Cela garantit que pour l'allemand, le variant peut agir sur la forme brute "ein" avant que `GermanNumberToStringLanguageSpecifics` ne la transforme en "eins".

---

## Related packages

- `omy.Utils` — contains `NumberToStringConverter`, `NumberToStringConverterOptions`, and all built-in culture XML configurations.
