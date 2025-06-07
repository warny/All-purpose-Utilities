using System;
using System.Linq;
using System.Text;
using Utils.Net;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1876;

/// <summary>
/// Represents a LOC (Location) record in DNS as defined in RFC 1876.
/// The LOC record specifies the physical location associated with a domain name,
/// including its geographical coordinates, altitude, and precision values.
/// </summary>
/// <remarks>
/// <para>
/// The binary format of the LOC record is defined as follows (each field is one or more octets):
/// </para>
/// <code>
///   MSB                                           LSB
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// 0 |        VERSION        |         SIZE          |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// 2 |       HORIZ PRE       |       VERT PRE        |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// 4 |                   LATITUDE                    |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// 6 |                   LATITUDE                    |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// 8 |                   LONGITUDE                   |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
///10 |                   LONGITUDE                   |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
///12 |                   ALTITUDE                    |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
///14 |                   ALTITUDE                    |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// (octet)
/// </code>
/// <para>
/// The fields are defined as:
/// <list type="bullet">
///   <item>
///     <description><b>VERSION</b>: A one-octet version number (must be zero).</description>
///   </item>
///   <item>
///     <description><b>SIZE</b>: The diameter of a sphere enclosing the described entity, in centimeters.
///     It is represented as a pair of four-bit unsigned integers: the first (most significant)
///     is the base, and the second is the exponent (power of 10). For example, 0x15 represents 1e5 cm.</description>
///   </item>
///   <item>
///     <description><b>HORIZ PRE</b>: The horizontal precision of the location (i.e., the diameter of the
///     horizontal "circle of error"), expressed in centimeters with the same exponential representation as SIZE.
///     To obtain the value in meters, divide by 100.</description>
///   </item>
///   <item>
///     <description><b>VERT PRE</b>: The vertical precision, representing the total potential vertical error,
///     expressed in centimeters using the same exponential representation. To convert to meters, divide by 100.</description>
///   </item>
///   <item>
///     <description><b>LATITUDE</b>: The latitude of the location, stored as a 32-bit integer in network byte order,
///     representing thousandths of a second of arc. A value of 2^31 indicates the equator; values above that are north.</description>
///   </item>
///   <item>
///     <description><b>LONGITUDE</b>: The longitude of the location, stored as a 32-bit integer in network byte order,
///     representing thousandths of a second of arc, rounded away from the prime meridian (2^31). Values above 2^31 are east.</description>
///   </item>
///   <item>
///     <description><b>ALTITUDE</b>: The altitude of the location, stored as a 32-bit integer in centimeters,
///     with an offset such that a value of <c>altitudeZeroCorrection</c> represents 0 meters relative to the WGS 84 spheroid.
///     To convert to meters, divide by 100.</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// The class also provides properties that convert the exponential representation into standard
/// double values (e.g., meters for precision, seconds of arc for latitude/longitude, etc.),
/// and vice versa.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x1D)]
[DNSTextRecord("{Latitude} {Longitude} {Altitude}")]
public class LOC : DNSResponseDetail
{
	// The fields below are annotated with [DNSField] so that they are automatically
	// serialized/deserialized as per the DNS LOC RDATA format.

	/// <summary>
	/// Gets or sets the version number of the LOC record format. This must be 0.
	/// </summary>
	[DNSField]
	public byte Version { get; set; }

	/// <summary>
	/// Private field storing the SIZE value in its exponential representation (one byte).
	/// </summary>
	[DNSField]
	private byte size { get; set; }

	/// <summary>
	/// Private field storing the horizontal precision (HORIZ PRE) in exponential representation (one byte).
	/// </summary>
	[DNSField]
	private byte horizontalPrecision { get; set; }

	/// <summary>
	/// Private field storing the vertical precision (VERT PRE) in exponential representation (one byte).
	/// </summary>
	[DNSField]
	private byte verticalPrecision { get; set; }

	/// <summary>
	/// Private field storing the latitude as a 32-bit unsigned integer (network byte order).
	/// The value is in thousandths of a second of arc.
	/// </summary>
	[DNSField]
	private uint latitude { get; set; }

	/// <summary>
	/// Private field storing the longitude as a 32-bit unsigned integer (network byte order).
	/// The value is in thousandths of a second of arc.
	/// </summary>
	[DNSField]
	private uint longitude { get; set; }

	/// <summary>
	/// Private field storing the altitude as a 32-bit unsigned integer (network byte order).
	/// The altitude is stored in centimeters.
	/// </summary>
	[DNSField]
	private uint altitude { get; set; }

	// Constants used for conversion:
	private const double equatorLatitude = 2_147_483_648; // 2^31, represents 0° latitude.
	private const double primeMeridian = 2_147_483_648;     // 2^31, represents 0° longitude.
	private const double altitudeZeroCorrection = 100_000_00; // Represents 0 m altitude relative to a base 100,000 m below WGS 84.
	private const double arcSec = 1_296_000;                // Thousandths of seconds of arc per degree? (Typically 3600 sec/degree * 360 degrees = 1,296,000)
	private const double meter2Centimeter = 100;

	/// <summary>
	/// Converts an exponential value (stored in one byte) to a double.
	/// The high 4 bits represent the base and the low 4 bits represent the exponent.
	/// </summary>
	/// <param name="value">The byte to convert.</param>
	/// <returns>The computed value.</returns>
	private double ExponentialValueConvert(byte value) => (value >> 4) * Math.Pow(10, value & 0xF);

	/// <summary>
	/// Converts a double into its exponential representation in one byte.
	/// The high 4 bits will store the mantissa and the low 4 bits the exponent.
	/// </summary>
	/// <param name="value">The double value to convert.</param>
	/// <returns>A byte representing the exponential encoding.</returns>
	private byte InverseExponentialValueConvert(double value)
	{
		int exponent = (int)Math.Floor(Math.Log10(value));
		int mantissa = (int)Math.Round(value / Math.Pow(10, exponent));
		return (byte)((mantissa << 4) + exponent);
	}

	/// <summary>
	/// Gets or sets the size (the diameter of the sphere enclosing the entity) in centimeters.
	/// The value is stored internally in an exponential representation.
	/// </summary>
	public double Size
	{
		get => ExponentialValueConvert(size);
		set => size = InverseExponentialValueConvert(value);
	}

	/// <summary>
	/// Gets or sets the horizontal precision (diameter of the circle of error) in meters.
	/// Internally stored as centimeters in an exponential representation.
	/// </summary>
	public double HorizontalPrecision
	{
		get => ExponentialValueConvert(horizontalPrecision) / meter2Centimeter;
		set => horizontalPrecision = InverseExponentialValueConvert(value * meter2Centimeter);
	}

	/// <summary>
	/// Gets or sets the vertical precision (total potential vertical error) in meters.
	/// Internally stored as centimeters in an exponential representation.
	/// </summary>
	public double VerticalPrecision
	{
		get => ExponentialValueConvert(verticalPrecision) / meter2Centimeter;
		set => verticalPrecision = InverseExponentialValueConvert(value * meter2Centimeter);
	}

	/// <summary>
	/// Gets or sets the latitude of the location, in seconds of arc.
	/// The latitude is stored as a 32-bit integer (network byte order) where a value of 2^31
	/// represents the equator. Values above 2^31 are interpreted as north.
	/// </summary>
	public double Latitude
	{
		get => (latitude - equatorLatitude) / arcSec;
		set => latitude = (uint)((value * arcSec) + equatorLatitude);
	}

	/// <summary>
	/// Gets or sets the longitude of the location, in seconds of arc.
	/// The longitude is stored as a 32-bit integer (network byte order) where a value of 2^31
	/// represents the prime meridian. Values above 2^31 are interpreted as east.
	/// </summary>
	public double Longitude
	{
		get => (longitude - primeMeridian) / arcSec;
		set => longitude = (uint)((value * arcSec) + primeMeridian);
	}

	/// <summary>
	/// Gets or sets the altitude of the location, in meters.
	/// The altitude is stored as a 32-bit integer (network byte order) in centimeters,
	/// with a base offset so that a specific value corresponds to 0 meters relative to the WGS 84 spheroid.
	/// </summary>
	public double Altitude
	{
		get => (altitude - altitudeZeroCorrection) / meter2Centimeter;
		set => altitude = (uint)((value * meter2Centimeter) + altitudeZeroCorrection);
	}

	/// <summary>
	/// Returns a string representation of the LOC record, including the geographic coordinates,
	/// altitude, and the size and precision of the location.
	/// </summary>
	/// <returns>A formatted string representing the LOC record.</returns>
	public override string ToString()
	{
		return $"L: {Latitude}°  l: {Longitude}°  A: {Altitude}m \n" +
			   $"\tSize: {Size}cm  Precision (H: {HorizontalPrecision}m, V: {VerticalPrecision}m)";
	}
}
