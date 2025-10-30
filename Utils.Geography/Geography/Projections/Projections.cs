using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.Collections;

namespace Utils.Geography.Projections
{
    /// <summary>
    /// Provides cached access to a collection of well-known map projections.
    /// </summary>
    /// <typeparam name="T">Floating-point type used by the projections.</typeparam>
    public static class Projections<T>
            where T : struct, IFloatingPointIeee754<T>
    {
        /// <summary>
        /// Lazily loads projection implementations keyed by their canonical name.
        /// </summary>
        private static CachedLoader<string, IProjectionTransformation<T>> _cache =
                new(
                        static (string key, out IProjectionTransformation<T> value) =>
                        {
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

        /// <summary>
        /// Gets the Mercator projection instance.
        /// </summary>
        public static IProjectionTransformation<T> Mercator => _cache["mercator"];

        /// <summary>
        /// Gets the Gall–Peters projection instance.
        /// </summary>
        public static IProjectionTransformation<T> GallsPeters => _cache["gallspeters"];

        /// <summary>
        /// Gets the equirectangular projection instance.
        /// </summary>
        public static IProjectionTransformation<T> Equirectangular => _cache["equirectangular"];

        /// <summary>
        /// Gets the Mollweide projection instance.
        /// </summary>
        public static IProjectionTransformation<T> Mollweide => _cache["mollweide"];

        /// <summary>
        /// Gets the Miller projection instance.
        /// </summary>
        public static IProjectionTransformation<T> Miller => _cache["miller"];

        /// <summary>
        /// Gets the Lambert azimuthal equal-area projection instance.
        /// </summary>
        public static IProjectionTransformation<T> Lambert => _cache["lambert"];

        /// <summary>
        /// Gets the stereographic projection instance.
        /// </summary>
        public static IProjectionTransformation<T> Stereographic => _cache["stereographic"];

        /// <summary>
        /// Retrieves a projection by its name using a case-insensitive lookup.
        /// </summary>
        /// <param name="name">Projection name.</param>
        /// <returns>The requested projection transformation.</returns>
        public static IProjectionTransformation<T> GetProjection(string name) => _cache[name];
    }
}
