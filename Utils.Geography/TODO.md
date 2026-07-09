# Utils.Geography — Audit qualité (2026-07-09)

Audit complet du package : conformité AGENTS.md, bugs, performance, couverture de tests.
Branche de travail : voir `git log` sur `Utils.Geography/` et `UtilsTest/Geography/` autour de cette date.

## Bugs corrigés

### Incohérence hash/égalité (`GeoPoint<T>`, `GeoVector<T>`)
`Equals` utilise un `FloatingPointComparer<T>` tolérant (5 décimales), mais `GetHashCode` hachait les
valeurs brutes non arrondies. Deux points considérés égaux par `Equals` pouvaient donc produire des
hash codes différents, un bug classique qui casse `Dictionary<GeoPoint<T>,...>`/`HashSet<GeoPoint<T>>`.
**Fix** : `GetHashCode` arrondit désormais à 5 décimales (`T.Round(x, 5)`), comme le fait déjà
`GeoVector.Travel`. Note : la non-transitivité documentée sur `FloatingPointComparer<T>`
(voir sa XML doc) veut dire qu'aucun hachage par arrondi n'est parfait à 100% pour un comparateur à
tolérance ; c'est une amélioration significative, pas une garantie absolue.

### `BoundingBox` incorrecte pour coordonnées entièrement négatives (`GeoPointList<T>`, `GeoPointList2<T>`)
Le calcul initialisait `minLatitude/minLongitude` à `10000` (magique) et `maxLatitude/maxLongitude`
à `0`. Si tous les points de la liste ont une latitude/longitude négative, le max restait à `0` — un
résultat faux. **Fix** : le seed utilise désormais le premier point (ou sa bounding box, pour
`GeoPointList2`) au lieu de constantes magiques. Tests de régression ajoutés (coordonnées 100%
négatives).

### Projection équirectangulaire : inverse incorrect
`MapPointToGeoPoint` divisait par `degree.RightAngle`/`degree.StraightAngle`, ce qui n'est pas
l'inverse de `GeoPointToMapPoint` (qui ne fait *aucune* division — X=longitude, Y=latitude en degrés
bruts). Le round-trip `GeoPoint → ProjectedPoint → GeoPoint` produisait un résultat complètement faux.
**Fix** : suppression des divisions superflues. Couvert par `ProjectionsRoundTripTests`.

### Projection de Mollweide : dérivée de Newton incohérente (degrés vs radians)
L'équation `2θ + sin(2θ) = 180·sin(lat)` était résolue avec des quantités en pseudo-« degrés », mais
`sin(2θ)` reste sans dimension : le mélange rendait l'équation incohérente et la dérivée de Newton
`f'(θ) = 2 + 2cos(2θ)` était fausse d'un facteur `180/π`. Détecté par le test de round-trip
(`(45°,45°)` revenait à `48.6°` au lieu de `45°`). **Fix** : réécriture entière du solveur en radians
(formule standard de Mollweide), avec conversion degrés↔radians uniquement aux limites de la méthode.
Un deuxième problème est apparu ensuite : l'estimation initiale `θ₀ = target/2` divergeait pour les
latitudes élevées (ex. 60°) faute d'un nombre d'itérations suffisant. **Fix** : `θ₀ = latitude` (en
radians), qui converge en 3-4 itérations sur toute la plage testée.

### Modulo négatif dans `MapPoint<T>.TileX`/`TileY`
`X % TileSize` peut être négatif en C# quand `X` est négatif (arithmétique de troncature), ce qui
sortait la valeur de la plage `[0, TileSize)` attendue pour une coordonnée « dans la tuile ». Cas
fréquent car la plupart des projections produisent des coordonnées négatives (hémisphères
ouest/sud). **Fix** : modulo flooré `((X % TileSize) + TileSize) % TileSize`. La propriété `Tile`
(index de tuile) utilisait aussi une division tronquée au lieu d'une division flooree ; corrigée en
cohérence avec le nouveau `TileX`/`TileY`.

### Double parsing dans `GeoVector<T>(string, CultureInfo[])`
Le constructeur appelait `ParseVectorString` deux fois dans le même appel `base(...)` (une fois pour
`latitude`, une fois pour `longitude`+`bearing`) — la chaîne entière (avec sa boucle sur les cultures
et sa regex) était parsée deux fois pour rien. **Fix** : constructeur privé prenant un tuple
`(latitude, longitude, bearing)` pré-calculé par un seul appel.

### Avertissements de nullabilité (contrats cassés, détectés par le compilateur)
- `GeoVector<T>.Recenter` retournait `null` sur un type de retour non-nullable quand `other is null`.
  **Fix** : lève désormais `ArgumentNullException` via `.Arg().MustNotBeNull()` (cohérent avec le
  reste du package, ex. `MapPosition`).
- `GeoPoint<T>.ToString`/`GeoVector<T>.ToString` : `IFormatProvider.GetFormat(typeof(TextInfo))` peut
  renvoyer `null` ; le cast direct `(TextInfo)` aurait levé une `InvalidCastException` sur `null`.
  **Fix** : `as TextInfo` + garde déjà présente sur `textInfo?.ListSeparator`.
- `Planet<T>(T, string name = null)` : paramètre `string?` manquant.
- `Projections<T>` (cache) : `_ => null` dans un switch dont le type de retour n'est pas nullable ;
  annoté `null!` avec commentaire (jamais déréférencé, seul `return value != null` compte).

### Performance mineure : conversions `CreateChecked` redondantes (`Planet<T>.Area`)
`T.CreateChecked(EquatorialRadius)` et `T.CreateChecked(a.Longitude)` etc. convertissaient une valeur
déjà de type `T` vers `T` — no-op coûteux dans une boucle par arête de polygone. Supprimé.

## Documentation mise à jour
- `LambertAzimuthalEqualArea<T>` : la XML doc ne précisait pas que cette implémentation est l'aspect
  **polaire** (centré sur le pôle nord, `(lat=90°,lon=0°) → (0,0)`), pas l'aspect équatorial. Un test
  naïf supposant que `(0,0) → (0,0)` pour toutes les projections a révélé la confusion. Documenté.

## Couverture de tests ajoutée
Avant cet audit, ces classes n'avaient **aucun** test : `BoundingBox<T>`, `GeoPointList<T>`,
`GeoPointList2<T>`, `MapPosition<T>`, `ProjectedPoint<T>`, `Tile<T>`, `MapPoint<T>`,
`CoordinatesUtil<T>`, `RepresentationConverter<T>`, et les 7 implémentations de
`IProjectionTransformation<T>` (Mercator, Equirectangular, Gall-Peters, Miller, Mollweide, Lambert,
Stereographic) ainsi que le cache `Projections<T>`.

Fichiers ajoutés dans `UtilsTest/Geography/` :
- `BoundingBoxTests.cs`, `GeoPointListTests.cs`, `MapPositionTests.cs`, `ProjectedPointTests.cs`,
  `MapPointTileTests.cs`, `CoordinatesUtilTests.cs`, `RepresentationConverterTests.cs`,
  `ProjectionsRoundTripTests.cs` (round-trip géographique pour les 7 projections + vérification du
  centre de projection).
- Extension de `GeoPointTests.cs`/`GeoVectorTests.cs` : cohérence hash/égalité, non-double-parsing du
  constructeur string, comportement de `Recenter`.

Tous les nouveaux tests vont dans **UtilsTest.Unit** (aucune dépendance externe).

## Items connus, non corrigés (nécessitent une décision de conception)

### 1. `MapPoint<T>`/`Tile<T>`/`RepresentationConverter<T>` : mise à l'échelle incohérente — priorité haute si ce sous-système doit être utilisé
Trois façons différentes de passer d'un `ProjectedPoint<T>` à un index de tuile coexistent et ne sont
pas cohérentes entre elles :
- `MapPoint(ProjectedPoint<T>, zoomLevel, tileSize)` multiplie `projectedPoint.X` par
  `1 << zoomLevel` (pas de facteur `tileSize`).
- `RepresentationConverter.GetMapSize(zoomLevel)` retourne `tileSize << zoomLevel` (donc suppose que
  la taille totale de la carte en pixels inclut `tileSize`).
- `RepresentationConverter.MappointToTile` utilise `projectedPoint.X` **brut** (aucune mise à
  l'échelle par zoom) divisé par `tileSize`.

Par ailleurs, **aucune** des 7 projections fournies ne produit de coordonnées normalisées `[0,1]`
(Equirectangular sort des degrés, Mercator un logarithme, etc.), ce qui est le préalable habituel
pour un système de tuiles à la Web-Mercator/Slippy-map. Sans étape de normalisation explicite entre
`IProjectionTransformation<T>` et `MapPoint<T>`/`Tile<T>`, ce sous-système d'affichage n'est
probablement pas utilisable tel quel.
Aucun test, aucun appelant dans le reste du dépôt ne définit le comportement attendu — impossible de
« corriger » sans deviner une nouvelle sémantique. **Recommandation** : décider explicitement du
contrat (ex. ajouter une méthode de normalisation sur `IProjectionTransformation<T>`, ou documenter
que `MapPoint`/`Tile`/`RepresentationConverter` attendent un `ProjectedPoint` déjà normalisé en
`[0,1]`), puis aligner les trois formules.

### 2. Avertissement analyseur CA2260 sur `GeoVector<T>`
`GeoVector<T> : GeoPoint<T>, IEqualityOperators<GeoVector<T>, GeoVector<T>, bool>` — l'analyseur
signale que `GeoPoint<T>` (qui implémente lui-même `IEqualityOperators<GeoPoint<T>, GeoPoint<T>,
bool>`) attend que `T` soit rempli par le type dérivé (CRTP), ce qui n'est pas le cas ici puisque
`GeoVector<T>` hérite classiquement de `GeoPoint<T>` plutôt que de `GeoPoint<GeoVector<T>>`. Corriger
proprement demanderait de transformer `GeoPoint<T>` en `GeoPoint<TSelf, T>` (CRTP) — un changement
d'API cassant pour tous les consommateurs. Laissé tel quel ; warning informatif, sans impact
fonctionnel connu.

