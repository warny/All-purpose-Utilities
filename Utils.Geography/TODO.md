# Utils.Geography — Audit qualité (2026-07-09)

Audit complet du package : conformité AGENTS.md, bugs, performance, couverture de tests.
Branche de travail : voir `git log` sur `Utils.Geography/` et `UtilsTest/Geography/` autour de cette date.

## Open findings from the follow-up audit (2026-07-10)

The following issues were identified during a second independent pass. They are not fixed yet.

### 1. `GeoPoint<T>` — `Equals`/`GetHashCode` contract is still broken at the poles

`GeoPoint<T>.Equals` deliberately treats every longitude at the same pole as the same geographic
point: once both rounded latitudes equal `+90°` or `-90°`, it returns `true` without comparing
longitude. `GetHashCode`, however, still includes normalized longitude unconditionally. Therefore
`new GeoPoint<double>(90, 10)` and `new GeoPoint<double>(90, -170)` compare equal but can produce
different hash codes, breaking `Dictionary<GeoPoint<T>, ...>` and `HashSet<GeoPoint<T>>`.

**Proposed fix**: when rounded latitude is either pole, hash latitude only. Otherwise hash rounded
latitude together with normalized/rounded longitude. Add regression tests for both poles that assert
both equality and identical hash codes for different longitudes.

**Severity**: high — direct violation of the .NET equality/hash contract.

### 2. `GeoPoint<T>.FormatPosition` — DMS seconds are multiplied by 3600 instead of 60

After extracting degrees, the code multiplies the fractional degree by 60 and extracts whole
minutes. At that point the remaining fraction is a fraction of a minute, but it is multiplied by
`SecondsInDegree` (`3600`) instead of `SecondsInMinute` (`60`). Coordinates containing non-zero
seconds are therefore formatted with values up to roughly 3599 seconds instead of `[0, 59]`.
Existing tests only use values whose seconds are exactly zero, so they do not expose the bug.

**Proposed fix**: replace the second multiplication by `SecondsInMinute`. Add tests such as
`45.508333333...° -> 45°30'30"` and equivalent negative coordinates.

**Severity**: high — public formatting returns incorrect geographic coordinates.

### 3. Coordinate parsing regex accepts valid substrings inside invalid input

`GeoPoint<T>.BuildRegexCoordinates` creates a regex without `^` and `$` anchors, and
`ParseCoordinate` uses `Regex.Match`. Inputs containing arbitrary text before or after a valid
coordinate can therefore be accepted by matching only the valid substring. `Parse`/constructors and
`TryParse` should validate the complete coordinate string, not extract a coordinate from free text.

**Proposed fix**: anchor the expression, allow surrounding whitespace explicitly, and add tests that
reject trailing or leading garbage while preserving intended whitespace support.

**Severity**: medium to high — malformed input may be silently accepted.

### 4. `CoordinatesUtil<T>.ParseCoordinatestring` silently removes empty coordinates

The method splits with `StringSplitOptions.RemoveEmptyEntries`. An invalid positional list such as
`"1,,2,3,4"` becomes four tokens and may be accepted when four coordinates are expected, shifting all
values after the empty position. Empty coordinates are semantically significant and must not be
removed.

**Proposed fix**: use `StringSplitOptions.None`, verify the exact token count, reject null/empty or
whitespace-only tokens, then parse. Add regression tests for leading, middle and trailing empty
values.

**Severity**: high — invalid data may be accepted with shifted coordinate positions.

### 5. `BoundingBox<T>.ToString()` is not parseable by `BoundingBox<T>.FromString()`

`FromString` expects four comma-separated numeric values in the order
`minLat,minLon,maxLat,maxLon`, while `ToString()` emits a descriptive representation containing
labels such as `minLatitude=`. A natural round-trip through `FromString(box.ToString())` therefore
fails. This may be intentional, but the API names imply a symmetry that does not exist.

**Proposed fix**: either make the default representation parseable, add an explicit parseable format
specifier, implement `IParsable<BoundingBox<T>>`, or rename the parsing API to make the accepted
format explicit.

**Severity**: medium — API contract and discoverability issue.

### 6. `BoundingBox<T>` cannot represent a box crossing the antimeridian

`ValidateBoundingBox` always orders longitudes with `T.Min`/`T.Max`. A box intended to run from
`170°E` to `170°W` should span 20 degrees across the antimeridian, but it is converted into a
340-degree box. `Contains`, `Intersects`, `LongitudeSpan`, and `GetCenterpoint` inherit that
interpretation.

**Proposed fix**: either document that antimeridian-crossing boxes are unsupported, or model this
case explicitly (for example with a `CrossesAntimeridian` invariant and circular longitude logic).
Add tests for containment, intersection, center and span around `±180°`.

**Severity**: significant functional limitation for global mapping use cases.

### 7. Trigonometric inverse inputs are not clamped against floating-point drift

`GeoPoint<T>.AngleWith` passes a computed spherical dot product directly to `Acos`. Although the
mathematical value is in `[-1, 1]`, floating-point rounding can produce a value just outside the
interval and return `NaN`, especially for identical or nearly identical points. Similar direct
`Acos`/`Asin` calls exist in `GeoVector<T>.Intersections`.

**Proposed fix**: clamp mathematically bounded inputs to `[-1, 1]` immediately before inverse
trigonometric calls. Add tests for identical points, nearly identical points, antipodal points, and
near-degenerate great-circle intersections.

**Severity**: medium — rare but plausible numerical failures.

### Follow-up priority

| Priority | Finding |
| --- | --- |
| P0 | Pole equality/hash inconsistency |
| P1 | Incorrect DMS seconds conversion |
| P1 | Empty coordinate tokens silently removed |
| P1 | Coordinate regex not anchored |
| P2 | Missing clamp before inverse trigonometric functions |
| P2 | Antimeridian-crossing bounding boxes unsupported |
| P3 | `BoundingBox.ToString`/`FromString` are not round-trip compatible |

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

### `Equals`/`GetHashCode` ne géraient pas le wraparound circulaire de `Longitude`/`Bearing` (PR #423, revue)
Suite à la revue de la PR #423 (Codex + retour du mainteneur), l'arrondi comparatif introduit pour
`GeoPoint<T>.Equals`/`GetHashCode` (voir plus haut) traitait `Longitude` (antiméridien ±180°) et
`Bearing` (0°/360°) comme des nombres linéaires plutôt que circulaires : deux longitudes comme
`179.999998°` et `-179.999998°` (quasiment le même point, ~4×10⁻⁶° d'écart réel) étaient considérées
différentes et hachaient différemment.
**Fix** : ajout de trois membres génériques sur `IAngleCalculator<T>` (implémentés dans
`Trigonometry<T>`, donc disponibles pour Degree/Radian/Grade) :
- `AreEqual(angle1, angle2, tolerance)` — comparaison à tolérance, distance angulaire la plus courte.
- `AreEqualRounded(angle1, angle2, decimals)` — arrondit après normalisation dans `[0, Perigon)`.
- `NormalizeRounded(angle, decimals)` — la valeur normalisée-arrondie sous-jacente, exposée pour le hachage.

`GeoPoint<T>.Equals`/`GetHashCode` et `GeoVector<T>.Equals`/`GetHashCode` utilisent désormais
`degree.AreEqualRounded`/`degree.NormalizeRounded` pour `Longitude`/`Bearing`. `Latitude` n'est pas
concernée (elle ne boucle pas ; les pôles restent un cas spécial séparé).

### Pas de méthode de comparaison floue explicite après le retrait de la tolérance d'`Equals`
En rendant `Equals` cohérent avec `GetHashCode` (arrondi plutôt que fenêtre de tolérance), l'ancien
comportement « égal à epsilon près » a disparu de `Equals`. **Fix** : ajout de
`GeoPoint<T>.IsApproximately(other, tolerance)` / `IsApproximately(other)` (tolérance par défaut =
`comparer.Interval`) et des équivalents sur `GeoVector<T>`, explicitement documentés comme *non*
utilisables pour le hachage/les clés de dictionnaire (fenêtre de tolérance non transitive).

### `MapPosition<T>` mutable alors qu'il surcharge `Equals`/`GetHashCode`
`GeoPoint`/`ZoomLevel` avaient des setters publics alors que `GetHashCode` en dépend : muter l'instance
après l'avoir stockée dans un `Dictionary`/`HashSet` aurait pu casser silencieusement les lookups, et la
validation du constructeur (`zoomLevel > 0`) restait contournable après coup. **Fix** : propriétés
passées en lecture seule (`{ get; }`), comme le reste des types « valeur » du namespace (`GeoPoint`,
`BoundingBox`, `Tile`, `ProjectedPoint`).

### `GeoPointList<T>`/`GeoPointList2<T>` héritaient directement de `List<T>`
Contraire au principe de séparation des responsabilités d'AGENTS.md (mélange stockage de collection et
logique de calcul géométrique). **Fix** : composition plutôt qu'héritage — les deux classes encapsulent
désormais un `List<T>` privé et implémentent `IList<T>`/`IReadOnlyList<T>` par délégation complète. Le
comportement observable est inchangé (syntaxe d'initialiseur de collection, indexeur, `Count`, etc.
vérifiés). **Changement d'API potentiellement cassant** pour du code externe qui ferait
`List<GeoPoint<T>> x = geoPointList;` (upcast implicite) ou utiliserait des membres spécifiques à `List<T>`
absents de `IList<T>` (`Sort`, `ForEach`, `AsReadOnly`, etc.) — aucun appelant de ce type trouvé dans le
dépôt ; à mentionner dans le changelog si une nouvelle version est publiée.

### `MapPoint<T>`/`Tile<T>`/`RepresentationConverter<T>` : mise à l'échelle incohérente
Trois façons différentes de passer d'un `ProjectedPoint<T>` à un index de tuile coexistaient sans être
cohérentes entre elles (`MapPoint` ignorait `tileSize` dans sa mise à l'échelle, `MappointToTile`
utilisait la coordonnée projetée **brute**, sans mise à l'échelle du tout). Démontré concrètement :
pour le même `ProjectedPoint` (Paris, Mercator, zoom 10), `MapPoint(...).Tile` donnait `(9, 3)` alors
que `RepresentationConverter.MappointToTile(...)` donnait `(0, 0)`.

**Fix** — ajout de `IProjectionTransformation<T>.Bounds` (bornes rectangulaires en unités natives de
la projection) et `Normalize(ProjectedPoint<T>)` (implémentation par défaut sur l'interface, en
C# 8+ : mise à l'échelle linéaire indépendante par axe vers `[0,1]`) :
- Chaque projection déclare ses propres bornes. La plupart sont finies et exactes aux pôles
  (Equirectangular, Gall-Peters, Miller, Mollweide, Lambert) ; **Mercator** est le cas particulier
  demandé : `Y` diverge à `lat=±90°` (singularité mathématique du logarithme de tangente), donc sa
  borne est dérivée d'une nouvelle propriété publique `MercatorProjection<T>.MaxLatitude`
  (`≈85.05112878°`, le seuil standard « Web Mercator » utilisé par OpenStreetMap/Google
  Maps/Bing/Leaflet, calculé comme `2·atan(e^π) − π/2`) plutôt que d'évaluer à `lat=90°`.
  **Stereographic** reste un cas limite documenté : sa singularité est au point antipodal
  (lat=0°,lon=±180°), pas aux pôles, donc ses bornes ne couvrent que l'étendue atteinte aux pôles
  (`[-1,1]`) — les points proches du méridien antipodal se normalisent légitimement hors `[0,1]`.
- `MapPoint(ProjectedPoint<T>, zoomLevel, tileSize)` et `RepresentationConverter.MappointToTile`
  utilisent désormais tous les deux `Normalize` puis la même formule de taille de carte
  (`tileSize << zoomLevel`, identique à `GetMapSize`) — les deux chemins sont maintenant garantis
  cohérents entre eux (testé : `MappointToTileAgreesWithMapPointForTheSameProjectedPoint`).

**Limite assumée, à noter si un usage « slippy-map » réel (OSM-like) est prévu** : `Normalize` ne fait
aucune hypothèse d'orientation cardinale (nécessaire car les projections azimutales comme Lambert/
Stereographic n'ont pas un axe Y qui suit seul la latitude). Conséquence pour les projections
cylindriques (Mercator, Equirectangular, ...) : la ligne de tuile augmente **vers le nord**, alors que
la convention standard OSM/Google/Bing veut que la ligne `0` soit au nord et augmente **vers le sud**.
Si un usage réel de rendu de tuiles façon slippy-map est prévu, il faudra inverser cet axe Y quelque
part (dans `MapPoint`/`RepresentationConverter`, ou par un flag) — non fait ici faute de demande
explicite en ce sens.

**Corrections suite à la revue Codex sur la PR #425** :
- **Compatibilité ascendante de `Bounds`** — ajouter `Bounds` comme membre requis de l'interface
  publique `IProjectionTransformation<T>` aurait cassé toute implémentation tierce existante
  (source et binaire). **Fix** : `Bounds` a désormais une implémentation par défaut sur l'interface
  (C# 8+) qui lève `NotSupportedException` avec un message explicite, plutôt que d'être abstraite.
  Les implémentations existantes qui ne la redéfinissent pas continuent de compiler et de fonctionner
  pour `GeoPointToMapPoint`/`MapPointToGeoPoint` sans aucun changement ; elles ne lèvent que si du code
  appelle réellement `Bounds`/`Normalize` dessus. La classe abstraite `ProjectionTransformation<T>` ne
  redéclare plus `Bounds` en `abstract` pour la même raison (hérite du même filet de sécurité). Testé :
  `CustomProjectionsThatDoNotOverrideBoundsStillCompileAndWorkForCoreConversions` et
  `...ThrowOnlyWhenNormalizeIsActuallyUsed`.
- **Bord exact `normalized=1` non clampé** — `Floor(1 × mapSize)` vaut `mapSize`, un pixel après le
  dernier valide (ex. latitude exactement `90°` en Equirectangular, ou exactement
  `MercatorProjection<T>.MaxLatitude`). `MapPoint` ne clampait pas ce cas alors que `MappointToTile`
  clampait déjà l'index de tuile — donc les deux chemins auraient pu à nouveau diverger sur cette
  frontière précise. **Fix** : les deux méthodes clampent désormais le pixel lui-même à
  `[0, mapSize - 1]` avant tout calcul de tuile. Testé : `PixelCoordinatesAreClampedExactlyAtTheBoundaryEdge`.
- **Overflow `int` de la taille de carte aux hauts niveaux de zoom** — `tileSize << zoomLevel` en `int`
  déborde dès le zoom 24 avec une tuile de 256px (`256 << 24` dépasse `int.MaxValue`), produisant une
  taille de carte négative ou nulle et des tuiles fausses. **Fix** :
  `RepresentationConverter.GetMapSize` retourne désormais `long` (changement de signature — l'ancien
  type `int` était trop étroit pour un usage réaliste) ; `MapPoint` calcule aussi sa taille de carte en
  `long` directement. Testé : `GetMapSizeDoesNotOverflowAtHighZoomLevels`.

### Avertissement analyseur CA2260 sur `GeoVector<T>` (résolu, 2026-07-10 : composition + struct)
`GeoVector<T> : GeoPoint<T>, IEqualityOperators<GeoVector<T>, GeoVector<T>, bool>` — l'analyseur
signalait que `GeoPoint<T>` (qui implémente lui-même `IEqualityOperators<GeoPoint<T>, GeoPoint<T>,
bool>`) attendait que `T` soit rempli par le type dérivé (CRTP), ce qui n'était pas le cas ici puisque
`GeoVector<T>` héritait classiquement de `GeoPoint<T>` plutôt que de `GeoPoint<GeoVector<T>>`.
**Résolu** : `GeoVector<T>` et `GeoPoint<T>` ont été transformés en `readonly struct` indépendants ;
`GeoVector<T>` compose désormais un `GeoPoint<T>` (propriété `Point`) plutôt que d'en hériter — une
struct ne peut de toute façon pas hériter, donc CA2260 ne se pose plus. Le pragma a été supprimé.
Cette refonte corrige aussi un bug réel que l'héritage masquait : `GeoPoint<T>.Equals(object?)`
matche `obj is GeoPoint<T>`, donc comparer un `GeoPoint<T>` brut à un `GeoVector<T>` aux mêmes
coordonnées les déclarait égaux en ignorant silencieusement le bearing, alors que leurs
`GetHashCode()` respectifs (lat/lon vs lat/lon/bearing) différaient — un contrat Equals/GetHashCode
cassé pour tout `Dictionary`/`HashSet` mélangeant les deux types. Composition + struct rend ce
mélange impossible : les deux types sont désormais sans relation. Voir aussi les tests mis à jour
dans `GeoPointTests.cs`/`GeoVectorTests.cs` (pattern `TryParse(out T result)` non-nullable idiomatique
des structs, suppression des tests `null` désormais impossibles à écrire pour des paramètres struct
non-nullables).

## Documentation mise à jour
- `LambertAzimuthalEqualArea<T>` : la XML doc ne précisait pas que cette implémentation est l'aspect
  **polaire** (centré sur le pôle nord, `(lat=90°,lon=0°) → (0,0)`), pas l'aspect équatorial. Un test
  naïf supposant que `(0,0) → (0,0)` pour toutes les projections a révélé la confusion. Documenté.
- `StereographicProjection<T>` : documentation de la singularité au point antipodal (lat=0°, lon=±180°)
  — le dénominateur nul est remplacé par `T.Epsilon` (valeur finie mais énorme) plutôt que de lever une
  exception ; documenté explicitement plutôt que changé, pour ne pas risquer un changement de
  comportement non validé.
- `Planet<T>.Area` : documentation des limites connues de la formule (polygones englobant un pôle,
  auto-intersectants, ou couvrant une très large fraction de la surface).
- Renommage du fichier `MollweidProjection.cs` → `MollweideProjection.cs` (la classe s'appelait déjà
  `MollweideProjection`, seul le nom de fichier avait une coquille).
- `README.md` : `Planets<double>.Moon` n'existe pas (les satellites sont préfixés par leur planète —
  `EarthMoon`, `JupiterEurope`, ... — c'est voulu) → corrigé en `Planets<double>.EarthMoon`. Le bearing
  Paris→Londres annoncé (`~296°`, à deux endroits) était faux ; vérifié par exécution réelle du
  snippet : la valeur correcte est `~330°`. Corrigé aux deux occurrences.

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
  centre de projection), `ProjectionBoundsTests.cs` (`Bounds`/`Normalize` par projection,
  `MercatorProjection<T>.MaxLatitude`, et la cohérence `MapPoint`/`RepresentationConverter`).
- Extension de `GeoPointTests.cs`/`GeoVectorTests.cs` : cohérence hash/égalité, non-double-parsing du
  constructeur string, comportement de `Recenter`.
- Extension de `RepresentationConverterTests.cs` : régression prouvant l'accord entre `MapPoint.Tile`
  et `RepresentationConverter.MappointToTile` pour un même `ProjectedPoint`.

Tous les nouveaux tests vont dans **UtilsTest.Unit** (aucune dépendance externe).
