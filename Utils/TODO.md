# Utils — Audit qualité (2026-07-10)

Premier passage d'audit qualité sur la bibliothèque core `Utils/` (Collections, Randomization,
String, Arrays, Format, Mathematics, Objects, Range, Async, Dates, Net, Numerics, Resources,
Security, Transactions, Files — 136 fichiers). Suit la même méthodologie que les audits déjà menés
sur Utils.Geography, Utils.Reflection et Utils.Fonts : chaque proposition ci-dessous a été vérifiée
manuellement (lecture du code, traçage à la main d'un scénario concret, ou absence de test qui
l'aurait révélée) avant d'être retenue — plusieurs pistes soulevées lors du balayage initial se sont
révélées être des faux positifs après vérification (voir section finale).

**État (2026-07-11) :** les 13 propositions ci-dessous sont toutes corrigées, chacune avec un commit
dédié et un test de régression, en commençant par les bugs fonctionnels (items 1, 2, 3, 6, 7, 8) puis
les incohérences d'écriture par ordre de priorité (items 4, 9, 11, 12, 10, 5, 13). Branche
`claude/utils-todo-fixes`.

## Bugs fonctionnels

### 1. `ForwardFusion<T1,T2>.Enumerate` — clés dupliquées à gauche non gérées (jointure many-to-many incorrecte)
`Utils/Collections/ForwardFusion.cs:105-110`. Dans la branche `case 0` (clés égales), seul
`rightEnum` avance (`hasRight = rightEnum.MoveNext()`) ; `leftEnum` reste sur place. Ce choix gère
correctement le cas 1-vers-N (une clé à gauche, plusieurs valeurs consécutives à droite avec la même
clé — c'est d'ailleurs le seul cas couvert par `ForwardFusionTests.cs`, où `list2` a des doublons
mais jamais `list1`). Mais dès que la clé de **gauche** est aussi dupliquée (many-to-many), le
résultat est incorrect : pour `list1 = [1, 1]` et `list2 = [1]`, un inner join SQL correct produirait
deux paires `(1,1)` (une par ligne de gauche), mais `ForwardFusion` n'en produit qu'une seule — la
deuxième occurrence de `1` à gauche se retrouve comparée à ce qui suit `1` à droite (right déjà
épuisé), et n'est donc jamais réappariée avec la valeur `1` déjà consommée.
**Fix proposé** : soit documenter explicitement que `leftList` doit avoir des clés uniques (limite
assumée d'un "merge join" simplifié), soit implémenter le vrai algorithme de jointure par fusion qui
bufferise le run de doublons du côté qui en a besoin pour produire le produit cartésien correct.
**Sévérité** : bug fonctionnel (résultats de jointure incomplets/faux pour toute clé dupliquée des
deux côtés — silencieux, aucune exception).
**Corrigé.** `Enumerate` bufferise désormais les runs de clés dupliquées des deux côtés et émet leur
produit cartésien complet ; la comparaison à `-1`/`1` littéraux a aussi été remplacée par un test de
signe. Tests : `ForwardFusionTests.cs` (5 nouveaux cas).

### 2. `DistributedRandom.NextDouble(double min, double max)` — `Floor()` transforme la sortie en entier
`Utils/Randomization/DistributedRandom.cs:66-72`. La méthode est documentée "Generates a random
**double** number within a specified range" et retourne un `double`, mais l'implémentation fait
`double.Floor(distributedValue * (max - min)) + min`. Le `Floor()` tronque systématiquement la partie
fractionnaire : `NextDouble(0.0, 1.0)` renverra donc presque toujours `0.0` (jamais une valeur comme
`0.37`), et `NextDouble(0.0, 100.0)` ne renverra jamais que des valeurs entières (`0.0`, `1.0`, ...,
`99.0`). Le `Floor()` n'a de sens que pour l'usage interne de `NextInt` (ligne 81 : `(int)
NextDouble(min, max)`), pas pour l'API publique `NextDouble(min, max)` elle-même, qui prétend générer
un double continu dans la plage. Aucun test ne couvre `NextDouble(min, max)` (seul le ctor et les cas
triviaux sont documentés dans le xml compilé, aucun fichier de test trouvé).
**Fix proposé** : retirer le `Floor()` de `NextDouble(min, max)` (retourner `distributedValue * (max
- min) + min` tel quel) ; `NextInt` peut appliquer son propre floor/cast sur le résultat continu.
**Sévérité** : bug fonctionnel (API publique inutilisable pour son usage documenté ; non couvert par
un test).
**Corrigé.** `Floor()` retiré de `NextDouble(min, max)` ; `NextInt` tronque toujours via son propre
cast. Tests : `DistributedRandomTests.cs` (nouveau fichier).

### 3. `StringUtils.ParseCommandLine` — dernier argument dé-échappé différemment des autres
`Utils/String/StringUtils.cs:113` vs `139`. Pour un argument entre guillemets suivi d'un espace, le
code appelle `.TrimBrackets('\"')` (ligne 113), qui retire uniquement la paire de guillemets
englobante sans traiter l'échappement `""` → `"` à l'intérieur. Pour le **dernier** argument de la
ligne (pas suivi d'espace), le code appelle `TrimQuotes(...)` (ligne 139), qui retire les guillemets
englobants **et** convertit les guillemets doublés internes `""` en `"` (`.Replace("\"\"", "\"")`).
Conséquence vérifiée : `ParseCommandLine("\"a\"\"b\" \"c\"\"d\"")` donne `["a\"\"b", "c\"d"]` — le
premier argument garde son guillemet interne doublé (`a""b`), le second (dernier de la ligne) le
convertit correctement en un seul guillemet (`c"d`), alors que les deux devraient suivre la même
convention d'échappement.
**Fix proposé** : appliquer `TrimQuotes` (ou une fonction équivalente gérant l'échappement `""`) de
façon uniforme à tous les arguments, y compris ceux suivis d'un espace.
**Sévérité** : bug fonctionnel (incohérence de désérialisation selon la position de l'argument dans
la ligne — pas de test existant pour `StringUtils` du tout).
**Corrigé.** `TrimQuotes` appliqué uniformément à tous les arguments. Tests :
`UtilsTest/String/StringUtilsTests.cs` (nouveau fichier).

### 6. `Number` — `default(Number)` represents an invalid `0/0` fraction
`Utils/Numerics/Number.cs`. `Number` is a `readonly struct` whose constructor rejects a zero
denominator and normalizes every valid fraction. However, the default value of a struct bypasses the
constructor, so `default(Number)` contains `_numerator = 0` and `_denominator = 0`.

This state is reachable in normal code through zero-initialized fields, arrays, automatic properties,
and explicitly through `TryParse`, which assigns `default` to the result when parsing fails.
Consequences include division by zero in `ToDecimal`, `NaN` through `ToDouble`, invalid arithmetic
propagation, and a possible textual representation of `0/0`.

**Proposed fix**: make the zero-initialized representation equivalent to zero by introducing an
effective denominator of `1` whenever `_denominator` is zero, and use that normalized denominator in
all operations. Alternatively, convert the type to a class, but that would be a much larger breaking
change. Add a regression test asserting that `default(Number)` behaves exactly like `Number.Zero`.

**Severity**: critical functional invariant violation. A public value type should have a valid and
well-defined default value.
**Fixed.** Introduced an `EffectiveDenominator` used throughout instead of the raw field, so
`default(Number)` is fully equivalent to `Number.Zero` (equality, hash code, arithmetic, formatting).
Tests: `UtilsTest/Math/NumberTests.cs`.

### 7. `Number.Parse` silently accepts malformed decimal inputs
`Utils/Numerics/Number.cs:101-123`. Decimal parsing uses
`text.Split(info.NumberDecimalSeparator)` and then reads only `parts[0]` and `parts[1]`. It never
verifies that exactly two parts were produced. As a result, an input such as `"1.2.3"` is silently
parsed as `1.2`, with the trailing `.3` ignored.

Conversely, common decimal forms such as `.5`, `-.5`, or possibly `5.` are rejected because the empty
or sign-only integer/fractional component is passed directly to `BigInteger.Parse`.

**Proposed fix**: locate the decimal separator explicitly, reject a second occurrence, validate that
the entire input is consumed, and define/document whether leading- or trailing-separator forms are
supported. Add tests for multiple separators, `.5`, `-.5`, `5.`, culture-specific separators, and
thousands separators.

**Severity**: high. Malformed input can be accepted with a value different from the supplied text.
**Fixed.** The separator is now located explicitly, a second occurrence throws `FormatException`, and
a missing integer/fractional part is treated as zero (supporting `.5`, `-.5`, `5.`). Tests:
`UtilsTest/Math/NumberTests.cs`.

### 8. `ObjectUtils.ComputeHash(Array)` throws on `null` elements
`Utils/Objects/ObjectUtils.cs:88-113`. The non-generic multidimensional-array overload computes each
item hash through `array.GetValue(indices).GetHashCode()`. `Array.GetValue` may return `null`, which
causes a `NullReferenceException`.

This is inconsistent with the `IEnumerable<object>` overload, which explicitly maps `null` to hash
code `0` using `value?.GetHashCode() ?? 0`.

**Proposed fix**: store the retrieved value in an `object?` local and combine
`value?.GetHashCode() ?? 0`. Clarify the nullability contract of the generic overload that accepts a
custom hash delegate.

**Severity**: medium functional bug for reference-type arrays containing null values.
**Fixed.** Applies the same `value?.GetHashCode() ?? 0` pattern already used by the
`IEnumerable<object>` overload. Tests: `UtilsTest/Objects/ObjectUtilsTests.cs` (new file).

## Incohérences d'écriture

### 4. `LRUCache<K,V>` — synchronisation partielle et trompeuse
`Utils/Collections/LRUCache.cs`. L'indexeur (`this[K]` get/set, lignes 61/67) et `Add(K,V)` (ligne
100) portent `[MethodImpl(MethodImplOptions.Synchronized)]`, ce qui suggère une intention de
thread-safety. Mais `Remove(K)` (ligne 119), `TryGetValue` (ligne 136), `Add(KeyValuePair<K,V>)`
(ligne 154), `Clear()` (ligne 162), et `CopyTo` (ligne 183) — qui mutent ou lisent les mêmes champs
internes `cacheMap`/`lruList` — n'ont **aucune** synchronisation. Un consommateur de l'interface
`IDictionary<K,V>` appelant naturellement `TryGetValue` ou `Remove` (chemins très courants) n'obtient
donc aucune protection, alors que la présence de `[Synchronized]` sur d'autres membres laisse croire
au contraire. Aucun test de stress multi-thread n'existe pour cette classe.
**Fix proposé** : soit synchroniser tous les membres mutants/lisants de façon cohérente (un seul
verrou explicite, plus lisible que l'attribut `Synchronized` implicite sur `this`), soit documenter
clairement que la classe n'est **pas** thread-safe et retirer les attributs `Synchronized` trompeurs
sur l'indexeur/`Add`.
**Sévérité** : incohérence de conception (thread-safety partielle et trompeuse, pas un bug isolé mais
un risque de course si un appelant se fie à la présence de `[Synchronized]` sur certains membres).
**Corrigé (en deux temps).** D'abord documenté comme non thread-safe et les attributs
`[Synchronized]` trompeurs retirés (dont `Clear()`, qui verrouillait en réalité un moniteur différent
de celui de l'indexeur/`Add`, donc sans exclusion mutuelle réelle entre eux). Puis, à la demande de
l'utilisateur (c'est le contexte d'usage le plus probable), rendu réellement thread-safe : un verrou
interne unique (`syncRoot`) protège désormais tous les membres, y compris `Keys`/`Values` (vues
« vivantes » mais dont chaque appel prend un instantané sous verrou) et `GetEnumerator` (idem, plutôt
qu'un curseur vivant sur la `LinkedList` interne, qui aurait levé `InvalidOperationException` en cas
de mutation concurrente). Tests : `LRUCacheTests.cs` (`ConcurrentAddsWithDistinctKeys_...`,
`ConcurrentReadWriteEnumerateStress_...`).

### 5. `Ranges.Specifics.cs` — tableau non-bracket
`Utils/Range/Ranges.Specifics.cs:269` : `new string[] { "-", ".." }` devrait utiliser la syntaxe
crochets (`["-", ".."]`), conformément à la règle du projet (AGENTS.md : "Arrays must use bracket
syntax").
**Fix proposé** : remplacer par `["-", ".."]`.
**Sévérité** : cosmétique.
**Corrigé.**

### 9. `ObjectUtils.DoAsync` is asynchronous in name only
`Utils/Objects/ObjectUtils.cs:64-80`. Both `DoAsync` overloads accept synchronous delegates and wrap
them in `Task.Run`. They therefore consume a thread-pool thread without composing real asynchronous
operations, provide no cancellation support, and may create unnecessary contention in server-side
or highly concurrent applications.

**Proposed fix**: replace or supplement them with overloads accepting `Func<T, Task<TResult>>` and
`Func<Task<TResult>>`, or `ValueTask<TResult>` equivalents, and return the selected task directly.
If the current thread-pool semantics are intentionally retained, rename the methods to make that
behavior explicit.

**Severity**: medium design and scalability issue rather than an isolated correctness bug.
**Fixed.** Documented the thread-pool offloading on the existing overloads and added
`Func<T, Task<Result>>`/`Func<Task<Result>>` overloads that compose the returned tasks directly
without `Task.Run`. Tests: `UtilsTest/Objects/ObjectUtilsTests.cs`.

### 10. `IntRange<T>.SimpleRange.CompareTo(object)` violates the `IComparable` contract
`Utils/Range/IntRange.cs:101-106`. The non-generic `CompareTo(object?)` implementation throws
`NotImplementedException` for both `null` and incompatible object types.

The conventional .NET contract is to return a positive value for `null` and throw
`ArgumentException` for an incompatible type.

**Proposed fix**: return `1` for `null`, delegate to the strongly typed comparison for
`SimpleRange`, and throw an `ArgumentException` naming the expected type for all other values.

**Severity**: low contract inconsistency. The nested type is private, but the behavior can still be
observed through non-generic sorting and collection APIs.
**Fixed.** Now returns `1` for `null` and throws `ArgumentException` for an incompatible type. Tests:
`IntRangeTests.cs` (reflection-based, since `SimpleRange` is private).

### 11. `RandomExtensions.RandomFloat` and `RandomDouble` expose ambiguous semantics
`Utils/Randomization/RandomExtensions.cs:241-258`. These methods fill the raw bytes of a `float` or
`double` and reinterpret them through `BitConverter`. They can therefore return `NaN`, positive or
negative infinity, subnormal values, and arbitrary magnitudes.

That may be useful for binary fuzzing, but the names suggest semantics similar to
`Random.NextSingle()` and `Random.NextDouble()`, which produce finite values in `[0, 1)`.

**Proposed fix**: use `NextSingle()` and `NextDouble()` for the existing method names, and expose the
raw-bit behavior under explicit names such as `RandomFloatBits` and `RandomDoubleBits`. If the current
behavior is intentional and must remain for compatibility, document it prominently and add tests for
special IEEE-754 values.

**Severity**: medium API-design inconsistency; it becomes a functional bug when callers assume a
finite normalized value.
**Resolved by documentation, not renaming.** `RandomFloat`/`RandomDouble` follow the same
full-bit-pattern convention as `RandomByte`/`RandomShort`/`RandomInt`/`RandomLong` in the same file,
and an existing serialization round-trip test in `UtilsTest.Functional` already depends on the
raw-bit behavior to exercise edge-case values. Renaming would have broken both. Documented the
behavior prominently (NaN/Infinity/subnormal/out-of-range possible) and pointed to
`Random.NextSingle()`/`NextDouble()` for finite `[0, 1)` semantics. Tests:
`RandomExtensionsTests.cs`.

### 12. `Ranges<T>.InnerParse` accepts valid fragments inside otherwise invalid input
`Utils/Range/Ranges.cs:114-129`. The parser uses `Regex.Matches` with an unanchored pattern and yields
every matching range while ignoring unmatched text before, between, or after the matches. A string
such as `"invalid [1..5] trailing garbage"` may therefore be partially accepted.

**Proposed fix**: distinguish strict parsing from extraction. A `Parse` API should require that the
entire input be consumed, while a separately named API such as `ExtractRanges` may intentionally
search for valid ranges inside larger text.

**Severity**: medium parsing-contract ambiguity and possible silent data acceptance.
**Fixed.** `InnerParse` now requires every character of the input to belong to a matched range or be
whitespace, throwing `FormatException` otherwise (leading, trailing, in-between garbage, and
zero-match input are all rejected); the existing concatenated-range syntax keeps working unchanged.
Tests: `UtilsTest/Objects/RangesTests.cs`.

### 13. `IntRange.cs` contains an unrelated `System.Formats.Tar` import
`Utils/Range/IntRange.cs:11` contains `using System.Formats.Tar; // If needed for IAdditionOperators,
etc.`. The namespace is unrelated to generic-math operator interfaces and appears to be a leftover
from generated or unfinished code.

**Proposed fix**: remove the unused import and its misleading comment.

**Severity**: cosmetic, but indicative of incomplete cleanup.
**Fixed.**

## Missing regression coverage

The following scenarios should be covered before or alongside the corresponding fixes:

1. `default(Number)` is equivalent to `Number.Zero`.
2. `Number.Parse` rejects multiple decimal separators and handles explicitly supported edge forms.
3. `ObjectUtils.ComputeHash(Array)` supports multidimensional arrays containing `null`.
4. `RandomFloat` and `RandomDouble` have an explicit, tested finite-value or raw-bit contract.
5. `IComparable.CompareTo(object?)` follows the standard null and incompatible-type behavior.
6. Strict range parsing rejects unmatched input instead of silently extracting valid fragments.
7. `LRUCache<K,V>` is either stress-tested for thread safety or explicitly tested/documented as not
   thread-safe.

## Suggested priority

| Priority | Finding |
|---|---|
| P0 | `default(Number)` produces an invalid `0/0` value |
| P1 | `Number.Parse` silently accepts malformed decimal text |
| P1 | `ForwardFusion` produces incomplete many-to-many joins |
| P1 | `DistributedRandom.NextDouble` truncates all fractional values |
| P1 | `ParseCommandLine` unescapes arguments inconsistently |
| P2 | `ComputeHash(Array)` throws for null elements |
| P2 | `LRUCache` synchronization is partial and misleading |
| P2 | `DoAsync` wraps synchronous work in `Task.Run` |
| P2 | `RandomFloat` / `RandomDouble` semantics are ambiguous |
| P2 | `Ranges<T>.InnerParse` accepts partial invalid input |
| P3 | `CompareTo(object)` violates the standard .NET contract |
| P4 | Bracket-array style violation and unrelated `using` directive |

## Points vérifiés sans anomalie trouvée (rejetés après vérification)
- `Utils/Collections/EnumerableEx.cs` (`Slice<T>`) — piste initialement suspectée (gestion du dernier
  segment après le dernier point de coupure) ; tracé à la main sur plusieurs exemples (2, 3 coupures),
  le comportement est correct. Confirmé indirectement par les tests `GlyphSimpleTests`/round-trip de
  Utils.Fonts qui exercent cette méthode intensivement sans échec.
- `Utils/Arrays/ArrayAccessor.cs` (`Position`) — piste initialement suspectée (dernière dimension non
  multipliée) ; tracé à la main en 2D et 3D, le résultat est correct car la dernière dimension a par
  construction un stride de 1 en layout row-major — pas besoin de multiplication.
- `Utils/Mathematics/MathEx.cs` (`Min<T>(IComparer<T>, params T[])`) — l'ordre des paramètres
  (comparateur avant le tableau `params`) est imposé par la syntaxe C# (`params` doit être le dernier
  paramètre), pas un choix de style discutable.
- `Utils/Collections/EnumerableEx.Zip.cs`, `Utils/Arrays/ArrayUtils.cs`, `Utils/Objects/ObjectUtils.cs`
  — les usages de `new T[n]` signalés lors du balayage initial sont des allocations de tableaux de
  taille connue (pas des tableaux avec initialiseur) ; il n'existe pas d'équivalent en syntaxe
  crochets pour ce cas (`[n]` créerait un tableau à un élément de valeur `n`, pas un tableau de
  longueur `n`) — ce ne sont pas des violations de la règle.
