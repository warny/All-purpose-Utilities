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
  ordinals `thứ N` avec exception `thứ nhất`. Connecteur *linh* géré via `intraGroupConnector`
  (voir item 22 plus bas).
- **TR/TR-TR (Turc)** : cardinals sans `bir yüz` / `bir bin` (règles nationales respectées).
- **SV/SV-SE (Suédois)** : cardinals avec fusion 20s (`tjugo*`) et espaces 30s-90s.
- **NO/NB/NB-NO (Norvégien Bokmål)** : cardinals.
- **UK/UK-UA (Ukrainien)** : cardinals simplifiés (inflexion тисяч invariante).
- **RO/RO-RO (Roumain)** : cardinals avec noms d'échelle statiques `mii`/`milioane`/`miliarde` ;
  remplacements `onScale+onValue` pour l'accord `unu/doi` ; variante `gen=feminin` (una/două).
  L'insertion de `de` avant les noms d'échelle pour les multiples ≥ 20 (ex. "douăzeci de mii")
  est gérée via l'attribut `scaleConnector`/`scaleConnectorThreshold`, ajouté sur `<Language>`
  avec la PR #419. Pas d'ordinaux RO à ce jour (voir item 9/11 : mêmes limites de portée
  que ZU/AR — nécessiterait un plugin C# dédié).

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

---

## Améliorations identifiées (2026-07-06) — toutes implémentées le 2026-07-09

### 24. ~~Tests manquants : HR, HU, NO, SV, UK, VN~~ — **implémenté**
Six nouveaux fichiers dédiés créés (`NumberToStringConverterHRTests.cs`,
`NumberToStringConverterHUTests.cs`, `NumberToStringConverterNOTests.cs`,
`NumberToStringConverterSVTests.cs`, `NumberToStringConverterUKTests.cs`,
`NumberToStringConverterVNTests.cs`), 31 tests au total couvrant cardinaux, milliers,
grande échelle et ordinaux (quand supportés).

### 25. ~~Ordinaux féminins HI non testés~~ — **déjà couvert**
La couverture existait déjà dans `NumberToStringConverterImprovementsTests.cs`
(section « C13 — Ordinal HI feminine variant », `ConvertOrdinal_HI_StriiVariant_*`),
ajoutée avec la PR #419. Seul le fichier dédié `NumberToStringConverterHITests.cs`
n'en avait pas — pas d'action nécessaire, le comportement est vérifié.

### 26. ~~Validation des *valeurs* de dimensions~~ — **implémenté**
`ValidateVariantReferences` (dans `NumberToStringConverter.Globalization.cs`) vérifie désormais
aussi que chaque valeur de contrainte figure dans les valeurs déclarées de sa dimension
(comparaison `OrdinalIgnoreCase`, cohérente avec `ApplyVariantRules`). Un typo de valeur lève
maintenant une `InvalidOperationException` descriptive au chargement, comme pour les clés
inconnues. La fonction a été factorisée (une seule vérification clé+valeur pour VariantRules,
OrdinalVariants et TriggerReplace.Forms).

### 27. ~~`VariantRules` pré-triées à la construction~~ — **implémenté**
Nouveau champ `_sortedVariantRules` (`ImmutableArray<VariantRule>`) calculé une fois dans le
constructeur de `NumberToStringConverter`. `ApplyVariantRules` et `ApplyVariantRulesForScale`
itèrent sur ce champ au lieu d'appeler `.OrderBy(r => r.Specificity)` à chaque conversion.

### 28. ~~Config FI et KO : suppression du multiplicateur "1" devant l'unité de mille~~ — **implémenté**
- **FI** : `<Replacement oldValue="yksi tuhat" newValue="tuhat" onScale="1" onValue="1" />`
  ajouté dans `FI.xml`. 1 000 → "tuhat" (au lieu de "yksi tuhat").
- **KO** : `<Replacement oldValue="일 천" newValue="천" onScale="1" onValue="1" />`
  ajouté dans `KO.xml` (le texte de groupe onScale inclut le `separator=" "` configuré).
  1 000 → "천" (au lieu de "일 천").

Les tests existants qui attendaient l'ancien comportement (`NumberToStringConverterFITests.cs`,
`NumberToStringConverterKOTests.cs`, et un cas dans `NumberToStringConverterImprovementsTests.cs`)
ont été mis à jour en conséquence.

---

## Améliorations identifiées (2026-07-09)

### ~~29. `GetMonthName` — catch trop large~~ — **implémenté**

`catch { }` remplacé par `catch (Exception ex) when (ex is CultureNotFoundException or IndexOutOfRangeException)`
dans `NumberToStringConverter.cs`. Test de régression via réflexion sur la méthode privée
(mois hors plage → repli sur la valeur numérique au lieu de propager une exception inattendue).

### ~~30. `BuildFractionText` ignore les numérateurs négatifs pour les suffixes nommés~~ — **implémenté**

Condition changée en `BigInteger.Abs(numerator) <= long.MaxValue` ; `ToPlural` reçoit aussi
la valeur absolue. `-1/10` bénéficie désormais de la forme nommée ("moins un dixième" /
"minus one tenth") au lieu de retomber sur la forme brute numérateur/dénominateur.

### ~~31. `ApplyVariantRules` / `ApplyVariantRulesForScale` dupliquées~~ — **implémenté**

Fusionnées en une seule méthode `ApplyVariantRules(text, query, numericValue?, scaleGroupNumber?, scaleGroupValue)` ;
`ApplyVariantRulesForScale` devient un wrapper fin. Le tri de l'item 27 a été fait dans la
foulée (`_sortedVariantRules` trié une fois au constructeur).

### ~~32. Étendre le fix item 28 (multiplicateur "1" devant mille) à ZH et JA~~ — **implémenté (JA seulement)**

Vérification empirique (et non simple supposition) : le mandarin standard **garde** le "一"
devant 千 ("一千" est correct, confirmé par un test ZH déjà existant) — aucune correction
apportée à `ZH.xml`. Le japonais, en revanche, omet "一" devant 千 comme devant 百 (déjà
correct) : `<Replacement oldValue="一 千" newValue="千" onScale="1" onValue="1" />` ajouté
dans `JA.xml`. Item 28 lui-même (FI/KO) a été implémenté au passage (il ne l'était pas malgré
un statut antérieur trompeur) — voir tests `NumberToStringConverterFITests.cs`/`KOTests.cs`.

### ~~33. PT/GL — centaines sans forme féminine~~ — **déjà implémenté (PT) ; GL déjà correct par conception**

Vérification du code : PT a déjà les 8 remplacements -entos→-entas dans `<Variants>` (testé
dans `Cardinals_Gender_Feminino`). GL a déjà `douscentos→douscentas` (le seul cas variable en
galicien selon le commentaire du fichier) ; test de régression ajouté pour verrouiller ce
comportement documenté (200 varie, 300 non).

### ~~34. FR-be-ch — connecteur "et" manquant pour 71/81/91~~ — **déjà correct, non un bug**

Le mécanisme générique (`Group level 1, digit=1 → "et un"`) applique déjà "et" uniquement
au chiffre 1, quel que soit le mot des dizaines — 71/81/91 en bénéficient automatiquement,
72-79/82-89/92-99 n'en ont pas besoin. Tests ajoutés (`NumberToStringConverterFRTests.cs`)
pour verrouiller ce comportement déjà correct mais non testé.

### ~~35. `Convert(double/float)` et `DecimalFormatOptions` quasi uniquement testés en FR~~ — **implémenté**

6 tests ajoutés couvrant DE/ES/IT (`Convert(double)` avec variant, `DecimalFormatOptions`
avec séparateur/suffixe pluralisés) dans `NumberToStringConverterCoverageGapsTests.cs`.

### ~~36. `Convert(TimeSpan/DateOnly/DateTime)` non testé pour DE~~ — **implémenté**

2 tests ajoutés : `Convert(DateTime)` DE (parties date+heure) et `Convert(DateOnly)` DE avec
`firstDay="ersten"` appliqué à `{ordinal-day}`.

### ~~37. `Convert(BigInteger, int significantDigits, ...)` sans test par langue~~ — **implémenté**

2 tests ajoutés (DE, ES avec variant de genre) vérifiant l'arrondi de groupe combiné aux
variants sur un grand nombre.

### ~~38. `ConvertYear` négatif (`beforeChristSuffix`) jamais combiné à des variants~~ — **implémenté**

Découverte en écrivant le test : aucune config XML livrée ne déclare `beforeChristSuffix`
(seul `NumberToStringConverterBatchTests.cs` l'exerçait via un converter construit à la main).
Nouveau test construit un converter FR (qui a un genre) + `BeforeChristSuffix`, vérifiant que
le genre est bien transmis au numéral avant append du suffixe ("un av. J.-C." / "une av. J.-C.").

---

## Améliorations identifiées (2026-07-09, suite)

### 39. EL (grec) — centaines sans forme féminine — **bug linguistique confirmé, bloqué par le moteur**

Les centaines grecques (διακόσια, τριακόσια…) ne sont produites qu'en neutre alors que le
grec moderne les accorde en genre. Tentative d'implémentation via `<Variant type="gender"
variant="αρσενικό/θηλυκό">` : **régression découverte** — la valeur par défaut d'une dimension
est toujours `Values[0]` (`NumberToStringConverter.cs:1099`, non redéfinissable), et cette
même dimension `gender` est partagée avec le positionnement des `forms=` des ordinaux
(qui exige `αρσενικό` en premier). Rendre `ουδέτερο` premier casserait le défaut des ordinaux
(`ConvertOrdinal(1)` sans variant doit rester "πρώτος", pas "πρώτο"). Fix annulé pour éviter
de casser `Convert(200)` (devenu silencieusement "διακόσιοι" au lieu de "διακόσια"). Nécessite
soit une dimension `gender` séparée pour les cardinaux (incohérent avec le reste du projet),
soit une évolution du moteur permettant une valeur par défaut différente de `Values[0]`.

### 40. RU/CS/SK/BG — accord numéral slave (nominatif/génitif sg/pl) absent — limitation architecturale

Les quatre langues slaves n'implémentent aucun mécanisme pour l'accord du nom compté selon
la règle slave classique (1 → nominatif singulier, 2-4 → génitif singulier, 5+ → génitif
pluriel ; ex. RU "1 книга" / "2 книги" / "5 книг"). Seul le mot d'échelle "тысяча" a un
remplacement ponctuel (RU, sans dimension de variant associée). Contrairement aux items
26/33/39 (accord d'un mot isolé), ce cas porterait sur le nom compté externe, ce que le
moteur ne modélise pas aujourd'hui — **reporté**, à l'instar de l'item 9 (ZU), nécessiterait
une extension du système de variants côté appelant plutôt qu'un correctif XML.

### ~~41. ES — composés 21-29 (`veintiuno`) toujours fusionnés sans espace~~ — **implémenté**

Plutôt que d'introduire un espace (qui changerait l'orthographe standard "veintiuno"),
un remplacement mot-entier dédié a été ajouté : `<Replacement oldValue="veintiuno"
newValue="veintiuna" scope="Anywhere" />`. 22-29 ne varient pas en genre (seul "uno" varie).
`Convert(21, "gender=femenino")` → "veintiuna".

### ~~42. README — 14 langues configurées absentes de la liste des langues supportées~~ — **implémenté**

Les 14 langues (BG, CS, DA, FA, HR, HU, ID/MS, NO/NB, SK, SV, SW, TR, UK, VN) ajoutées à la
table des langues supportées, avec leurs codes de culture réels lus dans chaque XML.

### ~~43. README — connecteurs XML majeurs et `beforeChristSuffix` non documentés~~ — **implémenté**

`groupConnector`/`groupConnectorThreshold`, `intraGroupConnector`/`intraGroupConnectorThreshold`,
`scaleConnector`/`scaleConnectorThreshold` ajoutés à la table `<Language>` attributes ;
`beforeChristSuffix` ajouté à la table `<YearFormat>` attributes.

### ~~44. `INumberToStringConverter` — XML docs sans tags `<exception>` structurés~~ — **implémenté**

Tags `<exception cref="...">` ajoutés sur `Convert(BigInteger, variants)`, `ConvertOrdinal`
(int/long/BigInteger, avec/sans variants), `ConvertCurrency` (avec/sans variants),
`ConvertMultiplicative`, `Convert(TimeSpan/TimeOnly/DateOnly/DateTime)`, et
`Convert(double/float)` (voir item 45).

### ~~45. `Convert(double)` / `Convert(float)` — incohérences de surcharge et cas limites non gérés~~ — **implémenté**

Surcharges `Convert(double)`/`Convert(float)` sans `variants` ajoutées (interface + implémentation
concrète, pour un accès direct sans passer par l'interface). `double.IsNaN`/`IsInfinity` (et
équivalents `float`) interceptés en tête de méthode : lève désormais `ArgumentException` au lieu
d'un comportement indéfini.

### ~~46. `ConvertFraction` — pas de surcharge `int`/`long`, uniquement `BigInteger`~~ — **implémenté**

Surcharges `ConvertFraction(int, int, ...)` et `ConvertFraction(long, long, ...)` ajoutées
(interface + implémentation concrète), délégant à la version `BigInteger`.
