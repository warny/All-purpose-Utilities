namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents a DNS CNAME (Canonical Name) record, used to alias one domain name to another,
/// as defined in <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.1">RFC 1035 §3.3.1</see>.
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
/// in <see href="https://www.rfc-editor.org/rfc/rfc1034#section-3.6.2">RFC 1034 §3.6.2</see>.
/// </para>
/// <para>
/// This class corresponds to a DNS record with type code <c>0x05</c> and DNS class <see cref="DNSClassId.IN"/>.
/// </para>
/// <para>The RDATA layout is simply the canonical domain name:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                     CNAME                     /
/// /                                               /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para>The <c>CNAME</c> value is a <c>&lt;domain-name&gt;</c> specifying the canonical target.</para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x05)]
[DNSTextRecord("{CName}")]
public sealed class CNAME : DNSResponseDetail
{
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
