using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC2163;

/// <summary>
/// Represents a PX (X.400/822 Gateway) record as defined in RFC 2163.
/// A PX record is used to locate a gateway that provides interoperability
/// between different messaging systems, typically bridging between RFC 822
/// (Internet mail) and X.400 (a legacy messaging system).
/// </summary>
/// <remarks>
/// <para>
/// The PX record contains three main fields:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <see cref="Preference"/>: a 16-bit integer that specifies the preference
///       for this gateway. Lower values indicate higher preference when multiple
///       PX records exist.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="Map822"/>: a domain name that specifies the gateway for RFC 822 mail.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="MapX400"/>: a domain name that specifies the gateway for X.400 mail.
///     </description>
///   </item>
/// </list>
/// <para>
/// PX records enable a mail system to choose an appropriate gateway for message delivery,
/// ensuring compatibility between different email protocols.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x1A)]
[DNSTextRecord("{Preference} {Map822} {MapX400}")]
internal class PX : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the 16-bit preference value of the PX record.
	/// A lower value indicates a higher priority for selecting this gateway.
	/// </summary>
	[DNSField]
	public ushort Preference { get; set; }

	/// <summary>
	/// Gets or sets the domain name for the gateway that handles RFC 822 (Internet) mail.
	/// </summary>
	[DNSField]
	public DNSDomainName Map822 { get; set; }

	/// <summary>
	/// Gets or sets the domain name for the gateway that handles X.400 mail.
	/// </summary>
	[DNSField]
	public DNSDomainName MapX400 { get; set; }
}
