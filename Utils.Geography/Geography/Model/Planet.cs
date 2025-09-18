using System;
using System.Diagnostics;
using System.Numerics;
using System.Collections.Generic;
using Utils.Mathematics;

namespace Utils.Geography.Model;

/// <summary>
/// Represents a planet with geodesic properties, such as equatorial radius and circumference.
/// </summary>
/// <typeparam name="T">A numeric type that supports IEEE 754 floating-point operations.</typeparam>
[DebuggerDisplay("{Name} (Equatorial Radius={EquatorialRadius}, Circumference={EquatorialCircumference})")]
public class Planet<T> where T : struct, IFloatingPointIeee754<T>
{
	private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

	/// <summary>
	/// Initializes a new instance of the <see cref="Planet{T}"/> class.
	/// </summary>
	/// <param name="equatorialRadius">The equatorial radius of the planet.</param>
	/// <param name="name">The name of the planet (optional).</param>
	public Planet(T equatorialRadius, string name = null)
	{
		EquatorialRadius = equatorialRadius;
		EquatorialCircumference = T.Pi * T.CreateChecked(2) * equatorialRadius; // Circumference = 2 * pi * radius
		Name = name ?? "Unnamed";
	}

	/// <summary>
	/// The name of the planet.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// The equatorial radius of the planet.
	/// </summary>
	public T EquatorialRadius { get; }

	/// <summary>
	/// The equatorial circumference of the planet.
	/// </summary>
	public T EquatorialCircumference { get; }

	/// <summary>
	/// Calculates the degrees of latitude that correspond to a specified distance on the planet's surface.
	/// </summary>
	/// <param name="meters">The distance in meters.</param>
	/// <returns>The degrees of latitude corresponding to the given distance.</returns>
	public T LatitudeDistance(T meters)
	{
		return (meters * degree.Perigon) / (T.Pi * T.CreateChecked(2) * EquatorialRadius);
	}

	/// <summary>
	/// Calculates the degrees of longitude that correspond to a specified distance at a given latitude.
	/// </summary>
	/// <param name="meters">The distance in meters.</param>
	/// <param name="latitude">The latitude at which the calculation should be performed.</param>
	/// <returns>The degrees of longitude corresponding to the given distance at the specified latitude.</returns>
	public T LongitudeDistance(T meters, T latitude)
	{
		return (meters * degree.Perigon) / (T.Pi * T.CreateChecked(2) * EquatorialRadius * degree.Cos(latitude));
	}

	/// <summary>
	/// Calculates the distance between two geographical points on the planet's surface.
	/// </summary>
	/// <param name="geoPoint1">The first geographical point.</param>
	/// <param name="geoPoint2">The second geographical point.</param>
	/// <returns>The distance between the two points in meters.</returns>
	public T Distance(GeoPoint<T> geoPoint1, GeoPoint<T> geoPoint2)
	{
		return degree.ToRadian(geoPoint1.AngleWith(geoPoint2)) * EquatorialRadius;
	}

	/// <summary>
	/// Travels a given distance along a geodesic path starting from the provided vector.
	/// </summary>
	/// <param name="geoVector">The starting vector.</param>
	/// <param name="distance">The distance to travel in meters.</param>
	/// <returns>A new <see cref="GeoVector{T}"/> representing the destination point.</returns>
        public GeoVector<T> Travel(GeoVector<T> geoVector, T distance)
        {
                return geoVector.Travel(degree.FromRadian(distance / EquatorialRadius));
        }

        /// <summary>
        /// Calculates the surface area of a spherical polygon defined by a list of geographic points.
        /// </summary>
        /// <param name="points">The polygon vertices in order. The first and last point need not be equal.</param>
        /// <returns>The area of the polygon in square meters.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than three points are supplied.</exception>
        public T Area(IReadOnlyList<GeoPoint<T>> points)
        {
                ArgumentNullException.ThrowIfNull(points);
                if (points.Count < 3)
                        throw new ArgumentException("At least three points are required", nameof(points));

                T radius = T.CreateChecked(EquatorialRadius);
                var deg = Trigonometry<T>.Degree;

                T total = T.Zero;
                for (int i = 0; i < points.Count; i++)
                {
                        var a = points[i];
                        var b = points[(i + 1) % points.Count];

                        T lon1 = deg.ToRadian(T.CreateChecked(a.Longitude));
                        T lon2 = deg.ToRadian(T.CreateChecked(b.Longitude));
                        T lat1 = deg.ToRadian(T.CreateChecked(a.Latitude));
                        T lat2 = deg.ToRadian(T.CreateChecked(b.Latitude));

                        T dLon = lon2 - lon1;
                        if (dLon < -T.Pi) dLon += T.Tau;
                        if (dLon > T.Pi) dLon -= T.Tau;

                        total += dLon * (T.Sin(lat1) + T.Sin(lat2));
                }

                T area = T.Abs(total) * radius * radius / T.CreateChecked(2);
                return area;
        }
}

/// <summary>
/// Contains predefined planet models with their respective equatorial radii.
/// </summary>
/// <typeparam name="T">A numeric type that supports IEEE 754 floating-point operations.</typeparam>
public static class Planets<T> where T : struct, IFloatingPointIeee754<T>
{
        /// <summary>
        /// Gets a predefined planet model for Mercury.
        /// </summary>
        public static Planet<T> Mercury { get; } = new Planet<T>((T)Convert.ChangeType(2439700, typeof(T)), "Mercury");
        /// <summary>
        /// Gets a predefined planet model for Venus.
        /// </summary>
        public static Planet<T> Venus { get; } = new Planet<T>((T)Convert.ChangeType(6051800, typeof(T)), "Venus");
        /// <summary>
        /// Gets a predefined planet model for Earth.
        /// </summary>
        public static Planet<T> Earth { get; } = new Planet<T>((T)Convert.ChangeType(6378137, typeof(T)), "Earth");
        /// <summary>
        /// Gets a predefined planet model for Earth's Moon.
        /// </summary>
        public static Planet<T> EarthMoon { get; } = new Planet<T>((T)Convert.ChangeType(1737400, typeof(T)), "Moon");
        /// <summary>
        /// Gets a predefined planet model for Mars.
        /// </summary>
        public static Planet<T> Mars { get; } = new Planet<T>((T)Convert.ChangeType(3396200, typeof(T)), "Mars");
        /// <summary>
        /// Gets a predefined planet model for Jupiter.
        /// </summary>
        public static Planet<T> Jupiter { get; } = new Planet<T>((T)Convert.ChangeType(71492000, typeof(T)), "Jupiter");
        /// <summary>
        /// Gets a predefined planet model for Jupiter's moon Europa.
        /// </summary>
        public static Planet<T> JupiterEurope { get; } = new Planet<T>((T)Convert.ChangeType(1560800, typeof(T)), "Europe");
        /// <summary>
        /// Gets a predefined planet model for Jupiter's moon Ganymede.
        /// </summary>
        public static Planet<T> JupiterGanymede { get; } = new Planet<T>((T)Convert.ChangeType(2634100, typeof(T)), "Ganymede");
        /// <summary>
        /// Gets a predefined planet model for Jupiter's moon Io.
        /// </summary>
        public static Planet<T> JupiterIo { get; } = new Planet<T>((T)Convert.ChangeType(1821600, typeof(T)), "Io");
        /// <summary>
        /// Gets a predefined planet model for Jupiter's moon Callisto.
        /// </summary>
        public static Planet<T> JupiterCallisto { get; } = new Planet<T>((T)Convert.ChangeType(2410300, typeof(T)), "Callisto");
        /// <summary>
        /// Gets a predefined planet model for Saturn.
        /// </summary>
        public static Planet<T> Saturn { get; } = new Planet<T>((T)Convert.ChangeType(60268000, typeof(T)), "Saturn");
        /// <summary>
        /// Gets a predefined planet model for Saturn's moon Titan.
        /// </summary>
        public static Planet<T> SaturnTitan { get; } = new Planet<T>((T)Convert.ChangeType(2574700, typeof(T)), "Titan");
        /// <summary>
        /// Gets a predefined planet model for Saturn's moon Enceladus.
        /// </summary>
        public static Planet<T> SaturnEnceladus { get; } = new Planet<T>((T)Convert.ChangeType(252100, typeof(T)), "Enceladus");
        /// <summary>
        /// Gets a predefined planet model for Saturn's moon Tethys.
        /// </summary>
        public static Planet<T> SaturnThetys { get; } = new Planet<T>((T)Convert.ChangeType(531000, typeof(T)), "Thetys");
        /// <summary>
        /// Gets a predefined planet model for Saturn's moon Mimas.
        /// </summary>
        public static Planet<T> SaturnMimas { get; } = new Planet<T>((T)Convert.ChangeType(198200, typeof(T)), "Mimas");
        /// <summary>
        /// Gets a predefined planet model for Saturn's moon Dione.
        /// </summary>
        public static Planet<T> SaturnDione { get; } = new Planet<T>((T)Convert.ChangeType(561400, typeof(T)), "Dione");
        /// <summary>
        /// Gets a predefined planet model for Saturn's moon Iapetus.
        /// </summary>
        public static Planet<T> SaturnIapetus { get; } = new Planet<T>((T)Convert.ChangeType(734500, typeof(T)), "Iapetus");
        /// <summary>
        /// Gets a predefined planet model for Saturn's moon Rhea.
        /// </summary>
        public static Planet<T> SaturnRhea { get; } = new Planet<T>((T)Convert.ChangeType(763800, typeof(T)), "Rhea");
        /// <summary>
        /// Gets a predefined planet model for Uranus.
        /// </summary>
        public static Planet<T> Uranus { get; } = new Planet<T>((T)Convert.ChangeType(25559000, typeof(T)), "Uranus");
        /// <summary>
        /// Gets a predefined planet model for Uranus' moon Umbriel.
        /// </summary>
        public static Planet<T> UranusUmbriel { get; } = new Planet<T>((T)Convert.ChangeType(584700, typeof(T)), "Umbriel");
        /// <summary>
        /// Gets a predefined planet model for Uranus' moon Oberon.
        /// </summary>
        public static Planet<T> UranusOberon { get; } = new Planet<T>((T)Convert.ChangeType(761400, typeof(T)), "Oberon");
        /// <summary>
        /// Gets a predefined planet model for Uranus' moon Titania.
        /// </summary>
        public static Planet<T> UranusTitania { get; } = new Planet<T>((T)Convert.ChangeType(788400, typeof(T)), "Titania");
        /// <summary>
        /// Gets a predefined planet model for Uranus' moon Miranda.
        /// </summary>
        public static Planet<T> UranusMiranda { get; } = new Planet<T>((T)Convert.ChangeType(235800, typeof(T)), "Miranda");
        /// <summary>
        /// Gets a predefined planet model for Uranus' moon Ariel.
        /// </summary>
        public static Planet<T> UranusAriel { get; } = new Planet<T>((T)Convert.ChangeType(578900, typeof(T)), "Ariel");
        /// <summary>
        /// Gets a predefined planet model for Neptune.
        /// </summary>
        public static Planet<T> Neptune { get; } = new Planet<T>((T)Convert.ChangeType(24764000, typeof(T)), "Neptune");
        /// <summary>
        /// Gets a predefined planet model for Neptune's moon Triton.
        /// </summary>
        public static Planet<T> NeptuneTriton { get; } = new Planet<T>((T)Convert.ChangeType(1353400, typeof(T)), "Triton");
}