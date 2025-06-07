namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents a DNS CNAME (Canonical Name) record, used to alias one domain name to another.
/// </summary>
/// <remarks>
/// <para>
/// A CNAME record indicates that the specified <see cref="CName"/> is the canonical or primary
/// name for the alias represented by the record’s owner name. For example, if "www.example.com"
/// is a CNAME to "example.com", then "www.example.com" is the alias, and "example.com" is the
/// canonical name.
/// </para>
/// <para>
/// CNAME records do not directly trigger additional section processing in standard DNS resolution.
/// However, a DNS server may internally restart the resolution at the canonical name, as described
/// in RFC 1034 Section 3.6.2.
/// </para>
/// <para>
/// This class corresponds to a DNS record with type code <c>0x05</c> and DNS class <see cref="DNSClass.IN"/>.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x05)]
[DNSTextRecord("{CName}")]
public sealed class CNAME : DNSResponseDetail
{
	/*
            CNAME RDATA format

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                     CNAME                     /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            CNAME           A <domain-name> which specifies the canonical or primary
                            name for the owner.  The owner name is an alias.

            CNAME RRs cause no additional section processing, but name servers may
            choose to restart the query at the canonical name in certain cases.
            See RFC 1034 for details.
        */

	/// <summary>
	/// Gets or sets the canonical domain name to which the owner name is aliased.
	/// </summary>
	[DNSField]
	public DNSDomainName CName { get; set; }

	/// <summary>
	/// Returns the canonical (target) name of this CNAME record.
	/// </summary>
	/// <returns>
	/// The string representation of <see cref="CName"/>, 
	/// or an empty string if <see cref="CName"/> is <c>null</c>.
	/// </returns>
	public override string ToString() => CName.ToString();
}
