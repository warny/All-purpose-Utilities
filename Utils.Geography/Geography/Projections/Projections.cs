using System;
using System.Numerics;

namespace Utils.Geography.Projections
{
	public static class Projections<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        private static readonly Lazy<MercatorProjection<T>> mercator = new(() => new ());
		private static readonly Lazy<GallsPetersProjection<T>> gallsPeters = new (() => new ());
		private static readonly Lazy<EquirectangularProjection<T>> equirectangular = new (() => new ());
		private static readonly Lazy<MollweidProjection<T>> mollweid = new (() => new ());
		public static MercatorProjection<T> Mercator => mercator.Value;
		public static GallsPetersProjection<T> GallsPeters => gallsPeters.Value;
		public static EquirectangularProjection<T> Equirectangular => equirectangular.Value;
		public static MollweidProjection<T> Mollweid => mollweid.Value;

		public static IProjectionTransformation<T> GetProjection ( string name )
		{
			switch (name.ToLower()) {
				case "mercator": 
					return Mercator;
				case "gallspeters":
					return GallsPeters;
				case "equirectangular":
					return Equirectangular;
				case "mollweid":
					return Mollweid;
				default:
					throw new ArgumentOutOfRangeException("name", string.Format("La projection \"{0}\" n'est pas supportée", name));
			}
		}
	}
}
