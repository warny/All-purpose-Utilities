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

### 2. ~~`ConvertCurrency` sans variants~~ — **implémenté**
Nouvelle surcharge `ConvertCurrency(decimal, CurrencyDefinition, params string[] variants)` ajoutée
dans `INumberToStringConverter` (avec implémentation par défaut qui lance `NotSupportedException`)
et dans `NumberToStringConverter` (où l'ancienne implémentation délègue désormais à la nouvelle).

Les `variants` sont transmis aux deux sous-appels `Convert(units, variants)` et
`Convert(subunits, variants)`, ce qui permet l'inflexion genre/cas des numéraux dans les deux parties :
```csharp
fr.ConvertCurrency(21.01m, livre, "gender=feminin"); // "vingt et une livres et une sous"
```

### 3. ~~`Convert(long/int, int significantDigits, ...)` manquant dans l'interface~~ — **implémenté**
Deux nouvelles surcharges ajoutées dans `INumberToStringConverter` (implémentations par défaut
déléguant à `Convert(BigInteger, int, string[])`) et dans `NumberToStringConverter` :
- `Convert(int number, int significantDigits, params string[] variants)`
- `Convert(long number, int significantDigits, params string[] variants)`

## Priorité haute — bugs corrigeables dans les XML

### 4. ~~Formes fusionnées ES et IT~~ — **implémenté**
`buildString` corrigés : `"treinta*"` → `"treinta y *"` (ES 31-99),
`"venti*"` → `"venti *"` (IT 21-29).
Cela permet à la règle `LastWord` d'atteindre `uno`/`una` dans ces composés
(ordinals ES masculins 31-99 et variant femminile IT 21-29 fonctionnent désormais).

## Priorité moyenne — nouvelles fonctionnalités

### 5. ~~`ConvertOrdinal(BigInteger)` dans l'interface~~ — **implémenté**
Surcharges `ConvertOrdinal(BigInteger)` et `ConvertOrdinal(BigInteger, params string[])` ajoutées
dans `INumberToStringConverter` (implémentation par défaut `checked((long)number)`)
et dans `NumberToStringConverter`.

### 6. ~~`ConvertYear` avec variants~~ — **implémenté**
`ConvertYear(int year, params string[] variants)` ajouté dans `INumberToStringConverter`
(implémentation par défaut délégue à `ConvertYear(year)`) et dans `NumberToStringConverter`
(les variants sont transmis à tous les sous-appels `Convert()`).

### 7. ~~Regex compilées à la construction du converter~~ — **implémenté**
`TriggerReplace` stocke maintenant `CompiledRegex` (un `Regex` pré-compilé) initialisé dans le
constructeur quand `isRegex=true`. `ApplyTriggerReplace` utilise `CompiledRegex.Replace()`
au lieu de `Regex.Replace()` à chaque appel.

### 8. ~~Langues populaires manquantes~~ — **implémenté (VN, TR, SV, NO, UK)**
Cinq nouveaux fichiers XML ajoutés :
- **VN/VI/VI-VN (Vietnamien)** : cardinals avec allomorphes mốt/lăm via remplacement `Anywhere`,
  ordinals `thứ N` avec exception `thứ nhất`. Connecteur *linh* non implémenté (nécessite moteur).
- **TR/TR-TR (Turc)** : cardinals sans `bir yüz` / `bir bin` (règles nationales respectées).
- **SV/SV-SE (Suédois)** : cardinals avec fusion 20s (`tjugo*`) et espaces 30s-90s.
- **NO/NB/NB-NO (Norvégien Bokmål)** : cardinals.
- **UK/UK-UA (Ukrainien)** : cardinals simplifiés (inflexion тисяч invariante).
- **RO (Roumain)** : non implémenté (système genre+cas trop complexe → reporté).

### 9. Ordinaux ZU (Zoulou) via `IOrdinalLanguageSpecifics` — **reporté**
Nécessite une recherche linguistique approfondie (classes nominales 1–17) et une implémentation
`ZuluOrdinalLanguageSpecifics`. Hors scope d'un correctif XML.

### 10. Ordinaux PL complets — **reporté**
L'implémentation actuelle couvre le nominatif masculin singulier.
Les formes complètes (féminin/neutre + 7 cas) nécessitent `PolishOrdinalLanguageSpecifics`.

### 11. `ConvertOrdinal` avec accord complet pour AR — **reporté**
La règle de polarité de genre arabe (3-10) et la forme duelle nécessitent
`ArabicOrdinalLanguageSpecifics`.

## Priorité basse — robustesse

### 12. ~~`GetConverter` avec codes de culture > 5 caractères~~ — **implémenté**
Remplacé `culture.Length.ArgMustBeIn([2, 5])` par un stripping récursif du dernier sous-tag BCP-47
(`culture[..culture.LastIndexOf('-')]`). `"zh-Hans-CN"` → `"zh-Hans"` → `"zh"` → ZH config.

### 13. ~~Thread-safety de `RegisterConfigurations` / `RegisterLanguageSpecifics`~~ — **implémenté**
`CachedConfigurations` et `_registeredSpecifics` convertis de `Dictionary` en `ConcurrentDictionary`.

### 14. ~~`ConvertYear` négatif — années av. J.-C.~~ — **implémenté**
Attribut `beforeChristSuffix` ajouté dans `<YearFormat>` (XML + `YearFormatType` + `YearFormatOptions`).
Quand présent, `ConvertYear(-44, ...)` retourne `"forty-four BC"` au lieu de `"minus forty-four"`.

### 15. `ConvertYear` — extension à d'autres langues — **non applicable**
Seuls EN, DE et NL bénéficient du format split-en-deux-moitiés, et tous trois sont déjà implémentés.
RU, FR, IT n'utilisent pas ce format.

---

## Prochaines améliorations identifiées

### 16. Scope `StartsWith` pour les remplacements — **indispensable**
Actuellement les scopes sont `Standalone`, `Anywhere`, `LastWord`. Il manque `StartsWith` (et
symétrique `EndsWith`) pour affecter le début d'un segment sans contaminer les autres occurrences.
Cas d'usage typiques :
- **ID** : "satu belas" → "sebelas" (le préfixe "satu" se contracte en "se" devant "belas")
- **PL/CS/SK** : contractions de préfixes en début de composé
- Implémentation : ajouter `StartsWith` / `EndsWith` à `ReplacementScope` (enum) et gérer dans
  `ApplySubstringReplacements` avec `string.StartsWith` / `string.EndsWith`.

### 17. Validation des variants au chargement XML — **indispensable**
Si un `<TriggerReplace>` ou un `<Replacement>` référence un variant non déclaré dans
`<VariantDimensions>`, l'erreur est silencieuse et le variant est simplement ignoré.
Il faut lever une `InvalidOperationException` descriptive au moment de `InitializeConfigurations`
(dans `NumberToStringConverter.Globalization.cs`) pour détecter les fautes de frappe dans les XML.

### 18. `Convert(double)` / `Convert(float)` — manque d'API évident
Les surcharges `decimal` existent mais `double`/`float` sont absentes alors que la majorité des
APIs .NET utilisent `double`. Proposition :
```csharp
string Convert(double number, int significantDigits = 15, params string[] variants);
string Convert(float number, int significantDigits = 7, params string[] variants);
```
La précision significative par défaut doit être paramétrable pour éviter les artefacts flottants.

### 19. `ConvertFraction(BigInteger, BigInteger)` dans l'interface publique
`BuildFractionText` est déjà implémenté en interne mais n'est pas exposé via
`INumberToStringConverter`. Exposer publiquement pour permettre "un tiers", "trois quarts", etc.

### 20. `ConvertMultiplicative(int)` — nombres multiplicatifs
Distinct des ordinaux : `ConvertMultiplicative(2)` → "double"/"deux fois" (FR),
"twice" (EN), "zweimal" (DE). Supportable via une section `<Multiplicatives>` en XML
ou `IMultiplicativeLanguageSpecifics`.

### 21. Conversion de types temporels — **excellente priorité**
Exposer des surcharges pour les types temporels standards .NET, en s'appuyant sur le
`INumberToStringConverter` existant :

#### 21a. `Convert(TimeSpan)` / `ConvertDuration`
```csharp
string Convert(TimeSpan duration, params string[] variants);
```
Produit "deux heures trente minutes cinq secondes". Nécessite des sections `<TimeUnits>`
dans le XML (heure/minute/seconde avec pluriel, éventuellement `(s)` via `ToPlural`).
Les unités non nulles sont concaténées avec le séparateur de la langue.

#### 21b. `Convert(TimeOnly)`
```csharp
string Convert(TimeOnly time, params string[] variants);
```
Produit "quatorze heures trente" ou "deux heures et demie" selon le format de la langue.
Option : format 12h vs 24h via variant ou option dédiée.

#### 21c. `Convert(DateOnly)`
```csharp
string Convert(DateOnly date, params string[] variants);
```
Produit "le deux juillet deux mille vingt-six" (FR), "July second, twenty twenty-six" (EN).
S'appuie sur `ConvertYear`, `ConvertOrdinal` et des noms de mois en XML (`<Months>`).

#### 21d. `Convert(DateTime)`
```csharp
string Convert(DateTime dateTime, params string[] variants);
```
Combinaison de `Convert(DateOnly)` + `Convert(TimeOnly)` avec connecteur configurable.

**Note architecturale :** les noms de mois et de jours sont disponibles sans XML supplémentaire via
`System.Globalization.CultureInfo.GetCultureInfo(lang).DateTimeFormat.MonthNames[month-1]` et
`.DayNames[dayOfWeek]`. Inutile de les dupliquer dans les fichiers XML — le moteur les lira
directement depuis le BCL, ce qui garantit leur fiabilité pour toutes les langues supportées
par .NET. Seules les unités de temps (heure/minute/seconde avec leur pluriel) et le patron de
composition de la date (ordre jour/mois/an, connecteurs) restent à configurer dans le XML.
```xml
<TimeUnits>
    <Unit name="hour"   singular="heure"  plural="heures"  />
    <Unit name="minute" singular="minute" plural="minutes" />
    <Unit name="second" singular="seconde" plural="secondes" />
</TimeUnits>
<DateFormat pattern="{ordinal-day} {month} {year}" />
```

### 22. Connecteur inter-groupes conditionnel (moteur XML)
Certaines langues insèrent un mot de liaison entre groupes selon le contexte :
- VN : "linh" entre centaines et unités isolées (101 → "một trăm linh một")
- EN : "and" entre centaines et dizaines/unités (101 → "one hundred and one")
- FA : "و" entre chaque groupe

Proposition : attribut `connector="linh"` + `connectorThreshold="10"` sur `<Group level="3">`
(inséré si le groupe de rang inférieur est < seuil). Résoudrait la limitation documentée dans VN.

### 23. Nouvelles langues — **candidats prioritaires**
- **HR/SR** (croate/serbe) : Slavique du sud, proches de BG ; serbe en deux alphabets
- **HU** (hongrois) : agglutinant, suffixes nominaux collant directement au chiffre
- **RO** (roumain) : genre masculin/féminin, "de" obligatoire devant les grands nombres
  (ex : "un milion **de** oameni") — complexe mais jouable avec `Variants`

### ~~C1. Numéros romains~~ — **hors périmètre**
`ConvertRoman` doit faire l'objet d'un système de conversion distinct qui pourrait implémenter
`INumberToStringConverter` mais sans utiliser le moteur XML interne. Hors scope de ce projet.
