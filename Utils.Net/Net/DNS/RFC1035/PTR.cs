using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents a PTR (Pointer) record in DNS, as specified by
/// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.12">RFC 1035 §3.3.12</see>.
/// </summary>
/// <remarks>
/// <para>
/// A PTR record is used to map (or "point") a domain name to another. Typically, PTR records
/// are most commonly known for usage in the <c>IN-ADDR.ARPA</c> zone to perform reverse lookups,
/// mapping IP addresses to hostnames. However, a PTR can also appear in other namespaces,
/// simply indicating a pointer to another location in the domain space.
/// </para>
/// <para>
/// Unlike <c>CNAME</c> records, PTRs do not imply special alias or canonical name behavior and
/// cause no additional DNS processing for resolution. They are simply a data pointer. When
/// placed in the <c>IN-ADDR.ARPA</c> domain, the name is usually the reverse-notation IP
/// address (e.g., <c>1.2.168.192.in-addr.arpa</c>), and the <see cref="PTRName"/> is the
/// corresponding hostname (e.g., <c>host.example.com</c>).
/// </para>
/// <para>The RDATA format is the target domain name:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                   PTRDNAME                    /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para><c>PTRDNAME</c> is a <c>&lt;domain-name&gt;</c> indicating the pointer destination.</para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x0C)]
[DNSTextRecord("{PTRName}")]
public class PTR : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the domain name to which this PTR record points.
	/// </summary>
	/// <remarks>
	/// For example, in a reverse DNS entry, <c>PTRName</c> would hold
	/// the canonical hostname associated with the IP's reverse-lookup domain.
	/// </remarks>
	[DNSField]
	public DNSDomainName PTRName { get; set; }

	/// <summary>
	/// Returns the string representation of <see cref="PTRName"/>.
	/// Because <see cref="DNSDomainName"/> is a struct, it will never be <c>null</c>,
	/// but may be an empty or default value if unset.
	/// </summary>
	public override string ToString() => PTRName.ToString();
}
