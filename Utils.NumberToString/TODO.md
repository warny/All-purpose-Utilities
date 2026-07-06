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

### 8. ~~Langues populaires manquantes~~ — **implémenté (VN, TR, SV, NO, UK, RO)**
Nouveaux fichiers XML ajoutés :
- **VN/VI/VI-VN (Vietnamien)** : cardinals avec allomorphes mốt/lăm via remplacement `Anywhere`,
  ordinals `thứ N` avec exception `thứ nhất`. Connecteur *linh* non implémenté (nécessite moteur).
- **TR/TR-TR (Turc)** : cardinals sans `bir yüz` / `bir bin` (règles nationales respectées).
- **SV/SV-SE (Suédois)** : cardinals avec fusion 20s (`tjugo*`) et espaces 30s-90s.
- **NO/NB/NB-NO (Norvégien Bokmål)** : cardinals.
- **UK/UK-UA (Ukrainien)** : cardinals simplifiés (inflexion тисяч invariante).
- **RO/RO-RO (Roumain)** : cardinals avec noms d'échelle statiques `mii`/`milioane`/`miliarde` ;
  remplacements `onScale+onValue` pour l'accord `unu/doi` ; variante `gen=feminin` (una/două).
  Limitation connue : l'insertion de `de` avant les noms d'échelle pour les multiples ≥ 20
  (ex. "douăzeci de mii") nécessite un attribut `scaleConnector` non encore implémenté dans le moteur.

### 9. Ordinaux ZU (Zoulou) via `IOrdinalLanguageSpecifics` — **reporté**
Nécessite une recherche linguistique approfondie (classes nominales 1–17) et une implémentation
`ZuluOrdinalLanguageSpecifics`. Hors scope d'un correctif XML.

### 10. ~~Ordinaux PL complets~~ — **implémenté**
`PolishOrdinalLanguageSpecifics` (plugin C#) couvre les ordinals 20–999 avec les 30 formes
`rodzaj × przypadek` (5 genres × 6 cas) ; le XML couvre 1–19 avec des blocs `<Ordinal>` complets.
Limitation : les ordinals ≥ 1 000 retombent sur les règles XML simples (tysięczny, etc.).

### 11. `ConvertOrdinal` avec accord partiel pour AR — **partiellement implémenté**
Ordinals 11–19 ajoutés (masculin + féminin via la variante `gender=muʾannath`).
Restant non implémenté : la règle de polarité de genre arabe (3–10 : le masculin prend
la forme dérivée du féminin et inversement) et la forme duelle — nécessitent
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

### 16. ~~Scope `StartsWith` / `EndsWith` pour les remplacements~~ — **implémenté**
Deux nouveaux scopes ajoutés à `ReplacementScope` : `StartsWith` et `EndsWith`.
`ApplySubstringReplacements` utilise `string.StartsWith` / `string.EndsWith` selon le scope.
Utilisés notamment par **ID** ("satu belas" → "sebelas") et **HE** (formes de début de composé).

### 17. ~~Validation des variants au chargement XML~~ — **implémenté**
`ValidateVariantReferences` levée dans `ReadConverter` : si un `<TriggerReplace>` référence
une dimension de variant non déclarée dans `<VariantDimensions>`, une
`InvalidOperationException` descriptive est levée au chargement. Test de régression ajouté.

### 18. ~~`Convert(double)` / `Convert(float)`~~ — **implémenté**
Méthodes par défaut sur `INumberToStringConverter` : le `double`/`float` est converti via
`ToString("R")` + `decimal.TryParse` puis délégué à `Convert(decimal, variants)`.
Précision significative respectée grâce au format round-trip.

### 19. ~~`ConvertFraction(BigInteger, BigInteger)`~~ — **implémenté**
Méthode publique `ConvertFraction(numerator, denominator, params string[] variants)` exposée
sur `INumberToStringConverter` (implémentation par défaut) et `NumberToStringConverter`.
S'appuie sur `BuildFractionText` interne. Produit "un tiers", "trois quarts", etc.

### 20. ~~`ConvertMultiplicative(int)`~~ — **implémenté**
Section `<Multiplicatives>` en XML avec entrées `<Entry value="…" string="…" />`.
`ConvertMultiplicative(n)` expose la table ; méthode par défaut sur l'interface lève
`NotSupportedException` si non configurée.

### 21. ~~Conversion de types temporels~~ — **implémenté**
Quatre nouvelles surcharges ajoutées à `INumberToStringConverter` et `NumberToStringConverter` :
- `Convert(TimeSpan duration, params string[] variants)` — totalise les heures et concatène
  heure/minute/seconde non nuls avec leurs formes singulier/pluriel issues de `<TimeUnits>`.
- `Convert(TimeOnly time, params string[] variants)` — toujours l'heure ; minute/seconde si > 0.
- `Convert(DateOnly date, params string[] variants)` — patron configurable via `<DateFormat pattern="…">` ;
  tokens `{month}` (nom via `CultureInfo`), `{ordinal-day}`, `{cardinal-day}`, `{year}` ;
  attribut `firstDay` pour la forme du 1er du mois (ex. "premier", "ersten").
- `Convert(DateTime dateTime, params string[] variants)` — combine DateOnly + TimeOnly avec
  connecteur optionnel `dateTimeConnector`.

Propriétés `SupportsTimeConversion` / `SupportsDateConversion` exposées sur l'interface
(par défaut `false`). EN, FR et DE configurés avec `<TimeUnits>` et `<DateFormat>`.

### 22. ~~Connecteur inter-groupes conditionnel~~ — **implémenté**
Attributs `intraGroupConnector` et `intraGroupConnectorThreshold` sur `<Language>`.
Injecté dans `ConvertGroup` au niveau 3 quand `groupValue > 0` (centaines présentes)
et `0 < remainder < threshold`. VN configuré avec `intraGroupConnector="linh" threshold="10"` :
- 101 → "một trăm linh một" ✓  
- 110 → "một trăm mười" (pas de connecteur, remainder ≥ threshold) ✓

### 23. ~~Nouvelles langues HR et HU~~ — **implémenté** ; RO **implémenté (cardinals, voir item 8)**
- **HR** (croate) : long scale avec `groupSeparator="li"` → milijun/milijarda/bilijun ;
  exceptions 11–19 ; remplacement "jedan tisuća" → "tisuća" ; ordinals (prvi/drugi/treći…).
- **HU** (hongrois) : séparateur vide (agglutination) ; forme liante `két` vs forme isolée
  `kettő` via `Exception[2]` + Replacements ("kettőezer" → "kétezer") ; `startIndex="2"` pour
  générer billió/billiárd ; "egyezer" → "ezer" ; ordinals lexicaux (első/második…).
- **RO** (roumain) : cardinals implémentés (PR #416). Voir item 8 pour les détails et la limitation "de".

### ~~C1. Numéros romains~~ — **hors périmètre**
`ConvertRoman` doit faire l'objet d'un système de conversion distinct qui pourrait implémenter
`INumberToStringConverter` mais sans utiliser le moteur XML interne. Hors scope de ce projet.
