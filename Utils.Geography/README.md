# omy.Utils.Geography (mapping utilities)

`omy.Utils.Geography` provides coordinate models, geographic calculations, map projections, and tile utilities for map-centric applications.

## Install
```bash
dotnet add package omy.Utils.Geography
```

## Supported frameworks
- net8.0

## Features
- Generic `GeoPoint<T>`, `BoundingBox<T>`, `GeoVector<T>`, and `GeoPointList<T>` coordinate models.
- Planet model with great-circle distance, travel, and area calculations.
- Map projections: Mercator, Gall–Peters, Equirectangular, Mollweide, Miller, Lambert, Stereographic.
- Tile grid utilities for slippy-map-style rendering.

## Quick usage
```csharp
using Utils.Geography.Model;
using Utils.Geography.Projections;

var paris = new GeoPoint<double>(48.8566, 2.3522);
var london = new GeoPoint<double>(51.5074, -0.1278);
double dist = Planets<double>.Earth.Distance(paris, london); // ~340 km
```

## GeoPoint examples

`GeoPoint<T>` represents a geographic coordinate (latitude, longitude) in degrees.

### Construction

```csharp
using Utils.Geography.Model;

var paris   = new GeoPoint<double>(48.8566, 2.3522);
var equator = new GeoPoint<double>(0.0, 0.0);

// From DMS strings
var berlin = new GeoPoint<double>("52°30'N", "13°24'E");

// From a single comma-separated string
var tokyo = new GeoPoint<double>("35.6895, 139.6917");
```

### Properties and formatting

```csharp
Console.WriteLine(paris.Latitude);   // 48.8566
Console.WriteLine(paris.Longitude);  // 2.3522
Console.WriteLine(paris.φ);          // 48.8566 (alias for Latitude)
Console.WriteLine(paris.λ);          // 2.3522  (alias for Longitude)

Console.WriteLine(paris.ToString("d"));  // decimal degrees: 48.8566, 2.3522
Console.WriteLine(paris.ToString("D")); // DMS: 48°51'23.76"N 2°21'7.92"E

var (lat, lon) = paris; // deconstruct
```

### Angle between two points

```csharp
double angle = paris.AngleWith(london); // central angle in degrees (~3.06°)
```

## BoundingBox examples

`BoundingBox<T>` is an axis-aligned rectangle defined by minimum and maximum lat/lon values.

### Construction

```csharp
using Utils.Geography.Model;

var bbox = new BoundingBox<double>(
    minLatitude:  48.8,
    minLongitude: 2.2,
    maxLatitude:  48.9,
    maxLongitude: 2.5);

// From coordinate strings
var bbox2 = new BoundingBox<double>("48.8", "2.2", "48.9", "2.5");

// From a single "minLat,minLon,maxLat,maxLon" string
var bbox3 = BoundingBox<double>.FromString("48.8,2.2,48.9,2.5");
```

### Queries

```csharp
bool inside = bbox.Contains(paris);          // true
bool overlap = bbox.Intersects(bbox2);       // true

GeoPoint<double> center = bbox.GetCenterpoint();
Console.WriteLine(center.Latitude);          // 48.85

double latSpan = bbox.LatitudeSpan;          // 0.1
double lonSpan = bbox.LongitudeSpan;         // 0.3
```

## GeoVector examples

`GeoVector<T>` extends `GeoPoint<T>` with a bearing, representing a point and direction.

### Construction

```csharp
using Utils.Geography.Model;

// Explicit bearing (degrees from north, clockwise)
var v = new GeoVector<double>(48.8566, 2.3522, bearing: 90.0); // east

// Bearing from two points
var v2 = new GeoVector<double>(paris, london);
Console.WriteLine(v2.Bearing); // ~296° (NW towards London)
Console.WriteLine(v2.θ);       // same, alias for Bearing

// Reverse direction
var reversed = -v2;             // bearing + 180°
```

### Travel along a bearing

```csharp
// Travel from the vector's position for a given central angle (degrees)
GeoVector<double> arrived = v2.Travel(angleInDegrees: 1.0);
```

### Compute bearing between two points

```csharp
double bearing = GeoVector<double>.ComputeBearing(paris, london); // ~296°
```

### Intersection of two great-circle paths

```csharp
var v1 = new GeoVector<double>(paris, london);
var v3 = new GeoVector<double>(new GeoPoint<double>(50.0, 0.0), new GeoPoint<double>(48.0, 4.0));
GeoPoint<double>[] intersections = v1.Intersections(v3);
```

## Planet examples

`Planet<T>` encapsulates an ellipsoid and provides geodetic calculations. Ready-made instances live in `Planets<T>`.

### Planets

```csharp
using Utils.Geography.Model;

Planet<double> earth   = Planets<double>.Earth;
Planet<double> mars    = Planets<double>.Mars;
Planet<double> moon    = Planets<double>.Moon;
Planet<double> jupiter = Planets<double>.Jupiter;
```

### Great-circle distance

```csharp
double dist = Planets<double>.Earth.Distance(paris, london); // meters (~340 km)
```

### Travel from a vector

```csharp
var start = new GeoVector<double>(paris, london);
GeoVector<double> after100km = Planets<double>.Earth.Travel(start, 100_000.0); // 100 km
Console.WriteLine(after100km.Latitude);  // point 100 km along Paris→London route
```

### Polygon area

```csharp
var polygon = new List<GeoPoint<double>>
{
    new(48.8, 2.2),
    new(48.9, 2.2),
    new(48.9, 2.5),
    new(48.8, 2.5),
};
double area = Planets<double>.Earth.Area(polygon); // square meters
```

### Lat/lon span for a distance

```csharp
double latDeg = Planets<double>.Earth.LatitudeDistance(1000.0);   // degrees per 1 km
double lonDeg = Planets<double>.Earth.LongitudeDistance(1000.0, paris.Latitude);
```

## GeoPointList examples

`GeoPointList<T>` is a `List<GeoPoint<T>>` that tracks its own bounding box.

```csharp
using Utils.Geography.Model;

var route = new GeoPointList<double>
{
    paris,
    new GeoPoint<double>(50.6, 3.0),  // Lille
    london,
};

BoundingBox<double> bbox = route.BoundingBox;
Console.WriteLine(bbox.MinLatitude);  // 48.8566
Console.WriteLine(bbox.MaxLatitude);  // 51.5074

// GeoPointList2<T> — list of lists (e.g. polygon with holes)
var multi = new GeoPointList2<double> { route };
BoundingBox<double> outerBbox = multi.BoundingBox;
```

## Projection examples

`Projections<T>` provides cached instances of all supported map projections.

### Forward projection (geo → map)

```csharp
using Utils.Geography.Projections;
using Utils.Geography.Model;

IProjectionTransformation<double> proj = Projections<double>.Mercator;

ProjectedPoint<double> pt = proj.GeoPointToMapPoint(paris);
Console.WriteLine($"x={pt.X:F4}, y={pt.Y:F4}");
```

### Inverse projection (map → geo)

```csharp
GeoPoint<double> back = proj.MapPointToGeoPoint(pt);
Console.WriteLine(back.Latitude);  // ≈ 48.8566
```

### Available projections

| Key              | Property                       | Description                              |
|------------------|--------------------------------|------------------------------------------|
| `mercator`       | `Projections<T>.Mercator`      | Conformal, X=lon, Y=ln(tan(45+lat/2))   |
| `gallspeters`    | `Projections<T>.GallsPeters`   | Equal-area cylindrical                   |
| `equirectangular`| `Projections<T>.Equirectangular`| Simple plate carrée                     |
| `mollweide`      | `Projections<T>.Mollweide`     | Equal-area pseudo-cylindrical            |
| `miller`         | `Projections<T>.Miller`        | Modified Mercator, less distortion       |
| `lambert`        | `Projections<T>.Lambert`       | Azimuthal equal-area                     |
| `stereographic`  | `Projections<T>.Stereographic` | Conformal azimuthal                      |

```csharp
// By name (case-insensitive)
IProjectionTransformation<double> p = Projections<double>.GetProjection("lambert");
```

## RepresentationConverter examples

`RepresentationConverter<T>` bridges geo coordinates, projected points, and tile grid coordinates.

```csharp
using Utils.Geography.Display;
using Utils.Geography.Model;
using Utils.Geography.Projections;

var converter = new RepresentationConverter<double>(Projections<double>.Mercator, tileSize: 256);

// Geo → projected point
ProjectedPoint<double> projected = converter.GeoPointToMappoint(paris, zoomFactor: 0);

// Projected → tile at zoom 10
Tile<double> tile = converter.MappointToTile(projected, zoomLevel: 10);
Console.WriteLine($"tile x={tile.TileX}, y={tile.TileY}, zoom={tile.ZoomFactor}");

// Total map size at zoom 8 (256 * 2^8 = 65536 px)
int mapSize = converter.GetMapSize(zoomLevel: 8);

// Ground resolution (meters/pixel) at Paris latitude, zoom 14
double resolution = converter.ComputeGroundResolution(paris.Latitude, zoomLevel: 14);
```

## MapPosition examples

`MapPosition<T>` pairs a geographic center point with a zoom level.

```csharp
using Utils.Geography.Model;

var position = new MapPosition<double>(paris, zoomLevel: 12);
Console.WriteLine(position.GeoPoint.Latitude); // 48.8566
Console.WriteLine(position.ZoomLevel);         // 12
```

## Related packages
- `omy.Utils.Imaging` – for rendering map overlays.
- `omy.Utils.Mathematics` – math helpers used by projections.
