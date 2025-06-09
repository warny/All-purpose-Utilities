namespace Utils.Net.DNS.RFC4034;

/// <summary>
/// Represents an NSEC (Next Secure) record as defined in RFC 4034.
/// NSEC records are used in DNSSEC to provide authenticated denial of existence for a
/// range of domain names and RR types. They create a chain of all the names in a zone,
/// indicating which types exist for a given owner name and which names do not exist.
/// </summary>
/// <remarks>
/// <para>
/// The RDATA for an NSEC RR is formatted as follows:
/// </para>
/// <code>
///                             1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
///         0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///        /                      Next Domain Name                         /
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///        /                       Type Bit Maps                           /
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// </code>
/// <para>
/// Fields:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>NextDomainName</b>: A domain name indicating the next owner name in the zone's
///       canonical order. The set of NSEC records in a zone forms a complete chain,
///       covering the entire namespace.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Type Bitmaps</b>: A variable-length bit map that specifies which RR types are present
///       for the owner name. Each bit corresponds to a particular RR type; a bit set to 1 means that
///       at least one RR of that type exists.
///     </description>
///   </item>
/// </list>
/// <para>
/// The NSEC record is used in the denial-of-existence process. If a queried name does not exist,
/// a security-aware server returns an NSEC record from the zone that proves that the name is not present,
/// along with its signature (RRSIG).
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x2F)]
[DNSTextRecord("{NextDomainName} {TypeBitmaps}")]
public class NSEC : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the next domain name in the zone's canonical ordering.
	/// This field indicates the beginning of the next interval in which no owner name exists.
	/// </summary>
	[DNSField]
	public DNSDomainName NextDomainName { get; set; }

	/// <summary>
	/// Gets or sets the type bit maps field.
	/// This variable-length field is a bitmap where each bit represents the presence of a particular RR type
	/// for the owner name. For example, if bit 15 is set, it indicates that RRs of type 15 exist.
	/// </summary>
	[DNSField]
	public byte[] TypeBitmaps { get; set; }
}
