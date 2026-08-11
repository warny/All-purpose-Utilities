# TODO — Audit transversal des collections et tableaux exposés

## Objectif

Compléter l’audit d’immutabilité hors `Utils.Parser` avec une règle plus précise que la simple recherche de `IReadOnly*` :

1. **Collection reçue de l’extérieur et conservée par un objet immutable/snapshot** : faire une copie défensive puis stocker une représentation immutable (`ImmutableArray`, `ImmutableList`, `ImmutableDictionary`, `ImmutableHashSet`, etc.).
2. **Collection mutable créée et possédée par la classe** : elle peut rester mutable en interne si la classe doit évoluer, mais elle ne doit pas être exposée directement derrière une interface `IReadOnly*`. Créer une vue read-only une seule fois (constructeur / initialisation du champ) et réutiliser cette vue à chaque getter.
3. **Snapshot d’un état mutable** : le snapshot doit être réellement immutable. Éviter de recréer un simple wrapper read-only à chaque getter. Si le snapshot doit refléter un état qui évolue, préférer le reconstruire au moment des mutations puis retourner le snapshot pré-calculé sans allocation au getter.
4. **Vue live explicitement voulue** : conserver la vue live, mais empêcher le cast vers la collection mutable sous-jacente.

Principe performance : **pas de `AsReadOnly()`, `new ReadOnlyCollection(...)`, `ToImmutableArray()` ou `ToArray()` uniquement pour protéger un getter à chaque accès** lorsqu’une vue ou un snapshot peut être créé une fois et réutilisé.

---

## Tranche déjà terminée

Les cas suivants ont déjà été corrigés :

- [x] `SmtpMessage.Recipients`
- [x] `DnsLookupException.Failures`
- [x] `NtpQueryException.Failures`
- [x] `NetworkInterfaceSnapshot.DnsAddresses`
- [x] `SqlSyntaxOptions.IdentifierPrefixes`
- [x] `ReflectionSerializationContract.Members`
- [x] `Bytes` pour les chemins devant copier le `byte[]` (le contrat volontairement aliasant `AsBytes()` reste conservé)

---

# P1 — Collections possédées par la classe mais exposées directement

## `Utils.VirtualMachine/VirtualMemory.cs`

### `VirtualMemory<TAddress>.Pages`

Actuellement :

```csharp
private readonly List<VirtualPage> _pages = [];

public IReadOnlyList<VirtualPage> Pages => _pages;
```

Le getter est typé read-only, mais le consommateur peut faire :

```csharp
var pages = (List<VirtualPage>)memory.Pages;
pages.Clear();
```

Cela contourne complètement les invariants de `VirtualMemory` (`AllocatePage`, `FreePage`, mapping du master process, ownership des pages).

### À faire

Conserver `_pages` mutable en interne, mais créer une vue read-only **une seule fois** :

```csharp
private readonly List<VirtualPage> _pages = [];
private readonly IReadOnlyList<VirtualPage> _pagesView;

public VirtualMemory(...)
{
    _pagesView = _pages.AsReadOnly();
    ...
}

public IReadOnlyList<VirtualPage> Pages => _pagesView;
```

Adapter le pattern si un helper du projet existe déjà. Ne pas recréer la vue à chaque accès.

### Tests

- vérifier que `Pages` reste une vue live ;
- vérifier qu’une page ajoutée après avoir lu `Pages` apparaît dans la vue ;
- vérifier que `Pages` n’est pas un `List<VirtualPage>` ;
- vérifier qu’une tentative via `IList<T>` / collection mutable échoue ;
- vérifier qu’aucune allocation n’est nécessaire au getter après construction.

---

### `VirtualMemory<TAddress>.Processes`

Même défaut :

```csharp
private readonly List<VirtualProcess<TAddress>> _processes = [];
public IReadOnlyList<VirtualProcess<TAddress>> Processes => _processes;
```

### À faire

Créer une vue read-only une fois et la réutiliser. La vue doit rester live afin que `CreateProcess` / `FreeProcess` soient reflétés sans snapshot ni allocation par getter.

### Tests

Même famille de tests que `Pages`.

---

## `Utils.VirtualMachine/Scheduler.cs`

### `Scheduler<T>.Processes`

Actuellement :

```csharp
private readonly List<ScheduledProcess<T>> _processes = [];
public IReadOnlyList<ScheduledProcess<T>> Processes => _processes;
```

Le cast vers `List<ScheduledProcess<T>>` permet de supprimer/ajouter des processus sans passer par `AddProcess` / `RemoveProcess`, donc de contourner les invariants du scheduler.

### À faire

Créer une vue read-only live une seule fois (constructeur) et retourner toujours cette vue.

### Tests

- vue live après `AddProcess` / `RemoveProcess` ;
- pas de cast vers `List<T>` ;
- pas de copie/snapshot à chaque getter.

---

## `Utils.VirtualMachine/CallFrame.cs`

### `CallFrame.Locals`

Actuellement :

```csharp
private readonly Dictionary<string, object?> _locals = [];
public IReadOnlyDictionary<string, object?> Locals => _locals;
```

Le consommateur peut recaster vers `Dictionary<string, object?>` puis modifier le frame sans passer par `SetLocal`.

### À faire

Conserver `_locals` mutable en interne et créer une vue read-only live une seule fois :

```csharp
private readonly Dictionary<string, object?> _locals = [];
private readonly IReadOnlyDictionary<string, object?> _localsView;
```

Initialiser `_localsView` avec un wrapper read-only autour du dictionnaire dans le constructeur. Préserver la sémantique de **vue live**, pas de snapshot.

### Tests

- `SetLocal` doit être immédiatement visible depuis une référence `Locals` récupérée avant l’appel ;
- impossible de récupérer le dictionnaire mutable par cast ;
- aucun wrapper nouveau à chaque getter.

---

# P1 — Collection externe conservée sans copie défensive

## `Utils/Transactions/TransactionExecutor.cs`

### `TransactionException.RollbackExceptions`

Actuellement :

```csharp
public IReadOnlyList<Exception> RollbackExceptions { get; }

public TransactionException(
    Exception primaryException,
    IReadOnlyList<Exception> rollbackExceptions)
{
    ...
    RollbackExceptions = rollbackExceptions;
}
```

Le constructeur conserve directement une collection appartenant à l’appelant.

### À faire

Snapshotter une seule fois au constructeur :

```csharp
RollbackExceptions = rollbackExceptions.ToImmutableArray();
```

Préserver l’ordre des exceptions et le contrat `IReadOnlyList<Exception>` public.

### Tests

- mutation de la `List<Exception>` source après construction sans effet ;
- pas de `Exception[]`/`List<Exception>` recastable exposé ;
- message construit avec le même nombre d’exceptions.

---

# P2 — `VirtualProcess<TAddress>.Mappings`

## Constat

`Mappings` construit aujourd’hui un nouveau tableau à chaque accès :

```csharp
return _pageTable
    .Select(kv => (kv.Key, kv.Value.Page, kv.Value.Access))
    .ToArray();
```

Le snapshot est **sémantiquement pertinent** : une valeur déjà obtenue doit rester stable même si des mappings sont ajoutés ou supprimés ensuite. En revanche :

- le tableau retourné est mutable par cast ;
- une allocation complète est faite à chaque getter ;
- créer seulement un `ReadOnlyCollection`/`ImmutableArray` à chaque getter corrigerait le contrat mais conserverait les allocations répétées.

## À faire

Maintenir un snapshot immutable mis à jour **aux frontières de mutation** de `_pageTable`, et faire du getter une lecture sans allocation.

Pattern cible :

```csharp
private ImmutableArray<(TAddress VirtualPageIndex, VirtualPage Page, PageAccess Access)>
    _mappingsSnapshot = ImmutableArray<(TAddress, VirtualPage, PageAccess)>.Empty;

public IReadOnlyList<(TAddress VirtualPageIndex, VirtualPage Page, PageAccess Access)> Mappings
{
    get
    {
        ThrowIfFreed();
        return _mappingsSnapshot;
    }
}
```

Après toute mutation réussie de `_pageTable`, reconstruire le snapshot une fois :

- `MapPage`
- `UnmapPage`
- `ClearAllMappings`
- `RemoveMappingsForPage`

Par exemple via une méthode privée unique :

```csharp
private void RefreshMappingsSnapshot()
{
    _mappingsSnapshot = _pageTable
        .Select(kv => (kv.Key, kv.Value.Page, kv.Value.Access))
        .ToImmutableArray();
}
```

Ne pas introduire un wrapper supplémentaire au getter.

### Point à vérifier

Préserver l’ordre observable actuel du `Dictionary` (ordre d’énumération d’insertion dans l’implémentation actuelle). Ne pas convertir en set/dictionary immutable si cela change cet ordre.

### Tests

- deux lectures consécutives sans mutation doivent pouvoir retourner la même instance logique / même stockage immutable ;
- un snapshot déjà obtenu reste inchangé après `MapPage` / `UnmapPage` ;
- une nouvelle lecture après mutation reflète le nouvel état ;
- impossible de recaster vers un tableau mutable ;
- `FreeProcess` et `FreePage(force: true)` rafraîchissent correctement le snapshot ;
- getter sans reconstruction quand aucune mutation n’a eu lieu.

---

# P2 — Audit des tableaux publics des DTO/protocoles

La recherche `public byte[]` retourne de nombreux DTO/protocol records, surtout dans `Utils.Net/DNS` (`DS.Digest`, `KEY`, `SIG`, `RRSIG`, `NSEC`, `NSEC3`, `DHCID`, etc.). Ils ne doivent **pas** être convertis mécaniquement en immutable : plusieurs de ces types sont des modèles de protocole mutables avec setters publics utilisés par la sérialisation.

### À faire avant toute correction de cette famille

Pour chaque propriété tableau :

1. déterminer si le type est conçu comme mutable (`get; set;`) ou comme value/snapshot ;
2. déterminer si le tableau est affecté par l’appelant puis conservé ;
3. déterminer si l’objet doit prendre ownership de la donnée ;
4. vérifier les contraintes du serializer/reflection (`DNSField`, constructeurs, setters) ;
5. ne modifier que les types dont le contrat implique réellement une ownership interne ou une immutabilité.

Ne pas changer ces DTO uniquement parce qu’ils contiennent un `byte[]`.

---

# Cas explicitement considérés sûrs / à conserver

## `DateFormulaExpression.Steps`

Le constructeur fait déjà une matérialisation puis un wrapper read-only :

```csharp
Steps = Array.AsReadOnly(steps.ToArray());
```

La protection est créée une seule fois au constructeur. Pas de correction nécessaire.

## `LRUCache<K,V>.Keys` / `Values`

Le cache crée `_keysView` et `_valuesView` une seule fois au constructeur et retourne ensuite ces vues. C’est exactement le pattern attendu pour une collection interne mutable avec exposition read-only/live et sans allocation au getter.

## `ReadOnlyRange<T>`

Il s’agit volontairement d’une **vue** sur une `IReadOnlyList<T>` externe, pas d’un snapshot possédé par le range. Ne pas transformer en copie immutable sauf changement explicite de contrat.

## `VirtualProcessor<T>.Instructions`

La propriété expose une projection/énumération live de l’instruction set et la documentation l’annonce comme telle. Ce n’est pas une fuite directe du `Dictionary`. Ne pas transformer en snapshot sans besoin fonctionnel.

## `VirtualProcessor<T>.Breakpoints`

Collection publiquement mutable par design : les consommateurs doivent pouvoir ajouter/supprimer des breakpoints. Hors périmètre de l’immutabilité.

## `Authenticator`

La clé est copiée défensivement au constructeur et `ExportKey()` retourne une copie. Aucun tableau interne sensible n’est exposé.

## `QueryString`

Type explicitement mutable (`IList` via `QueryValues`, `Add`, `Remove`, `Clear`). Les listes internes sont la représentation de cette mutabilité publique ; hors périmètre.

---

# Recherche complémentaire à poursuivre avant de fermer le chantier

Rechercher dans tous les projets hors Parser :

```text
public IReadOnlyList<
public IReadOnlyCollection<
public IReadOnlyDictionary<
public IReadOnlySet<
private readonly List<
private readonly Dictionary<
private readonly HashSet<
private readonly ...[]
public ...[]
.ToArray()
.AsReadOnly()
Array.AsReadOnly(
new ReadOnlyCollection<
ToImmutableArray()
ToImmutableList()
ToImmutableDictionary()
ToImmutableHashSet()
```

Pour chaque résultat, classifier explicitement :

- **immutable snapshot** → copie défensive + collection immutable une fois ;
- **owned mutable state + live read-only view** → wrapper read-only créé une fois ;
- **owned mutable state + snapshot API** → snapshot immutable reconstruit lors des mutations, getter allocation-free ;
- **mutable public API volontaire** → ne pas corriger ;
- **view sur une collection externe volontaire** → ne pas corriger sans changement de contrat.

---

# Règles de mise en œuvre

## Collections possédées et mutables en interne

Préférer :

```csharp
private readonly List<T> _items = [];
private readonly IReadOnlyList<T> _itemsView;

public Foo()
{
    _itemsView = _items.AsReadOnly();
}

public IReadOnlyList<T> Items => _itemsView;
```

plutôt que :

```csharp
public IReadOnlyList<T> Items => _items.AsReadOnly();
```

ou :

```csharp
public IReadOnlyList<T> Items => _items.ToImmutableArray();
```

qui allouent à chaque accès.

## Collections immuables après construction

Préférer la matérialisation immutable dans le constructeur / `init` :

```csharp
_items = items.ToImmutableArray();
```

puis :

```csharp
public IReadOnlyList<T> Items => _items;
```

## Dictionnaires/sets

Préserver impérativement le comparer existant.

## Records / `with`

Si une propriété collection possède un setter `init`, normaliser aussi le chemin `with`; ne pas sécuriser uniquement le constructeur primaire.

---

# Critères d’acceptation

Le chantier est terminé lorsque :

- aucune collection mutable possédée par une classe n’est récupérable par cast via une propriété annoncée read-only ;
- aucune collection externe n’est conservée sans copie quand le type promet une valeur/snapshot immutable ;
- aucune protection read-only n’est recréée inutilement à chaque getter si elle peut être construite une fois ;
- les vues live restent live ;
- les snapshots restent des snapshots stables ;
- les snapshots d’état mutable sont rafraîchis sur mutation plutôt que reconstruits à chaque lecture lorsque cela évite des allocations sans complexité excessive ;
- comparers, ordre, unicité et nullabilité sont préservés ;
- les types volontairement mutables ne sont pas « immutabilisés » mécaniquement ;
- des tests prouvent à la fois l’encapsulation et l’absence d’allocations répétées inutiles sur les getters concernés.
