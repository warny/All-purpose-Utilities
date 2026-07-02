# Utils.NumberToString — Améliorations à venir

## Priorité haute — incohérences d'API

### 1. ~~`Convert(decimal, params string[] variants)` manquant~~ — **implémenté**
Trois nouvelles surcharges ajoutées dans `INumberToStringConverter` et `NumberToStringConverter` :
- `Convert(decimal number, params string[] variants)`
- `Convert(decimal number, int mandatoryDecimalDigits, params string[] variants)`
- `Convert(decimal number, int mandatoryDecimalDigits, DecimalFormatOptions? options, params string[] variants)`

`mandatoryDecimalDigits` : négatif = afficher les décimales telles quelles, 0 = partie entière
seulement, positif = arrondir et toujours afficher exactement N chiffres décimaux (zéros inclus).

`DecimalFormatOptions` expose trois propriétés :
- `DecimalSeparator` : remplace le mot séparateur (ex. `"euro(s)"` à la place de `"virgule"`) ;
  le marqueur `(s)` est mis au pluriel selon la valeur de la partie entière.
- `DecimalSuffix` : remplace la dénomination décimale (ex. `"centime(s)"` à la place de
  `"centième(s)"`) ; le marqueur `(s)` est mis au pluriel selon la valeur de la partie décimale.
  Quand renseigné, la partie décimale est toujours convertie comme un entier (pas chiffre par chiffre).
- `OmitZeroDecimals` : supprime la partie décimale si elle vaut zéro après arrondi.

### 2. `ConvertCurrency` sans variants
`ConvertCurrency(decimal amount, CurrencyDefinition currency)` ne prend pas de variants.
Or pour le FR/DE la partie entière varie en genre selon la devise (euro est masculin, livre est féminine).
Il faudrait pouvoir écrire :
```csharp
fr.ConvertCurrency(21m, livre, "gender=feminin"); // "vingt et une livres"
```

### 3. `Convert(long/int, int significantDigits, ...)` manquant dans l'interface
`Convert(BigInteger, int significantDigits, params string[])` existe dans `INumberToStringConverter`,
mais pas de surcharges `long`/`int` équivalentes. Les types primitifs passent par un cast implicite
vers `BigInteger` dans l'implémentation concrète, mais pas depuis l'interface.

## Priorité haute — bugs corrigeables dans les XML

### 4. Formes fusionnées ES et IT (`veintiuno`, `ventiuno`, etc.)
La règle `LastWord` ne peut pas atteindre `uno`/`una` dans les formes sans espace.
Solution purement XML : changer les `buildString` de `"treinta*"` → `"treinta y *"` (ES)
et `"venti*"` → `"venti *"` (IT).

## Priorité moyenne — nouvelles fonctionnalités

### 5. `ConvertOrdinal(BigInteger)` dans l'interface
`Convert` supporte `BigInteger` illimité, mais `ConvertOrdinal` s'arrête à `long` (et en pratique
à `int` via cast). Ajouter `ConvertOrdinal(BigInteger)` avec une implémentation par défaut
`checked((int)number)` garderait la cohérence de surface.

### 6. `ConvertYear` avec variants
`ConvertYear(int year)` n'accepte pas de variants. Pour les langues où `Convert` varie selon
le genre (DE: `ConvertYear(2001)` → `"zweitausend eins"` / `"zweitausend eine"`), l'absence
de variants dans `ConvertYear` crée une inconsistance.
Signature proposée : `ConvertYear(int year, params string[] variants)`.

### 7. Regex compilées à la construction du converter
Les `TriggerReplace` et les règles de remplacement avec `regex=true` sont compilées à chaque appel.
Stocker un `Regex` compilé dans le type `TriggerReplace` au moment de la construction du converter
éviterait la recompilation à chaque conversion.

### 8. Langues populaires manquantes
Par ordre d'utilité et de complexité :
- **TR (Turc)** : agglutinant, harmonie vocalique dans les suffixes numéraux (~90M de locuteurs).
- **SV/NO/DA (Suédois/Norvégien/Danois)** : langues nordiques, genre commun/neutre, ordinals réguliers.
- **UK (Ukrainien)** : proche du RU déjà implémenté.
- **RO (Roumain)** : langue romane comme IT/PT, genre féminin/masculin pour les chiffres.

### 9. Ordinaux ZU (Zoulou) via `IOrdinalLanguageSpecifics`
Les ordinaux zoulou dépendent de la classe nominale du substantif (morphologie agglutinante),
ce qui rend l'approche XML insuffisante. Nécessite :
- Recherche linguistique (classes 1–17, préfixes de classe)
- Implémentation d'une classe `ZuluOrdinalLanguageSpecifics : IOrdinalLanguageSpecifics`

### 10. Ordinaux PL complets — accord genre × cas via `IOrdinalLanguageSpecifics`
L'implémentation actuelle couvre le nominatif masculin singulier.
Pour les formes complètes (féminin/neutre + 7 cas), une classe `PolishOrdinalLanguageSpecifics`
serait plus appropriée que de multiplier les `<OrdinalVariants>`.

### 11. `ConvertOrdinal` avec accord complet pour AR via `IOrdinalLanguageSpecifics`
L'arabe inverse le genre de l'ordinal par rapport au cardinal pour 3-10 (règle de polarité),
et possède une forme duelle. Nécessite une classe `ArabicOrdinalLanguageSpecifics`.

## Priorité basse — robustesse

### 12. `GetConverter` avec codes de culture > 5 caractères
`culture.Length.ArgMustBeIn([2, 5])` rejette des codes légaux comme `"zh-Hans"` (7)
ou `"zh-Hans-CN"` (10). `CultureInfo.GetCultureInfo("zh-Hans").Name` retourne `"zh-Hans"`,
donc passer une `CultureInfo` via l'overload `GetConverter(CultureInfo)` peut lever une exception.

### 13. Thread-safety de `RegisterConfigurations` / `RegisterLanguageSpecifics`
Ces méthodes statiques modifient `CachedConfigurations` (un `Dictionary` statique).
En contexte ASP.NET Core où l'appel peut venir de plusieurs threads au démarrage,
passer à `ConcurrentDictionary` éviterait des races silencieuses.

### 14. `ConvertYear` négatif — années av. J.-C.
`ConvertYear(-44)` passe par le template `minus` → `"minus forty-four"`.
Un attribut optionnel `beforeChristSuffix` dans `<YearFormat>` permettrait de produire
`"forty-four BC"` sans bricoler avec `AdjustFunction`.

### 15. `ConvertYear` — extension à d'autres langues
Langues candidates pour un format split an :
- **RU** : « тысяча девятьсот восемьдесят четыре » (pas de split — fallback OK)
- **FR** : « mille neuf cent quatre-vingt-quatre » (pas de split — fallback OK)
- **IT** : 1984 → « millenovecentottantaquattro » (un seul mot en IT — pas de split)
- Conclusion : le split en deux moitiés est surtout pertinent pour EN, DE et NL.
