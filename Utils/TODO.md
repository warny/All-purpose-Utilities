# Utils — Audit qualité (2026-07-10)

Premier passage d'audit qualité sur la bibliothèque core `Utils/` (Collections, Randomization,
String, Arrays, Format, Mathematics, Objects, Range, Async, Dates, Net, Numerics, Resources,
Security, Transactions, Files — 136 fichiers). Suit la même méthodologie que les audits déjà menés
sur Utils.Geography, Utils.Reflection et Utils.Fonts : chaque proposition ci-dessous a été vérifiée
manuellement (lecture du code, traçage à la main d'un scénario concret, ou absence de test qui
l'aurait révélée) avant d'être retenue — plusieurs pistes soulevées lors du balayage initial se sont
révélées être des faux positifs après vérification (voir section finale). Aucune proposition
ci-dessous n'est encore corrigée.

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

### 5. `Ranges.Specifics.cs` — tableau non-bracket
`Utils/Range/Ranges.Specifics.cs:269` : `new string[] { "-", ".." }` devrait utiliser la syntaxe
crochets (`["-", ".."]`), conformément à la règle du projet (AGENTS.md : "Arrays must use bracket
syntax").
**Fix proposé** : remplacer par `["-", ".."]`.
**Sévérité** : cosmétique.

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
