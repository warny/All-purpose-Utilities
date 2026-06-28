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
| FR-be, FR-ch | Belgian/Swiss French | — | gender (masculin/feminin) |
| DE | German | — | genus (maskulin/feminin/neutrum) × kasus (nominativ/akkusativ/dativ/genitiv) |
| ES | Spanish | — | gender (masculino/femenino) |
| IT | Italian | — | gender (maschile/femminile) |
| PT | Portuguese | — | gender (masculino/feminino) |
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
| FI | Finnish | — | sijamuoto (nominatiivi/partitiivi/genetiivi) |
| CA | Catalan | — | gender (masculí/femení) |
| EU | Basque | — | — (pas de genre en basque) |
| GL | Galician | — | gender (masculino/feminino) |
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

### Espagnol — genre et centaines

En espagnol, `uno` (1) et les centaines composées `-cientos` varient en genre.

```csharp
NumberToStringConverter es = NumberToStringConverter.GetConverter("ES");

es.Convert(1);                    // "uno"
es.Convert(1,   "gender=femenino"); // "una"
es.Convert(200, "gender=femenino"); // "doscientas"
es.Convert(500, "gender=femenino"); // "quinientas"
es.Convert(900, "gender=femenino"); // "novecientas"
```

> **Limitation** : les formes composées soudées sans espace (`veintiuno`, `treintauno`) ne sont
> pas converties — la règle `LastWord` exige un espace ou un tiret avant `uno`. Corriger les
> `buildStrings` de la configuration (`"treinta y *"` au lieu de `"treinta*"`) résoudrait ce point.

### Portugais — genre avec espaces, unités et centaines

Le portugais utilise des espaces dans tous ses composés (`vinte e um`), ce qui rend
la règle `LastWord` efficace sur l'ensemble des formes. `um` et `dois` varient, ainsi que
toutes les centaines (sauf 100 `cem`/`cento`).

```csharp
NumberToStringConverter pt = NumberToStringConverter.GetConverter("PT");

pt.Convert(1,   "gender=feminino"); // "uma"
pt.Convert(2,   "gender=feminino"); // "duas"
pt.Convert(21,  "gender=feminino"); // "vinte e uma"
pt.Convert(22,  "gender=feminino"); // "vinte e duas"
pt.Convert(200, "gender=feminino"); // "duzentas"
pt.Convert(201, "gender=feminino"); // "duzentas e uma"
pt.Convert(202, "gender=feminino"); // "duzentas e duas"

// Le multiplicateur avant "mil" reste masculin (dernier mot = "mil")
pt.Convert(2_000, "gender=feminino"); // "dois mil"  (limitation)
```

### Italien — seul `uno` varie

En italien, les centaines (`duecento`, `trecento`…) sont invariables en genre.
Seul `uno` → `una` change.

```csharp
NumberToStringConverter it = NumberToStringConverter.GetConverter("IT");

it.Convert(1, "gender=femminile"); // "una"
it.Convert(100); // "cento"         ← invariable
it.Convert(200); // "duecento"      ← invariable
```

> **Limitation** : les composés soudés (`ventiuno`, `trentuno`…) ne sont pas convertis,
> pour la même raison que l'espagnol.

### Catalan — tirets comme frontières de mots

Le catalan utilise des tirets dans ses composés (`vint-i-un`, `trenta-un`…).
Un tiret est une frontière de mot pour `LastWord`, donc la règle s'applique correctement
aux nombres composés. Seul `dos-cents` (200) a une forme féminine parmi les centaines.

```csharp
NumberToStringConverter ca = NumberToStringConverter.GetConverter("CA");

ca.Convert(1,   "gender=femení"); // "una"
ca.Convert(2,   "gender=femení"); // "dues"
ca.Convert(21,  "gender=femení"); // "vint-i-una"   ← tiret = frontière de mot ✓
ca.Convert(22,  "gender=femení"); // "vint-i-dues"
ca.Convert(31,  "gender=femení"); // "trenta-una"
ca.Convert(200, "gender=femení"); // "dues-centes"
ca.Convert(201, "gender=femení"); // "dues-centes una"
```

### Galicien — comme le portugais

Le galicien utilise des espaces (`vinte e un`) et suit une logique proche du portugais.
`un` → `unha`, `dous` → `dúas`, et seul `douscentos` (200) a une forme féminine.

```csharp
NumberToStringConverter gl = NumberToStringConverter.GetConverter("GL");

gl.Convert(1,   "gender=feminino"); // "unha"
gl.Convert(2,   "gender=feminino"); // "dúas"
gl.Convert(21,  "gender=feminino"); // "vinte e unha"
gl.Convert(22,  "gender=feminino"); // "vinte e dúas"
gl.Convert(200, "gender=feminino"); // "douscentas"
gl.Convert(201, "gender=feminino"); // "douscentas unha"
```

### Basque (EU) — pas de genre grammatical

Le basque est une langue isolat sans genre grammatical : aucune variante morphologique
n'est disponible pour cette langue.

### Finnois — cas grammaticaux (sijamuoto)

Le finnois n'a pas de genre grammatical mais dispose de 15 cas. Trois cas sont
implémentés : le nominatif (défaut), le partitif (`partitiivi`) et le génitif
(`genetiivi`).

**Particularités d'implémentation :**

- Les dizaines composées (`kaksikymmentä`…) et centaines composées (`kaksisataa`…)
  sont déjà sous une forme compatible avec le partitif dans la configuration :
  seules les unités et les mots d'échelle seuls ont besoin de règles `LastWord`.
- Au génitif, les dizaines et centaines composées changent complètement
  (`kaksikymmentä` → `kahdenkymmenen`) via `Anywhere`.
- `seitsemän`, `kahdeksan`, `yhdeksän` (7, 8, 9) sont invariables au génitif.

```csharp
NumberToStringConverter fi = NumberToStringConverter.GetConverter("FI");

// Nominatif (défaut)
fi.Convert(1);    // "yksi"
fi.Convert(21);   // "kaksikymmentä yksi"
fi.Convert(100);  // "sata"

// Partitif
fi.Convert(1,   "sijamuoto=partitiivi"); // "yhtä"
fi.Convert(2,   "sijamuoto=partitiivi"); // "kahta"
fi.Convert(5,   "sijamuoto=partitiivi"); // "viittä"
fi.Convert(10,  "sijamuoto=partitiivi"); // "kymmentä"
fi.Convert(11,  "sijamuoto=partitiivi"); // "yhtätoista"
fi.Convert(21,  "sijamuoto=partitiivi"); // "kaksikymmentä yhtä"
fi.Convert(100, "sijamuoto=partitiivi"); // "sataa"
fi.Convert(201, "sijamuoto=partitiivi"); // "kaksisataa yhtä"

// Génitif
fi.Convert(2,   "sijamuoto=genetiivi"); // "kahden"
fi.Convert(20,  "sijamuoto=genetiivi"); // "kahdenkymmenen"
fi.Convert(21,  "sijamuoto=genetiivi"); // "kahdenkymmenen yhden"
fi.Convert(200, "sijamuoto=genetiivi"); // "kahdensadan"
fi.Convert(221, "sijamuoto=genetiivi"); // "kahdensadan kahdenkymmenen yhden"
fi.Convert(11,  "sijamuoto=genetiivi"); // "yhdentoista"
```

> **Limitation** : dans un nombre composé comme `sata yksi` (101), le mot `sata` (cent)
> n'est pas converti en `sadan` au génitif, car il n'est ni en dernière position, ni
> seul dans le texte. Un `Anywhere "sata"→"sadan"` corromprait les formes composées
> comme `kaksisataa`. De même, `yksi tuhat` (1000) produit `yksi tuhatta` au partitif.

### Français belge/suisse — même genre, mots différents

FR-be et FR-ch utilisent septante/huitante/nonante au lieu de soixante-dix/quatre-vingts/quatre-vingt-dix,
mais la règle de genre est identique à FR : la dimension `gender` (masculin/féminin) est disponible.

```csharp
NumberToStringConverter frBe = NumberToStringConverter.GetConverter("FR-be");

frBe.Convert(71);                  // "septante et un"
frBe.Convert(71, "gender=feminin"); // "septante et une"
frBe.Convert(81, "gender=feminin"); // "huitante et une"
frBe.Convert(91, "gender=feminin"); // "nonante et une"

// "un million" → dernier mot = "million" → pas de remplacement
frBe.Convert(1_000_000, "gender=feminin"); // "un million"
```

### Allemand — genus × kasus

L'allemand est le seul chiffre variable en déclinaison : seul `ein` (1) prend des formes différentes.
Les composés comme `einundzwanzig` (21) sont invariables.

```csharp
NumberToStringConverter de = NumberToStringConverter.GetConverter("DE");

// Défaut (nominatif masculin) — GermanSpecifics: "ein" → "eins"
de.Convert(1);   // "eins"

// Variations de genre et de cas
de.Convert(1, "genus=feminin");                       // "eine"
de.Convert(1, "kasus=akkusativ", "genus=maskulin");   // "einen"
de.Convert(1, "kasus=akkusativ", "genus=feminin");    // "eine"
de.Convert(1, "kasus=dativ",     "genus=maskulin");   // "einem"
de.Convert(1, "kasus=dativ",     "genus=neutrum");    // "einem"
de.Convert(1, "kasus=dativ",     "genus=feminin");    // "einer"
de.Convert(1, "kasus=genitiv",   "genus=maskulin");   // "eines"
de.Convert(1, "kasus=genitiv",   "genus=neutrum");    // "eines"
de.Convert(1, "kasus=genitiv",   "genus=feminin");    // "einer"

// Les composés ne sont pas déclinés
de.Convert(21, "genus=feminin");  // "einundzwanzig"  (inchangé)

// "eine Million" : GermanSpecifics corrige "ein Million" → "eine Million" indépendamment des variantes
de.Convert(1_000_000);  // "eine Million"
```

Tableau complet des formes de `ein` :

| kasus \ genus | maskulin | feminin | neutrum |
|--------------|----------|---------|---------|
| Nominativ    | eins*    | eine    | eins*   |
| Akkusativ    | einen    | eine    | eins*   |
| Dativ        | einem    | einer   | einem   |
| Genitiv      | eines    | einer   | eines   |

\* `GermanNumberToStringLanguageSpecifics` convertit la forme brute `ein` en `eins` (forme de comptage).
Pour akkusatif/nominatif neutrum, la forme adjectivale `ein` (sans -s) et la forme de comptage `eins` sont
indiscernables sans contexte syntaxique ; le système retourne `eins` dans les deux cas.

### Découverte des variantes disponibles

```csharp
NumberToStringConverter de = NumberToStringConverter.GetConverter("DE");

foreach (var dim in de.VariantDimensions)
    Console.WriteLine($"{dim.Name}: {string.Join(", ", dim.Values)}  (défaut: {dim.DefaultValue})");
// genus: maskulin, feminin, neutrum  (défaut: maskulin)
// kasus: nominativ, akkusativ, dativ, genitiv  (défaut: nominativ)
```

### Architecture — variantes multi-dimensionnelles et cascade

La configuration XML déclare chaque dimension, puis les règles de remplacement de la moins spécifique à la plus spécifique. L'ordre de déclaration des règles à égalité de contraintes est important : une règle transforme le texte en séquence, et la suivante voit le résultat de la précédente.

```xml
<Variants>
  <Dimension name="genus"  values="maskulin,feminin,neutrum" />
  <Dimension name="kasus"  values="nominativ,akkusativ,dativ,genitiv" />

  <!-- 1 contrainte — genus=feminin déclaré EN PREMIER -->
  <Variant genus="feminin">
    <Replacement oldValue="ein" newValue="eine" scope="LastWord" />
  </Variant>
  <!-- dativ/genitiv maskulin+neutrum : "ein" encore présent si genus≠feminin -->
  <Variant kasus="dativ">
    <Replacement oldValue="ein" newValue="einem" scope="LastWord" />
  </Variant>
  <Variant kasus="genitiv">
    <Replacement oldValue="ein" newValue="eines" scope="LastWord" />
  </Variant>

  <!-- 2 contraintes — surcharge les résultats des règles à 1 contrainte -->
  <Variant kasus="akkusativ" genus="maskulin">
    <Replacement oldValue="ein" newValue="einen" scope="LastWord" />
  </Variant>
  <!-- Pour dativ+feminin : genus=feminin a déjà changé "ein"→"eine",
       la règle à 2 contraintes cherche donc "eine" et non "ein" -->
  <Variant kasus="dativ" genus="feminin">
    <Replacement oldValue="eine" newValue="einer" scope="LastWord" />
  </Variant>
  <Variant kasus="genitiv" genus="feminin">
    <Replacement oldValue="eine" newValue="einer" scope="LastWord" />
  </Variant>
</Variants>
```

**Règles de cascade** : les variantes avec moins de contraintes sont appliquées avant celles avec plus de contraintes. Au sein du même niveau de spécificité, l'ordre de déclaration est préservé — ce qui permet de composer les transformations.

**Scope `LastWord`** : le remplacement ne s'applique que si `oldValue` correspond exactement au dernier mot du résultat (séparé par un espace ou un tiret). Cela empêche de modifier `ein` à l'intérieur de `einundzwanzig` ou dans `ein million` quand le dernier mot est `million`.

**Dimensions inconnues** : si le code appelant passe une dimension non déclarée pour une langue, elle est ignorée silencieusement — le résultat est identique à un appel sans variante.

### Langues sans variantes morphologiques

Les langues suivantes n'ont pas de variantes déclarées, soit parce que leur morphologie
numérale est invariable dans les contextes courants, soit parce que la distinction
morphologique n'est pas applicable.

| Code | Langue | Raison |
|------|--------|--------|
| EN | English | Les nombres anglais sont invariables (pas de genre ni de cas) |
| KO | Korean | Les nombres sont invariables en coréen |
| HI | Hindi | Pas de variantes déclarées (le hindi a un genre mais les nombres sont souvent invariables en usage courant) |
| EL | Greek | Pas de variantes déclarées |
| EU | Basque | Langue isolat sans genre grammatical |
| AR | Arabic | Pas de variantes déclarées |
| ZH | Chinese | Pas de variantes (pas de flexion) |
| JA | Japanese | Pas de variantes (pas de flexion) |
| RU | Russian | Pas de variantes déclarées (le russe a des cas mais ils ne sont pas encore implémentés) |
| PL | Polish | Pas de variantes déclarées |
| CS | Czech | Pas de variantes déclarées |
| HU | Hungarian | Pas de variantes déclarées |

Pour toutes ces langues, `VariantDimensions` renvoie une liste vide et tout paramètre
passé à `Convert()` est ignoré sans erreur.

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

## Configuration XML

Les configurations des langues sont des fichiers XML dont la structure est décrite par
`NumberConvertionConfiguration.xsd` (namespace `Utils/NumberConvertionConfiguration.xsd`).

### Structure générale

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
    <Language groupSize="3" separator=" " groupSeparator=""
              zero="zéro" minus="moins *"
              decimalSeparator="virgule" fractionSeparator="sur">

        <Culture>FR</Culture>      <!-- code 2 lettres -->
        <Culture>FR-fr</Culture>   <!-- code région optionnel -->

        <Groups>…</Groups>
        <NumberScale>…</NumberScale>
        <Replacements>…</Replacements>   <!-- optionnel -->
        <Exceptions>…</Exceptions>       <!-- optionnel -->
        <LanguageSpecifics>…</LanguageSpecifics> <!-- optionnel -->
        <Fractions>…</Fractions>         <!-- optionnel -->
        <Ordinals suffix="…">…</Ordinals>        <!-- optionnel -->
        <Variants>…</Variants>           <!-- optionnel -->

    </Language>
</Numbers>
```

Un même fichier peut contenir plusieurs éléments `<Language>`. Plusieurs éléments
`<Culture>` sur une même langue enregistrent le même convertisseur sous plusieurs codes.

### Attributs de `<Language>`

| Attribut | Requis | Description |
|----------|--------|-------------|
| `groupSize` | ✓ | Nombre de chiffres par groupe (toujours 3 pour le millier). |
| `separator` | ✓ | Séparateur de mots à l'intérieur d'un groupe (généralement `" "`). |
| `groupSeparator` | ✓ | Texte entre groupes (ex. `","` en anglais, `""` en français). |
| `zero` | ✓ | Texte pour la valeur 0. |
| `minus` | ✓ | Template pour les négatifs ; `*` est remplacé par la valeur absolue. |
| `decimalSeparator` | | Mot entre partie entière et décimale (ex. `"point"`, `"virgule"`). |
| `fractionSeparator` | | Connecteur numérateur/dénominateur pour les fractions (ex. `"sur"`). |
| `maxNumber` | | Valeur maximale acceptée ; au-delà, `ArgumentOutOfRangeException`. |
| `baseOn` | | Réservé (héritage futur). |

---

### `<Groups>` — tables de chiffres

Chaque `<Group level="N">` déclare comment les chiffres 0–9 sont écrits à la position N
d'un groupe : `level="1"` = unités, `level="2"` = dizaines, `level="3"` = centaines.

Chaque `<Digit digit="N" string="…" buildString="…"/>` :
- `string` — texte quand ce chiffre est seul dans sa position.
- `buildString` — template avec `*` remplacé par le sous-groupe inférieur.

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

### `<NumberScale>` — noms des grandes puissances

```xml
<NumberScale firstLetterUpperCase="false" voidGroup="ni" groupSeparator="lli" startIndex="0">

    <!-- Noms fixes pour les premiers niveaux -->
    <StaticNames>
        <Scale value="0" string=""/>        <!-- groupe des unités (pas de suffixe) -->
        <Scale value="1" string="mille"/>   <!-- 10^3 -->
        <!-- Les suivants peuvent aussi être listés : million, milliard, … -->
    </StaticNames>

    <!-- Suffixes pour les niveaux générés dynamiquement (Latin + suffixe) -->
    <Suffixes>
        <Suffix>on(s)</Suffix>    <!-- million, billion, trillion… -->
        <Suffix>ard(s)</Suffix>   <!-- milliard, billiard…         -->
    </Suffixes>

    <!-- Tables de préfixes optionnelles (surcharge les valeurs latines par défaut) -->
    <Scale0Prefixes>…</Scale0Prefixes>
    <UnitsPrefixes>…</UnitsPrefixes>
    <TensPrefixes>…</TensPrefixes>
    <HundredsPrefixes>…</HundredsPrefixes>

</NumberScale>
```

`firstLetterUpperCase="true"` capitalise les noms de scale générés (utile en allemand :
"Million", "Milliarde"). La chaîne `"(s)"` dans les noms est un marqueur de pluriel.

---

### `<Replacements>` — substitutions globales

Appliquées **après** l'assemblage du texte brut et **avant** les variantes.

```xml
<Replacements>
    <!-- scope omis → Standalone : remplace uniquement si tout le texte = oldValue -->
    <Replacement oldValue="un mille" newValue="mille" />

    <!-- Anywhere : remplace toutes les occurrences dans le texte -->
    <Replacement oldValue="vingt et " newValue="vingt-" scope="Anywhere" />

    <!-- LastWord : remplace uniquement si oldValue est le dernier mot -->
    <Replacement oldValue="un" newValue="une" scope="LastWord" />
</Replacements>
```

| Scope | Comportement |
|-------|-------------|
| `Standalone` (défaut) | Remplace si le texte entier est égal à `oldValue`. |
| `Anywhere` | Remplace toutes les occurrences sous-chaîne. |
| `LastWord` | Remplace `oldValue` uniquement s'il correspond au dernier mot (précédé d'un espace, d'un tiret ou début de chaîne). |

---

### `<Exceptions>` — formes irrégulières

Vérifiées en **priorité absolue** avant l'algorithme de groupement. Utiles pour les
nombres dont la forme est complètement irrégulière.

```xml
<Exceptions>
    <Number value="1"  string="un" />      <!-- forme dans un groupe (≠ "et un") -->
    <Number value="11" string="onze" />
    <Number value="71" string="soixante onze" />
    <!-- … -->
</Exceptions>
```

---

### `<LanguageSpecifics>` — hook de finalisation

Nom du type (complet ou court) d'une implémentation `INumberToStringLanguageSpecifics`
appelée en dernière étape du pipeline. Peut être pré-enregistré via
`RegisterLanguageSpecifics()` pour éviter la recherche par réflexion.

```xml
<LanguageSpecifics>GermanNumberToStringLanguageSpecifics</LanguageSpecifics>
```

---

### `<Fractions>` — suffixes de dénominateurs décimaux

Permettent d'exprimer la partie décimale d'un nombre avec un dénominateur nommé.
`"(s)"` est un marqueur de pluriel.

```xml
<Fractions>
    <Fraction digits="1" string="dixième(s)" />    <!-- 0.5 → "cinq dixièmes" -->
    <Fraction digits="2" string="centième(s)" />   <!-- 0.25 → "vingt-cinq centièmes" -->
    <Fraction digits="3" string="millième(s)" />
</Fractions>
```

---

### `<Ordinals>` — conversion en ordinaux

Requis pour activer `ConvertOrdinal()`. Priorité de résolution :
`OrdinalException` → règle `Ordinal` (dernier mot) → suffixe (± suppression du `e` final).

```xml
<Ordinals suffix="ième" stripTrailingE="true">

    <!-- Exceptions entières (priorité maximale) -->
    <OrdinalException value="1" string="premier" />

    <!-- Règles sur le dernier mot du cardinal -->
    <Ordinal from="un"   to="unième" />
    <Ordinal from="cinq" to="cinquième" />
    <Ordinal from="neuf" to="neuvième" />

    <!-- Tous les autres : dernier mot + suppression 'e' + "ième"  -->
    <!-- "quatre" → "quatr" + "ième" → "quatrième"                -->
    <!-- "mille"  → "mill"  + "ième" → "millième"                 -->

</Ordinals>
```

| Attribut | Description |
|----------|-------------|
| `suffix` | Suffixe ajouté quand aucune règle ne correspond. |
| `stripTrailingE` | Si `true`, supprime le `e` final du dernier mot avant d'ajouter `suffix`. |

---

### `<Variants>` — variantes morphologiques

Déclare les dimensions de variation et les règles de remplacement associées.
Activée par des appels `Convert(number, "dimension=value", …)`.

```xml
<Variants>

    <!-- 1. Déclaration des dimensions (doivent précéder les Variant) -->
    <!-- La première valeur de chaque dimension est le DÉFAUT -->
    <Dimension name="gender" values="masculin,feminin" />
    <!-- Exemple multi-dimensions : -->
    <!-- <Dimension name="kasus" values="nominativ,akkusativ,dativ,genitiv" /> -->

    <!-- 2. Règles de variante -->
    <!-- Variant sans attributs = wildcard (toujours appliqué en premier) -->
    <!-- Variant avec N attributs = N contraintes → priorité plus haute -->

    <Variant gender="feminin">
        <!-- scope="LastWord" : ne remplace "un" que s'il est le dernier mot -->
        <!-- "un million" → dernier mot = "million" ≠ "un" → pas de remplacement -->
        <!-- "vingt et un" → dernier mot = "un" → "vingt et une"              -->
        <Replacement oldValue="un" newValue="une" scope="LastWord" />
    </Variant>

    <!-- Exemple à 2 contraintes (priorité supérieure à genus seul) :
         voir la configuration DE pour l'implémentation complète genus × kasus -->
    <!-- <Variant kasus="akkusativ" genus="maskulin">
        <Replacement oldValue="ein" newValue="einen" scope="LastWord" />
    </Variant> -->

</Variants>
```

**Règles de cascade** : les variantes sont appliquées dans l'ordre croissant du nombre
de contraintes. Une variante à 2 contraintes peut donc surcharger le résultat d'une
variante à 1 contrainte. Les dimensions non déclarées et les valeurs inconnues sont
ignorées silencieusement.

---

## Related packages

- `omy.Utils` — contains `NumberToStringConverter`, `NumberToStringConverterOptions`, and all built-in culture XML configurations.
