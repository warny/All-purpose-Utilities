# TODO — Immutabilité et encapsulation des collections

## État au 2026-08-12

Un audit transversal a été refait sur `master` après les PR #533 à #537. Il couvre les collections reçues de l'extérieur, les tableaux exposés, les propriétés `IReadOnly*`, les collections internes possédées par leur classe, les snapshots construits au getter, les propriétés `init`, les tables statiques et les comparateurs associés aux collections immutables.

Le chantier n'est **pas terminé** : huit findings concrets restent à traiter. Le fichier reste donc un `TODO`.

## Principe cible

### Objet immutable / résultat / snapshot

Lorsqu'un objet prend possession d'une collection externe, faire une copie défensive **une seule fois**, au moment de la construction ou de la prise de possession, puis conserver une représentation immutable :

- séquence compacte : `ToImmutableArray()` ou l'équivalent read-only standard du projet s'il copie réellement ;
- liste : `ToImmutableList()` ;
- dictionnaire : `ToImmutableDictionary(...)` en préservant impérativement le comparer ;
- set : `ToImmutableHashSet(...)` en préservant le comparer ;
- autre forme : `ToImmutable...` correspondant à la sémantique.

Ne pas stocker `source.ToArray()` derrière `IReadOnlyList<T>` / `IReadOnlyCollection<T>` si ce tableau reste récupérable par cast.

### Collection mutable possédée par sa classe

Si la classe possède un `List<T>`, `Dictionary<TKey,TValue>`, `Stack<T>`, etc. et fournit elle-même les opérations de mutation, conserver le stockage mutable mais exposer une **vue read-only persistante créée une seule fois**. La vue peut être live ; elle ne doit pas permettre de récupérer le backing mutable et ne doit pas être recréée à chaque getter.

### Snapshot d'un état mutable

Si le contrat exige un snapshot historique, le snapshot doit être réellement immutable. Quand le propriétaire peut changer, préférer un cache invalidé/reconstruit lors des mutations plutôt qu'une copie à chaque lecture.

### Sémantique à préserver

Toujours préserver : ordre, unicité, comparer, sensibilité à la casse, déterminisme et API publique autant que possible.

---

# Findings restants

## P1 — `Utils.Fonts.FontSupport` expose des tables globales mutables

`FontSupport` expose directement plusieurs tableaux statiques get-only utilisés comme tables de référence globales, notamment :

- `StdNames` ;
- `StdValues` ;
- `Type1CExpertCharset` ;
- `Type1CExpertSubCharset` ;
- `MacExtras` ;
- `MacRomanEncoding` ;
- `IsoLatin1Encoding` ;
- `WinAnsiEncoding` ;
- `StandardEncoding`.

Un appelant peut modifier ces tableaux en place. Cela modifie l'état global observable du composant. Le cas `StdNames` est particulièrement dangereux : `StdNameIndexMap` est construit séparément, donc une mutation de `StdNames` peut rendre `GetName(...)` et `GetStrIndex(...)` incohérents.

### À faire

Stocker les tables une seule fois sous une représentation réellement immutable/read-only. Éviter une copie à chaque getter. Si conserver les signatures publiques `T[]` impose une allocation défensive à chaque lecture, évaluer explicitement le breaking change vers `IReadOnlyList<T>` / une représentation immutable dans le cadre de la version majeure en cours plutôt que de laisser un état global mutable.

### Tests

Vérifier qu'un consommateur ne peut plus modifier les tables globales et que les lookups dérivés restent cohérents.

---

## P1 — `Utils.Objects.Types` expose des classifications globales comme `Type[]`

Les propriétés statiques suivantes sont des tableaux globaux directement modifiables :

- `Number` ;
- `UnsignedNumber` ;
- `SignedNumber` ;
- `FloatingPointNumber` ;
- `_8BitsNumberI` ;
- `_16BitsNumberI` ;
- `_32BitsNumberI` ;
- `_32BitsNumberF` ;
- `_64BitsNumberI` ;
- `_64BitsNumberIF` ;
- `_128BitsNumberIF`.

Elles représentent des classifications constantes et ne constituent pas une API de mutation volontaire.

### À faire

Créer une représentation immutable une seule fois et l'exposer sans permettre de mutation. Comme pour `FontSupport`, traiter explicitement la contrainte de compatibilité liée aux signatures publiques `Type[]` ; ne pas remplacer le problème par une allocation cachée à chaque getter sans décision consciente.

### Tests

Vérifier que les groupes ne peuvent plus être modifiés par un appelant et que les usages de classification restent inchangés.

---

## P1 — `CMapFormat4.TableMap` conserve le `short[]` fourni par l'appelant

`CMapFormat4.TableMap` reçoit un `short[] map`, affecte directement `Map = map`, puis construit `reverseMap` à partir du même contenu.

L'appelant peut modifier `map` après construction : l'indexation caractère → glyph utilise alors les nouvelles valeurs de `Map`, tandis que `reverseMap` conserve les anciennes valeurs. L'objet devient structurellement incohérent.

### À faire

Faire une copie defensive une seule fois à la construction, idéalement sous `ImmutableArray<short>` ou équivalent indexable immutable, puis construire `reverseMap` depuis cette même représentation possédée.

Ne pas recopier au getter/indexeur.

### Tests

Construire un `TableMap` depuis un tableau, modifier la source, puis vérifier que les deux sens de mapping restent cohérents et basés sur l'état initial.

---

## P1 — `DateFormulaLanguage.Days` reste aliasé malgré une API `init`-only

`DateFormulaLanguage` est un type de configuration dont les propriétés sont `required ... { get; init; }`. `Days` est déclaré :

```csharp
public required IReadOnlyDictionary<string, DayOfWeek> Days { get; init; }
```

Un `Dictionary<string, DayOfWeek>` fourni dans l'initializer reste donc mutable après construction et change l'état observable de l'objet.

### À faire

Introduire un backing field et normaliser dans l'`init` vers un dictionnaire immutable. Préserver le comparer de la collection source lorsque sa sémantique est significative. Conserver la possibilité d'object initializer si l'API le requiert.

### Tests

Modifier le dictionnaire source après l'initialisation et vérifier que `Days` ne change pas ; vérifier aussi qu'un `with` n'est pas pertinent ici (`DateFormulaLanguage` n'est pas un record) et que la propriété exposée n'est pas un `Dictionary` mutable.

---

## P1 — `SerializationContractException.Diagnostics` expose son tableau interne

Le constructeur fait actuellement :

```csharp
Diagnostics = diagnostics.ToArray();
```

alors que la propriété est :

```csharp
public IReadOnlyList<SerializationContractDiagnostic> Diagnostics { get; }
```

Le snapshot est donc détaché de la source mais reste mutable par cast vers `SerializationContractDiagnostic[]`.

### À faire

Matérialiser une seule fois vers `ImmutableArray<SerializationContractDiagnostic>` (ou représentation équivalente) et exposer directement cette valeur sous l'API existante.

### Tests

Vérifier mutation de la source après construction et impossibilité de récupérer un tableau mutable depuis `Diagnostics`.

---

## P1 — `ODataQueryCompilation.Filters` et `Expansions` exposent des tableaux internes

Le constructeur fait :

```csharp
Filters = filters.ToArray();
Expansions = (expansions ?? Array.Empty<string>()).ToArray();
```

et expose les deux valeurs sous `IReadOnlyList<string>`. `ODataQueryCompilation` est un résultat de compilation à getters seuls ; les tableaux peuvent néanmoins être recastés et modifiés après construction.

### À faire

Convertir une seule fois à la construction avec `ToImmutableArray()` et conserver les signatures publiques `IReadOnlyList<string>`.

### Tests

Vérifier non-aliasing avec les sources et impossibilité de modifier `Filters` / `Expansions` par cast.

---

## P2 — `AcntTable.AccentDescription.Multiple.Extensions` conserve directement la liste source

`AccentDescription.Multiple` reçoit :

```csharp
IReadOnlyList<ExtensionEntry> extensions
```

puis fait directement :

```csharp
Extensions = extensions;
```

La classe `Multiple` n'expose aucune opération de mutation et se comporte comme une description construite ; une `List<ExtensionEntry>` externe peut pourtant modifier ultérieurement son contenu et donc la taille/sérialisation de la table qui la contient.

### À faire

Faire une copie défensive une seule fois, par exemple `extensions.ToImmutableArray()`.

### Tests

Modifier la liste source après construction et vérifier que `Extensions` et la sérialisation restent stables.

---

## P2 — `ParserOptions.NumberSuffixes` perd le comparer lors de l'immuabilisation

`NumberSuffixes` est construit depuis un `Dictionary<string, Func<string, object>>` configuré avec `StringComparer.CurrentCultureIgnoreCase`, puis converti avec :

```csharp
.ToImmutableDictionary()
```

sans passer explicitement le comparer. L'immuabilité est correcte, mais la conversion ne doit pas changer la sémantique de comparaison qui était explicitement demandée par le dictionnaire source.

### À faire

Construire l'`ImmutableDictionary` avec le comparer attendu explicitement (ou utiliser un builder configuré avec ce comparer). Vérifier la sémantique souhaitée entre `CurrentCultureIgnoreCase` et une éventuelle politique plus stable telle qu'`OrdinalIgnoreCase` avant de modifier le comportement.

### Tests

Tester au minimum la résolution des suffixes en casse différente et, si `CurrentCultureIgnoreCase` est conservé, un scénario de culture pertinent.

---

# Audit transversal — cas classés corrects / volontaires

Les cas suivants ont été revérifiés et ne doivent pas être modifiés mécaniquement :

- `ProtocolResponseException.Responses` : copie puis `ReadOnlyCollection<T>` persistante ;
- `DateFormulaExpression.Steps` : snapshot copié puis `Array.AsReadOnly(...)` ;
- `ArraysDifference<T>` : la classe encapsule son stockage et implémente elle-même `IReadOnlyList<T>` ; `ArraysChange<T>.Value` est immutable ;
- `InterpolatedStringParser` : la liste privée est encapsulée par `AsReadOnly()` une seule fois ;
- `GeoPointList<T>` / `GeoPointList2<T>` : `IList<T>` est volontairement l'API de mutation ;
- `QueryString.QueryValues` : vue mutable volontaire faisant partie de l'API de modification du query string ;
- `ExpressionCompilerContext.Symbols` : dictionnaire publiquement mutable par conception ;
- `VirtualProcessor<T>.Breakpoints` : set publiquement mutable par conception ;
- `ReadOnlyRange<T>` : vue live volontaire sur une collection externe ;
- `ExternalResource.DiagnosticMessages` : vue live read-only persistante sur un état interne évolutif ;
- `LRUCache` : vues read-only persistantes déjà créées une seule fois ;
- `CMapFormat0.MapBytes`, `FvarTable` et les autres DTO/tables de fontes à propriétés tableau `get; set;` : modèles explicitement mutables, à distinguer des sous-objets get-only ci-dessus ;
- modèles DNS à propriétés `byte[]` : DTO de protocole mutables, pas des snapshots immuables ;
- méthodes telles que `SmtpClient.EhloAsync/ExpnAsync/HelpAsync` qui retournent un nouveau tableau sans le conserver : le résultat appartient au caller et ne constitue pas une fuite d'état interne ;
- `Brackets.All` : retourne explicitement une nouvelle copie à chaque accès, donc aucune donnée globale mutable n'est exposée ;
- tableaux privés statiques non exposés : aucun changement nécessaire.

---

# Travaux déjà terminés

## Tranche 1

- [x] `SmtpMessage.Recipients` ;
- [x] `DnsLookupException.Failures` ;
- [x] `NtpQueryException.Failures` ;
- [x] `NetworkInterfaceSnapshot.DnsAddresses` ;
- [x] `SqlSyntaxOptions.IdentifierPrefixes` ;
- [x] `ReflectionSerializationContract.Members` ;
- [x] `Bytes` pour les constructions qui doivent copier (l'alias volontaire `AsBytes(byte[])` reste son contrat explicite).

## Tranche 2

- [x] `VirtualMemory<TAddress>.Pages` ;
- [x] `VirtualMemory<TAddress>.Processes` ;
- [x] `Scheduler<T>.Processes` ;
- [x] `CallFrame.Locals` ;
- [x] `ControlFlowStack.Blocks` ;
- [x] `VirtualProcess<TAddress>.Mappings` ;
- [x] `TransactionException.RollbackExceptions`.

## Tranche 3

- [x] `VirtualProcessor<T>.Instructions` : opcode copié une seule fois dans un stockage privé immutable, même instance réutilisée dans la vue publique et comme clé ; comparer de contenu conservé.

`Utils.Parser` a son audit/remédiation séparé déjà terminé ; les recherches transversales ont néanmoins été croisées avec ses patterns pour éviter de réintroduire un faux positif dans ce fichier.

---

# Critères de clôture

Renommer ce fichier en `DONE-immutability-YYYY-MM-DD.md` uniquement lorsque :

- les huit findings ci-dessus sont corrigés ;
- chaque correction possède un test de non-aliasing / impossibilité de mutation adapté ;
- aucune vue live possédée n'alloue un wrapper ou snapshot à chaque getter ;
- les tables statiques globales ne sont plus modifiables par les callers ;
- les comparateurs, ordres et règles de casse sont préservés explicitement ;
- les signatures publiques sont conservées ou les éventuels breaking changes sont assumés/documentés dans le cadre de la version majeure ;
- les tests des projets concernés et les quality gates applicables restent verts.
