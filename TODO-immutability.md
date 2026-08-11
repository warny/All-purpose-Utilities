# TODO — Collections réellement immuables hors `Utils.Parser`

## Objectif

Corriger les types immuables ou utilisés comme snapshots dans les autres projets de la solution lorsqu’ils :

- exposent directement un tableau ;
- conservent un tableau derrière `IReadOnlyList<T>` ou `IReadOnlyCollection<T>` ;
- conservent directement un `List<T>`, `Dictionary<TKey,TValue>`, `HashSet<T>` ou autre collection mutable derrière une interface read-only ;
- ne réalisent pas de copie défensive lors de la construction.

Une interface `IReadOnly*` n’est pas une garantie d’immutabilité.

## Première tranche terminée (2026-08-10)

Les cas suivants sont corrigés et couverts par des tests de non-aliasing :

- [x] `SmtpMessage.Recipients` (construction et affectation par `with`) ;
- [x] `DnsLookupException.Failures` ;
- [x] `NtpQueryException.Failures` ;
- [x] `NetworkInterfaceSnapshot.DnsAddresses` ;
- [x] `SqlSyntaxOptions.IdentifierPrefixes` ;
- [x] `ReflectionSerializationContract.Members` ;
- [x] `Bytes` lors de la construction depuis un `byte[]`.

Les collections séquentielles ci-dessus utilisent désormais des snapshots `ImmutableArray`,
à l’exception de `Bytes`, qui conserve volontairement son tableau strictement privé après en
avoir effectué une copie défensive. `SqlSyntaxOptions` utilise un `ImmutableHashSet<char>`.

## Remaining work

- Auditer et corriger les prochaines tranches hors Parser qui ne faisaient pas partie de cette PR.
- Auditer `ControlFlowStack.Blocks` : cette vue live est exposée sous `IEnumerable<T>`, mais le
  `Stack<T>` interne reste récupérable par cast. Une correction doit préserver l'ordre
  d'énumération et éviter une allocation à chaque lecture.

## Deuxième tranche terminée (2026-08-11)

Les cas suivants sont corrigés et couverts par des tests d'encapsulation :

- [x] `VirtualMemory<TAddress>.Pages` : vue read-only live créée une fois ;
- [x] `VirtualMemory<TAddress>.Processes` : vue read-only live créée une fois ;
- [x] `Scheduler<T>.Processes` : vue read-only live créée une fois ;
- [x] `CallFrame.Locals` : `ReadOnlyDictionary` live créé une fois ;
- [x] `VirtualProcess<TAddress>.Mappings` : snapshot `ImmutableArray` mis en cache avec
  invalidation lazy après chaque mutation de la table des pages ;
- [x] `TransactionException.RollbackExceptions` : copie défensive immutable à la construction.

### Audit ciblé `Utils.VirtualMachine` et `Utils.Transactions`

- `VirtualProcessor<T>.Instructions` expose bien les tableaux possédés servant de clés au
  dictionnaire : un consommateur peut recaster `Opcode` en `byte[]` et modifier la clé après son
  insertion. Le stockage clone les sources externes, mais ce clone interne reste exposé. Ce
  finding est volontairement reporté : corriger le type de clé exige d'adapter et de tester le
  comparateur de séquences, la détection des préfixes, la table rapide et le dispatch sans changer
  leur sémantique.
- `VirtualProcessor<T>.Breakpoints` reste volontairement mutable : la collection constitue l'API
  publique de mutation.
- `ReadOnlyRange<T>` reste volontairement une vue live sur la liste fournie par l'appelant.
- Les tableaux privés statiques réellement encapsulés sont des faux positifs et ne nécessitent
  pas de conversion.
- Les modèles de protocole DNS à propriétés `byte[]` restent des DTO mutables hors périmètre ;
  aucun invariant d'objet immutable n'a été établi pendant cette tranche.

---

## Principe cible commun

Lorsqu’un objet immutable reçoit une collection, effectuer immédiatement une copie défensive et affecter à la propriété une représentation immutable.

### Tableaux / séquences compactes

Utiliser :

```csharp
source.AsReadOnlyArray()
```

lorsque cette extension fournit la représentation read-only standard du projet et effectue bien une copie défensive.

Lorsque le type le permet, préférer directement :

```csharp
source.ToImmutableArray()
```

Ne pas exposer directement le résultat de :

```csharp
source.ToArray()
```

sous `IReadOnlyList<T>` ou `IReadOnlyCollection<T>`.

### Listes

Utiliser :

```csharp
source.ToImmutableList()
```

### Dictionnaires

Utiliser :

```csharp
source.ToImmutableDictionary()
```

avec conservation impérative du comparateur existant.

### Sets

Utiliser :

```csharp
source.ToImmutableHashSet()
```

avec conservation du comparateur existant.

### Autres collections

Utiliser la forme immutable correspondant à la sémantique de la collection :

- `ToImmutableSortedSet`
- `ToImmutableSortedDictionary`
- `ToImmutableQueue`
- `ToImmutableStack`
- etc.

Préserver :

- ordre ;
- unicité ;
- comparateur ;
- sensibilité à la casse ;
- déterminisme.

---

# P1 — `Utils.Net`

## `SmtpMessage`

Actuellement :

```csharp
public sealed record SmtpMessage(
    string From,
    IReadOnlyList<string> Recipients,
    string Data);
```

`Recipients` peut être un tableau ou une liste appartenant à l’appelant. Une modification postérieure de cette collection modifie donc l’état observable du message.

### À faire

Remplacer le record positionnel ou introduire un constructeur contrôlé permettant une copie défensive, par exemple :

```csharp
Recipients = recipients.ToImmutableArray();
```

ou `AsReadOnlyArray()` selon la représentation standard retenue.

### Tests

Vérifier qu’une modification de la collection source après construction ne modifie pas `Recipients` et que la propriété ne permet pas de récupérer le tableau mutable source par cast.

---

## `DnsLookupException.Failures`

La classe effectue déjà :

```csharp
Failures = failures.ToArray();
```

mais expose ensuite le tableau sous `IReadOnlyList<DnsServerFailure>`, ce qui permet de le recaster.

### À faire

Remplacer par :

```csharp
Failures = failures.ToImmutableArray();
```

ou :

```csharp
Failures = failures.AsReadOnlyArray();
```

### Tests

Vérifier :

- la mutation de la source n’affecte pas l’exception ;
- la collection exposée n’est pas un tableau mutable recastable.

---

## `NtpQueryException.Failures`

Même problème que `DnsLookupException`.

Remplacer le `ToArray()` stocké sous `IReadOnlyList<T>` par une représentation immutable.

---

# P1 — `Utils.Data`

## `SqlSyntaxOptions.IdentifierPrefixes`

Actuellement :

```csharp
private readonly HashSet<char> identifierPrefixes;

public IReadOnlyCollection<char> IdentifierPrefixes
    => identifierPrefixes;
```

Le `HashSet<char>` interne peut être récupéré par cast puis modifié, ce qui modifie ensuite le résultat de `IsIdentifierPrefix(...)`.

### À faire

Stocker une collection réellement immutable, par exemple :

```csharp
private readonly ImmutableHashSet<char> identifierPrefixes;
```

et construire avec :

```csharp
identifierPrefixes = resolvedPrefixes.ToImmutableHashSet();
```

Préserver la logique ajoutant automatiquement `AutoParameterPrefix` à l’ensemble.

### Tests

Vérifier qu’une tentative de mutation externe ne peut plus modifier le comportement de `IsIdentifierPrefix`.

---

# P1 — cœur `Utils`

## `Utils.Objects.Bytes`

`Bytes` est un `readonly struct` présenté comme une vue read-only sur des octets.

Le constructeur interne conserve actuellement directement le tableau fourni :

```csharp
internal Bytes(params byte[] byteArray)
{
    _innerBytes = byteArray ?? [];
}
```

et l’opérateur public :

```csharp
public static implicit operator Bytes(byte[] bytes)
    => new Bytes(bytes);
```

permet donc à l’appelant de conserver et modifier la référence mutable après construction.

### À faire

Effectuer une copie défensive lors de la construction.

Ici, un tableau privé peut rester acceptable si celui-ci n’est jamais exposé directement :

```csharp
_innerBytes = byteArray?.ToArray() ?? [];
```

Si cela reste performant et cohérent avec le type, `ImmutableArray<byte>` peut également être envisagé.

Le point obligatoire est que la source mutable de l’appelant ne soit jamais conservée.

### Tests

Ajouter notamment :

```csharp
byte[] source = [1, 2, 3];
Bytes bytes = source;
source[0] = 42;
Assert.AreEqual(1, bytes[0]);
```

---

# P3 — `Utils.VirtualMachine`

## `VirtualProcess<TAddress>.Mappings`

La propriété construit actuellement un nouveau tableau avec `ToArray()` puis le retourne sous `IReadOnlyList<...>`.

La mutation de ce tableau ne modifie pas `_pageTable`, donc il ne s’agit pas d’une rupture d’encapsulation du `VirtualProcess`. En revanche, la documentation parle d’un `immutable snapshot` alors que le snapshot retourné reste lui-même mutable.

### À faire

Pour rendre le contrat strictement conforme à la documentation, retourner :

```csharp
.ToImmutableArray()
```

ou :

```csharp
.AsReadOnlyArray()
```

### Priorité

P3 : amélioration de contrat plutôt que défaut fonctionnel.

---

# Recherche complémentaire obligatoire sur toute la solution

La liste ci-dessus correspond aux cas déjà confirmés pendant l’audit initial. La correction ne doit pas s’y limiter.

Effectuer une recherche systématique dans tous les projets hors Parser avec notamment :

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

Identifier en priorité les types :

- `record` ;
- `readonly record struct` ;
- `readonly struct` ;
- options/configuration avec getters uniquement ;
- résultats d’opération ;
- snapshots ;
- diagnostics ;
- exceptions transportant une liste de causes ;
- DTO explicitement documentés immutable/read-only.

Pour chaque résultat, déterminer si la collection mutable peut modifier l’état observable du type après construction ou être récupérée par cast.

---

# Faux positifs à ne pas modifier mécaniquement

Ne pas remplacer les implémentations qui assurent déjà correctement l’immutabilité ou l’encapsulation.

## `ProtocolResponseException.Responses`

Le pattern copie puis enveloppe dans une `ReadOnlyCollection<T>`. Le tableau n’est pas exposé directement.

## `NetworkParameters.NetworkInterfaces`

Utilise `Array.AsReadOnly(...)` : le tableau interne n’est pas directement récupérable via la propriété.

## `NetworkParameters.DnsServers`

Retourne volontairement un tableau, mais une copie défensive différente à chaque appel.

## `CmapTable.CMaps`

Utilise une `ReadOnlyCollection<T>` construite à partir d’un snapshot.

## `ExternalResource.DiagnosticMessages`

Utilise `AsReadOnly()` sur une collection interne volontairement évolutive ; il s’agit d’une vue live et non d’un objet immutable.

## `ArraysChange<T>.Value`

Le stockage est créé avec `ToImmutableArray()` et ne nécessite pas de correction.

---

# Tests génériques

Pour chaque type corrigé, ajouter au minimum les scénarios suivants.

## Tableau source

```csharp
T[] source = [...];
var value = new Model(source);
source[0] = other;
Assert.AreNotEqual(other, value.Items[0]);
```

## Liste source

```csharp
var source = new List<T> { initial };
var value = new Model(source);
source[0] = other;
Assert.AreEqual(initial, value.Items[0]);
```

## Dictionnaire source

```csharp
var source = new Dictionary<TKey, TValue>
{
    [key] = value1
};

var model = new Model(source);
source[key] = value2;
Assert.AreEqual(value1, model.Values[key]);
```

## Set source

```csharp
var source = new HashSet<T> { value1 };
var model = new Model(source);
source.Add(value2);
Assert.IsFalse(model.Values.Contains(value2));
```

## Cast vers une collection mutable

Lorsque la propriété est exposée sous une interface read-only, vérifier qu’elle ne correspond plus directement au tableau, à la liste, au dictionnaire ou au set mutable utilisé comme stockage.

---

# Critères d’acceptation

Le TODO est terminé lorsque :

- toutes les classes réellement immuables de la solution hors Parser ont été auditées ;
- toutes les collections fournies par l’appelant sont copiées défensivement ;
- les collections conservées sont réellement immutables ;
- aucun tableau interne n’est directement exposé sous une interface read-only ;
- aucun `List<T>`, `Dictionary<TKey,TValue>`, `HashSet<T>` ou autre collection mutable interne n’est exposé derrière une interface read-only ;
- les comparateurs et règles d’ordre existants sont conservés ;
- les tests prouvent l’absence d’alias avec les collections sources ;
- les snapshots documentés comme immuables le sont effectivement ;
- les implémentations déjà correctes ne sont pas modifiées inutilement ;
- tous les tests des projets concernés restent verts.
