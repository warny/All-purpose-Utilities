using Utils.Net.Structures;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1712;

/// <summary>
/// Represents a GPOS (Geographical Position) record as defined in RFC 1712.
/// GPOS records store geographical location information for a domain,
/// including longitude, latitude, and altitude.
/// </summary>
/// <remarks>
/// <para>
/// The GPOS record is used to provide location information for a domain in a human-readable format.
/// Each of the three fields (Longitude, Latitude, Altitude) is encoded as a string that represents
/// a double-precision floating-point value. The string encoding is expected to follow the rules specified
/// in RFC 1712.
/// </para>
/// <para>
/// In this implementation, each field is annotated with a <see cref="DNSFieldAttribute"/> using
/// the <see cref="FieldsSizeOptions.PrefixedSize1B"/> option, which means that the length of the string
/// is prefixed with a one-byte length indicator during DNS packet serialization.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x1B)]
[DNSTextRecord("{Longitude} {Latitude} {Altitude}")]
public class GPOS : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the longitude component of the geographical position.
	/// The value is stored as a <see cref="StringEncodedDouble"/>, representing
	/// the longitude in degrees.
	/// </summary>
	/// <remarks>
	/// The field is serialized with a one-byte length prefix.
	/// </remarks>
	[DNSField(FieldsSizeOptions.PrefixedSize1B)]
	public StringEncodedDouble Longitude { get; set; }

	/// <summary>
	/// Gets or sets the latitude component of the geographical position.
	/// The value is stored as a <see cref="StringEncodedDouble"/>, representing
	/// the latitude in degrees.
	/// </summary>
	/// <remarks>
	/// The field is serialized with a one-byte length prefix.
	/// </remarks>
	[DNSField(FieldsSizeOptions.PrefixedSize1B)]
	public StringEncodedDouble Latitude { get; set; }

	/// <summary>
	/// Gets or sets the altitude component of the geographical position.
	/// The value is stored as a <see cref="StringEncodedDouble"/>, representing
	/// the altitude in meters.
	/// </summary>
	/// <remarks>
	/// The field is serialized with a one-byte length prefix.
	/// </remarks>
	[DNSField(FieldsSizeOptions.PrefixedSize1B)]
	public StringEncodedDouble Altitude { get; set; }
}
