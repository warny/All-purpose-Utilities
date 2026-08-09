# TODO — Immutabilité des modèles `Utils.Parser`

## Objectif

Auditer et corriger l’ensemble des types de la famille `Utils.Parser` qui sont immuables, présentés comme immuables, ou utilisés comme snapshots/objets de valeur, mais qui exposent encore indirectement des collections mutables.

Le problème visé concerne notamment les propriétés de type :

- `IReadOnlyList<T>`
- `IReadOnlyCollection<T>`
- `IReadOnlySet<T>`
- `IReadOnlyDictionary<TKey, TValue>`
- tableaux exposés directement ou masqués derrière une interface read-only
- toute autre interface read-only dont l’instance concrète reste mutable

Une interface `IReadOnly*` ne garantit pas l’immutabilité de l’objet sous-jacent. Un tableau, une `List<T>`, un `HashSet<T>` ou un `Dictionary<TKey,TValue>` peut être recasté vers son type concret et modifié.

Exemple de défaut :

```csharp
var items = new SomeType[] { value1 };
var model = new Model(items);
items[0] = value2;
```

La mutation de `items` ne doit jamais modifier l’état observable de `model`.

Le pattern suivant est également insuffisant :

```csharp
public IReadOnlyList<T> Items { get; }

Items = source.ToArray();
```

car le résultat reste un `T[]` et peut être récupéré par cast :

```csharp
var array = (T[])model.Items;
array[0] = other;
```

L’objectif est une **immutabilité effective**, et non seulement une surface statiquement read-only.

---

## Principe cible de correction

Lors de la construction d’un objet immutable, toute collection reçue de l’extérieur doit être copiée défensivement avant d’être affectée à la propriété.

### Tableaux / séquences compactes

Préférer :

```csharp
Items = source.AsReadOnlyArray();
```

lorsque l’extension `AsReadOnlyArray` correspond au contrat attendu du projet et réalise bien une copie défensive.

Lorsque le type le permet, utiliser directement :

```csharp
Items = source.ToImmutableArray();
```

Ne pas utiliser `ToArray()` si le tableau obtenu est ensuite exposé directement derrière `IReadOnlyList<T>` ou `IReadOnlyCollection<T>`.

### Listes

Utiliser :

```csharp
Items = source.ToImmutableList();
```

### Dictionnaires

Utiliser :

```csharp
Values = source.ToImmutableDictionary();
```

avec conservation impérative du comparateur existant lorsque nécessaire.

### Sets

Utiliser :

```csharp
Values = source.ToImmutableHashSet();
```

avec conservation du comparateur existant.

### Autres formes

Utiliser la forme `ToImmutable...` correspondant à la sémantique de la collection :

- `ToImmutableSortedSet`
- `ToImmutableSortedDictionary`
- `ToImmutableQueue`
- `ToImmutableStack`
- etc.

Le choix doit préserver :

- l’ordre ;
- l’unicité ;
- le comparateur ;
- la sensibilité à la casse ;
- le déterminisme.

---

## Règles générales

Pour chaque type corrigé :

1. Ne jamais conserver directement une collection fournie par l’appelant.
2. Ne jamais considérer `IReadOnly*` comme une garantie d’immutabilité.
3. Effectuer la copie défensive à la construction.
4. Affecter à la propriété une représentation réellement immutable.
5. Éviter de refaire une copie à chaque lecture de propriété.
6. Préserver l’ordre et les comparateurs existants.
7. Préserver les règles de déduplication existantes.
8. Ne pas modifier la sémantique fonctionnelle du parser.
9. Ajouter des tests démontrant que la mutation de la collection source ne modifie plus l’objet construit.
10. Ajouter des tests démontrant que la propriété retournée ne peut pas être recastée vers la collection mutable initiale.
11. Examiner les `with` expressions des records et empêcher qu’elles permettent de réinjecter directement une collection mutable dans un objet supposé immutable.

---

# P1 — Modèle public `Utils.Parser.Model`

## `ParserDefinition`

**Status: DONE (first tranche).**

**Resolution:** External collections are normalized at construction and in collection `init` setters into immutable snapshots; ordinal comparers and source order are preserved.

**Files:** Public model files in `Utils.Parser/Model`, `Utils.Parser/Runtime/ParseNode.cs`, and `Utils.Parser.Antlr4.Common`.

**Tests:** `UtilsTest/Parser/ParserModelImmutabilityTests.cs` covers source mutation, mutable-type recasts, and `with`/object-initializer normalization.

**API impact:** Existing collection interface signatures and record construction patterns are retained; `init` assignments now normalize rather than retain the assigned instance.


Le type est explicitement documenté comme une description immutable de la grammaire.

Auditer et corriger au minimum :

```text
Actions
Imports
Modes
DeclaredTokens
DeclaredChannels
ExtensionBindings
ParserRules
AllRules
LeftRecursiveRules
```

Attendu :

- séquences -> `ToImmutableArray()` ou `AsReadOnlyArray()` ;
- listes -> `ToImmutableList()` ;
- sets -> `ToImmutableHashSet(...)` ;
- dictionnaires -> `ToImmutableDictionary(...)`.

Préserver les comparateurs actuellement utilisés, notamment `StringComparer.Ordinal` lorsque c’est le contrat en place.

### Attention `init` / `with`

Plusieurs propriétés sont `init`. Vérifier qu’une construction telle que :

```csharp
var copy = definition with { ParserRules = mutableArray };
```

ne permet pas de réintroduire une collection mutable.

Si nécessaire, remplacer certaines propriétés `init` par une construction contrôlée ou un modèle explicitement immutable.

---

## `Rule` et métadonnées associées

**Status: DONE (first tranche).**

**Resolution:** External collections are normalized at construction and in collection `init` setters into immutable snapshots; ordinal comparers and source order are preserved.

**Files:** Public model files in `Utils.Parser/Model`, `Utils.Parser/Runtime/ParseNode.cs`, and `Utils.Parser.Antlr4.Common`.

**Tests:** `UtilsTest/Parser/ParserModelImmutabilityTests.cs` covers source mutation, mutable-type recasts, and `with`/object-initializer normalization.

**API impact:** Existing collection interface signatures and record construction patterns are retained; `init` assignments now normalize rather than retain the assigned instance.


### `Rule`

Auditer et corriger au minimum :

```text
Parameters
Returns
Locals
```

Examiner également toutes les collections transitivement référencées par `Rule`.

### `RuleExceptionMetadata`

Corriger :

```text
Throws
CatchClauses
```

### `RuleOptions`

Corriger :

```text
Values
```

vers un dictionnaire immutable préservant le comparateur existant.

---

# P1 — Arbre `RuleContent`

**Status: DONE (first tranche).**

**Resolution:** External collections are normalized at construction and in collection `init` setters into immutable snapshots; ordinal comparers and source order are preserved.

**Files:** Public model files in `Utils.Parser/Model`, `Utils.Parser/Runtime/ParseNode.cs`, and `Utils.Parser.Antlr4.Common`.

**Tests:** `UtilsTest/Parser/ParserModelImmutabilityTests.cs` covers source mutation, mutable-type recasts, and `with`/object-initializer normalization.

**API impact:** Existing collection interface signatures and record construction patterns are retained; `init` assignments now normalize rather than retain the assigned instance.


Auditer tous les records dérivant de `RuleContent`.

Cas déjà identifiés :

### `CharSetMatch`

```text
Chars
```

Convertir en set immutable.

### `EmbeddedAction`

```text
Labels
```

Convertir en collection immutable.

### `Sequence`

```text
Items
```

Cette collection est structurelle et doit être réellement immutable.

### `Alternation`

```text
Alternatives
```

Même exigence.

Passer en revue tous les autres descendants de `RuleContent` pour identifier d’éventuelles collections ajoutées depuis cet audit.

---

# P1 — `LexerMode`

**Status: DONE (first tranche).**

**Resolution:** External collections are normalized at construction and in collection `init` setters into immutable snapshots; ordinal comparers and source order are preserved.

**Files:** Public model files in `Utils.Parser/Model`, `Utils.Parser/Runtime/ParseNode.cs`, and `Utils.Parser.Antlr4.Common`.

**Tests:** `UtilsTest/Parser/ParserModelImmutabilityTests.cs` covers source mutation, mutable-type recasts, and `with`/object-initializer normalization.

**API impact:** Existing collection interface signatures and record construction patterns are retained; `init` assignments now normalize rather than retain the assigned instance.


Corriger :

```text
Rules
```

Le tableau ou la liste fourni au constructeur ne doit plus pouvoir modifier le `LexerMode` après sa construction.

---

# P1 — Arbre de parsing

**Status: DONE (first tranche).**

**Resolution:** External collections are normalized at construction and in collection `init` setters into immutable snapshots; ordinal comparers and source order are preserved.

**Files:** Public model files in `Utils.Parser/Model`, `Utils.Parser/Runtime/ParseNode.cs`, and `Utils.Parser.Antlr4.Common`.

**Tests:** `UtilsTest/Parser/ParserModelImmutabilityTests.cs` covers source mutation, mutable-type recasts, and `with`/object-initializer normalization.

**API impact:** Existing collection interface signatures and record construction patterns are retained; `init` assignments now normalize rather than retain the assigned instance.


## `ParserNode`

Corriger :

```text
Children
```

## `QuantifierNode`

Corriger également :

```text
Children
```

Un parse tree produit par le moteur doit être stable. Une modification externe d’une collection utilisée pendant sa construction ne doit jamais modifier rétrospectivement :

- la navigation dans l’arbre ;
- les enfants ;
- l’égalité structurelle ;
- les diagnostics ;
- les résultats dérivés.

---

# P1 — `LeftRecursiveRuleInfo`

**Status: DONE (first tranche).**

**Resolution:** External collections are normalized at construction and in collection `init` setters into immutable snapshots; ordinal comparers and source order are preserved.

**Files:** Public model files in `Utils.Parser/Model`, `Utils.Parser/Runtime/ParseNode.cs`, and `Utils.Parser.Antlr4.Common`.

**Tests:** `UtilsTest/Parser/ParserModelImmutabilityTests.cs` covers source mutation, mutable-type recasts, and `with`/object-initializer normalization.

**API impact:** Existing collection interface signatures and record construction patterns are retained; `init` assignments now normalize rather than retain the assigned instance.


Corriger :

```text
BaseAlternatives
RecursiveAlternatives
```

Les propriétés `required init` ne constituent pas une protection contre une collection mutable. Examiner en particulier les initialisations par object initializer et les `with` expressions.

---

# P1 — `GrammarExtensionBinding`

**Status: DONE (first tranche).**

**Resolution:** External collections are normalized at construction and in collection `init` setters into immutable snapshots; ordinal comparers and source order are preserved.

**Files:** Public model files in `Utils.Parser/Model`, `Utils.Parser/Runtime/ParseNode.cs`, and `Utils.Parser.Antlr4.Common`.

**Tests:** `UtilsTest/Parser/ParserModelImmutabilityTests.cs` covers source mutation, mutable-type recasts, and `with`/object-initializer normalization.

**API impact:** Existing collection interface signatures and record construction patterns are retained; `init` assignments now normalize rather than retain the assigned instance.


Corriger :

```text
LexerRuleNames
DeclaredTokens
DeclaredChannels
```

Utiliser des sets immutables et préserver `StringComparer.Ordinal` lorsque c’est le comparateur existant.

---

# P1 — `Utils.Parser.Antlr4.Common`

**Status: DONE (first tranche).**

**Resolution:** External collections are normalized at construction and in collection `init` setters into immutable snapshots; ordinal comparers and source order are preserved.

**Files:** Public model files in `Utils.Parser/Model`, `Utils.Parser/Runtime/ParseNode.cs`, and `Utils.Parser.Antlr4.Common`.

**Tests:** `UtilsTest/Parser/ParserModelImmutabilityTests.cs` covers source mutation, mutable-type recasts, and `with`/object-initializer normalization.

**API impact:** Existing collection interface signatures and record construction patterns are retained; `init` assignments now normalize rather than retain the assigned instance.


## `Antlr4PrequelModel`

Corriger :

```text
Imports
Actions
DeclaredTokens
DeclaredChannels
```

Les collections de noms doivent utiliser la forme immutable correspondant à leur sémantique réelle.

## `Antlr4OptionSet`

Corriger :

```text
Values
```

vers un dictionnaire immutable.

## `Antlr4PrequelValidationResult`

Corriger :

```text
Diagnostics
```

vers un snapshot immutable.

---

# P1 — `Utils.Parser.Expressions`

**Status: OPEN — reserved for a subsequent PR.**

## `PreparedExpressionEmbeddedCodeRegistryBuildResult`

Cas prioritaire : le constructeur réalise déjà des copies avec `ToArray()`, mais stocke ensuite directement ces tableaux derrière `IReadOnlyList<T>`.

Corriger :

```text
SuccessfulSemanticPredicates
SuccessfulParserActions
NonSuccessEntries
DuplicateEntries
SkippedEntries
AllEntries
```

Remplacer le pattern :

```csharp
Property = source.ToArray();
```

par :

```csharp
Property = source.ToImmutableArray();
```

ou `AsReadOnlyArray()` si c’est la représentation standard retenue.

## `PreparedExpressionEmbeddedCodeRegistryBuildEntry`

Corriger :

```text
DiagnosticArguments
```

Le tableau issu de `ToArray()` ne doit plus rester recastable.

---

# P1 — Résultats d’exécution

**Status: OPEN — reserved for a subsequent PR.**

## `ParserActionExecutionOutcome`

Corriger :

```text
DiagnosticArguments
```

Le constructeur ne doit pas conserver directement la collection reçue.

Le cas `params object?[] diagnosticArguments` doit faire l’objet d’un test spécifique : le tableau `params` appartient à l’appelant et doit être copié avant conservation.

---

# P1/P2 — Résumés runtime publics

**Status: OPEN — reserved for a subsequent PR.**

## `RuntimeTraceSummary`

Corriger :

```text
EventDistribution
StatusDistribution
RuleDistribution
AlternativeDistribution
```

vers des dictionnaires immutables en conservant leurs comparateurs.

## `RuntimeTraceComparison`

Corriger :

```text
EventCountDelta
```

vers un dictionnaire immutable.

---

# P2 — Modèles internes du scheduler/runtime

**Status: OPEN — reserved for a subsequent PR.**

Même si ces types sont internes, ils sont souvent explicitement documentés comme immuables et servent de snapshots entre phases du parser.

Corriger au minimum :

## `PreparedSchedulingInputs`

```text
StructuralDescriptors
LookaheadProbes
SharedPrefixCandidates
ContinuationDescriptors
```

## `AlternativeStructuralDescriptor`

```text
StructuralTokens
```

La documentation actuelle demandant aux appelants de ne pas caster/muter la collection doit devenir inutile une fois l’invariant réellement garanti.

## `ParserLookaheadProbeResult`

```text
ExpectedTokenNames
```

## `ParserLookaheadSharedPrefixCandidate`

```text
AlternativeIndexes
```

## `ParserContinuationDescriptor`

```text
ExpectedTokenNames
```

## `ParserContinuationPreparationInput`

```text
ExpectedTokenNames
```

## `ParserSharedPrefixPlan`

```text
AlternativeIndexes
Continuations
```

## `ParserSharedPrefixSegment`

```text
StructuralTokens
```

## `ParserSharedPrefixBoundary`

```text
ExpectedTokenNames
```

---

# P2 — `GrammarImportCompositionPlanner`

**Status: OPEN — reserved for a subsequent PR.**

Auditer tous les records internes représentant le résultat de composition.

Cas connus :

## `GrammarDependencyEdge`

```text
ImportPath
```

## `AmbiguousGrammarDependency`

```text
Candidates
```

## `GrammarImportCycle`

```text
Path
```

## `EffectiveGrammarRule`

```text
ImportPath
```

## `GrammarRuleCollision`

```text
Candidates
```

## `GrammarImportCompositionPlan`

Corriger toutes les collections du plan :

```text
Grammars
Dependencies
Cycles
MissingDependencies
AmbiguousDependencies
EffectiveRules
MaskedRules
IgnoredRules
Collisions
TokenVocabLexerRules
```

Le plan est explicitement présenté comme immutable. Des `ToArray()` fournis au record ne suffisent pas si ces tableaux restent exposés derrière `IReadOnlyList<T>`.

---

# Recherche complémentaire obligatoire

La correction ne doit pas se limiter aux cas déjà identifiés.

Effectuer une recherche complète dans :

```text
Utils.Parser/
Utils.Parser.Antlr4.Common/
Utils.Parser.Expressions/
Utils.Parser.Diagnostics/
Utils.Parser.Source/
Utils.Parser.Generators/
```

Chercher notamment :

```text
IReadOnlyList<
IReadOnlyCollection<
IReadOnlySet<
IReadOnlyDictionary<
ToArray()
new List<
new Dictionary<
new HashSet<
params ...[]
```

Pour chaque résultat, déterminer si le type propriétaire est :

- immutable ;
- un record représentant un objet de valeur ;
- un snapshot ;
- un résultat d’analyse ;
- un résultat de parsing ;
- un modèle de grammaire ;
- une structure passée entre phases du parser.

Si oui, appliquer le même principe de copie défensive et de stockage immutable.

---

# Tests requis

Ajouter une suite de tests dédiée aux invariants d’immutabilité.

## Mutation de la source

```csharp
var source = new[] { value1 };
var model = new Sequence(source);
source[0] = value2;
Assert.Same(value1, model.Items[0]);
```

## Absence de tableau exposé

Lorsque la propriété publique est `IReadOnlyList<T>` :

```csharp
Assert.IsFalse(model.Items is T[]);
```

si la représentation retenue doit empêcher ce cast.

## Absence de `List<T>` mutable

```csharp
Assert.IsFalse(model.Items is List<T>);
```

## Dictionnaires

```csharp
var source = new Dictionary<string, string>
{
    ["a"] = "1"
};

var model = new RuleOptions(source);
source["a"] = "2";
Assert.AreEqual("1", model.Values["a"]);
```

Vérifier également que `model.Values` n’est ni le dictionnaire source ni un dictionnaire mutable exposé.

## Sets

Même principe avec `HashSet<T>`.

## `params`

Ajouter un test spécifique pour `ParserActionExecutionOutcome`.

## Records et `with`

Pour chaque record public avec collection, vérifier qu’un `with` ne permet pas de contourner l’immutabilité. Si C# ne permet pas d’intercepter correctement une affectation `init`, revoir la conception du type plutôt que de laisser cette brèche.

---

# Critères d’acceptation

Le TODO est terminé lorsque :

- aucune collection mutable fournie par l’appelant n’est conservée directement dans un modèle immutable ;
- aucune propriété immutable ne retourne directement le tableau utilisé comme stockage ;
- aucun `List<T>`, `Dictionary<TKey,TValue>`, `HashSet<T>` ou équivalent mutable n’est exposé derrière une interface read-only depuis un objet immutable ;
- les comparateurs existants sont préservés ;
- les objets construits restent inchangés après mutation des collections sources ;
- les records restent immuables après copie par `with` ;
- les tests couvrent tableaux, listes, dictionnaires, sets et paramètres `params` ;
- la documentation ne repose plus sur des formulations telles que « callers must not mutate/cast this collection » ;
- aucune modification du comportement fonctionnel du parser n’est introduite ;
- tous les tests Parser existants restent verts.
