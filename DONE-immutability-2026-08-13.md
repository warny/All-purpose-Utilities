# DONE — Immutabilité et encapsulation des collections

## État au 2026-08-13

L’audit transversal des collections publiées est terminé. Le principe appliqué est la copie défensive unique lors de la prise de possession, suivie d’un stockage réellement immutable et réutilisé sans copie dans les getters. L’ordre, l’unicité et les comparateurs significatifs sont conservés.

## Catégories auditées

La passe finale a couvert les tableaux et tables statiques publics, les propriétés `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `IReadOnlyDictionary<TKey, TValue>` et `IReadOnlySet<T>`, les propriétés `init`, les records et structures readonly, les matérialisations `ToArray()` / `ToList()`, ainsi que les assignations directes de tableaux, listes, dictionnaires, sets et énumérables. Les usages et tests ont été relus pour distinguer objet immutable, snapshot, vue read-only live, modèle mutable et résultat neuf appartenant à l’appelant.

## Corrections réalisées

- `FontSupport` conserve toutes ses tables de noms et tables numériques sous forme d’instances immutables uniques. `StdNameIndexMap` est désormais construit depuis la même table possédée, ce qui garantit la cohérence de `GetName` et `GetStrIndex`.
- `Types` expose ses onze classifications numériques sous forme de listes réellement immutables uniques. Les usages internes ont été adaptés à l’interface de collection sans copie supplémentaire.
- `CMapFormat4.TableMap` copie le tableau source une seule fois et construit son reverse mapping immutable depuis ce snapshot.
- `DateFormulaLanguage.Days` normalise la valeur fournie au `init` vers un dictionnaire immutable, en conservant les comparateurs d’égalité des dictionnaires standards, y compris leurs variantes triées mutables et immutables.
- `SerializationContractException.Diagnostics` matérialise une seule fois les énumérables, y compris one-shot, puis utilise ce même snapshot immutable pour le message et la propriété.
- `ODataQueryCompilation.Filters` et `Expansions` sont des snapshots immutables stables et `ToUriString()` conserve sa sortie.
- `AcntTable.AccentDescription.Multiple.Extensions` conserve un snapshot immutable ordonné.
- `ParserOptions.NumberSuffixes` transmet explicitement `StringComparer.CurrentCultureIgnoreCase` lors de la conversion immutable.
- La passe complémentaire a identifié et corrigé `ConstantNumericAttribute.Values`, qui conservait et publiait le tableau `params` de l’appelant.

Des tests ciblés prouvent le non-aliasing, l’absence de tableaux cachés derrière les interfaces read-only, la stabilité des lookups et sérialisations concernées, l’énumération unique et la conservation des comparateurs.

## Compatibilité API

Pour supprimer les tables globales mutables sans allocation à chaque getter, les propriétés tableau de `FontSupport` et `Types` utilisent désormais `IReadOnlyList<T>`. `ConstantNumericAttribute.Values` utilise également `IReadOnlyList<double>?`. Ces changements de signature sont des ruptures de compilation assumées dans la version majeure 2.0 en cours ; l’ordre, le contenu et les usages d’énumération/indexation restent inchangés. Les autres corrections conservent les signatures publiques existantes.

## Faux positifs et exceptions de design conservés

- `Bytes.AsBytes(byte[])` conserve volontairement l’aliasing documenté ; `ToBytes` reste l’alternative de copie.
- `ProtocolResponseException.Responses`, `NetworkParameters.NetworkInterfaces`, `NetworkParameters.DnsServers`, `CmapTable.CMaps` et `ArraysChange<T>.Value` sont déjà protégés selon leur contrat respectif.
- `ExternalResource.DiagnosticMessages` est une vue read-only live persistante sur une collection interne évolutive.
- `ReadOnlyRange<T>` et les autres vues live explicitement documentées restent live.
- `GeoPointList<T>`, `GeoPointList2<T>`, `ExpressionCompilerContext.Symbols`, `VirtualProcessor<T>.Breakpoints` et `QueryString.QueryValues` sont des APIs volontairement mutables.
- `CMapFormat0.MapBytes` et les tables TTF à propriétés `get; set;` sont des modèles mutables de lecture, édition et sérialisation.
- Les modèles DNS à propriétés `byte[]` et les autres DTOs explicitement mutables ne sont pas des snapshots.
- Les builders, parseurs/readers internes et collections jamais publiées conservent leur stockage mutable.
- Les méthodes retournant un tableau neuf appartenant uniquement à l’appelant ne divulguent aucun état interne.
- `Brackets.All` conserve son contrat explicite de snapshot neuf à chaque accès.
- L’audit distinct de `Utils.Parser` reste clos ; ses modèles de contexte et options suivent leurs contrats documentés propres.

## Conclusion

Après correction et relecture complète du diff, une dernière recherche exhaustive n’a trouvé aucun défaut restant dans le périmètre : aucun aliasing publié non volontaire, tableau mutable caché derrière `IReadOnly*`, perte de comparer, double énumération, copie répétée au getter ou changement accidentel de sémantique live/snapshot.
