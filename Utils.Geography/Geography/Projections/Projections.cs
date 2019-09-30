using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Geography.Projections
{
	public static class Projections
	{
		private static readonly Lazy<MercatorProjection> mercator = new Lazy<MercatorProjection>(() => new MercatorProjection());
		private static readonly Lazy<GallsPetersProjection> gallsPeters = new Lazy<GallsPetersProjection>(() => new GallsPetersProjection());
		private static readonly Lazy<EquirectangularProjection> equirectangular = new Lazy<EquirectangularProjection>(() => new EquirectangularProjection());
		private static readonly Lazy<MollweidProjection> mollweid = new Lazy<MollweidProjection>(() => new MollweidProjection());
		public static MercatorProjection Mercator => mercator.Value;
		public static GallsPetersProjection GallsPeters => gallsPeters.Value;
		public static EquirectangularProjection Equirectangular => equirectangular.Value;
		public static MollweidProjection Mollweid => mollweid.Value;

		public static IProjectionTransformation GetProjection ( string name )
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
