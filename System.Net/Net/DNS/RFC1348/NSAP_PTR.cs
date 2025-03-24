namespace Utils.Net.DNS.RFC1348;

/// <summary>
/// Represents an NSAP-PTR record, which provides a mapping from a domain name to an NSAP address.
/// </summary>
/// <remarks>
/// <para>
/// The NSAP-PTR record (type code 0x16) is defined in RFC 1348. It is used to map a domain name
/// to an NSAP (Network Service Access Point) address. This record is part of the DNS extensions
/// that were designed to support OSI networking protocols and legacy network architectures.
/// </para>
/// <para>
/// This record contains a single domain name field (<see cref="DomainName"/>), which represents
/// the target domain associated with the NSAP address.
/// </para>
/// <para>
/// Note that NSAP-PTR records are rarely used in modern DNS implementations.
/// </para>
/// <para>
/// For additional details, see <see href="https://datatracker.ietf.org/doc/html/rfc1348"/>.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x16, "NSAP-PTR")]
public class NSAP_PTR : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the domain name that maps to an NSAP address.
	/// </summary>
	[DNSField]
	public DNSDomainName DomainName { get; set; }
}
