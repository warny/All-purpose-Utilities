# omy.Utils.Geography (mapping utilities)

`omy.Utils.Geography` provides coordinate models, map projection helpers, and tile utilities for map-centric applications.

## Install
```bash
dotnet add package omy.Utils.Geography
```

## Supported frameworks
- net8.0

## Features
- Geo coordinate and bounding box primitives.
- Map projection helpers to translate between screen and earth coordinates.
- Tile helpers for map providers.

## Quick usage
```csharp
using Utils.Geography;

var paris = new GeoCoordinate(48.8566, 2.3522);
var bbox = new BoundingBox(paris, 0.25, 0.25);
bool contains = bbox.Contains(paris);
```

## Related packages
- `omy.Utils.Imaging` – for rendering map overlays.
- `omy.Utils.Mathematics` – math helpers used by projections.
