# Utils.Geography Library

**Utils.Geography** provides models and helpers for working with geographic coordinates, map projections and tile systems.
It targets **.NET 9** and is suitable for GIS style applications and mapping tools.

## Features

- Conversions between geographic and polar coordinates
- Distance calculations on the ellipsoid or a sphere
- Support for common map projections and tile addressing schemes
- Utility types for bounding boxes and geohash operations

## Usage examples
```csharp
var projection = new Utils.Geography.Projections.MercatorProjection<double>();
var paris = new Utils.Geography.Model.GeoPoint<double>(48.8566, 2.3522);
var point = projection.GeoPointToMapPoint(paris);
var newYork = new Utils.Geography.Model.GeoPoint<double>(40.7128, -74.0060);
double distance = Utils.Geography.Model.Planets<double>.Earth.Distance(paris, newYork);
var vector = new Utils.Geography.Model.GeoVector<double>(paris, 90);
var dest = Utils.Geography.Model.Planets<double>.Earth.Travel(vector, 1000);
var lambert = Utils.Geography.Projections.Projections<double>.Lambert;
var lambertPoint = lambert.GeoPointToMapPoint(paris);
```
