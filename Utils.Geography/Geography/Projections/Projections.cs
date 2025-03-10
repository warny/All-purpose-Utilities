using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.Collections;

namespace Utils.Geography.Projections
{
	public static class Projections<T>
        where T : struct, IFloatingPointIeee754<T>
    {
		private static CachedLoader<string, IProjectionTransformation<T>> _cache =
			new(
				static (string key, out IProjectionTransformation<T> value) => {
					value = key switch
					{
						"mercator" => new MercatorProjection<T>(),
						"gallspeters" => new GallsPetersProjection<T>(),
						"equirectangular" => new EquirectangularProjection<T>(),
						"mollweide" => new MollweideProjection<T>(),
						"miller" => new MillerProjection<T>(),
						"lambert" => new LambertAzimuthalEqualArea<T>(),
						"stereographic" => new StereographicProjection<T>(),
						_ => null,
					};
					return value != null;
				},
				new Dictionary<string, IProjectionTransformation<T>>(StringComparer.InvariantCultureIgnoreCase)
			);

		public static IProjectionTransformation<T> Mercator => _cache["mercator"];
		public static IProjectionTransformation<T> GallsPeters => _cache["gallspeters"];
		public static IProjectionTransformation<T> Equirectangular => _cache["equirectangular"];
		public static IProjectionTransformation<T> Mollweide => _cache["mollweide"];
		public static IProjectionTransformation<T> Miller => _cache["miller"];
		public static IProjectionTransformation<T> Lambert => _cache["lambert"];
		public static IProjectionTransformation<T> Stereographic => _cache["stereographic"];

		public static IProjectionTransformation<T> GetProjection(string name) => _cache[name];
	}
}
