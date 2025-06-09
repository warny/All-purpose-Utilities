using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC7553;

/// <summary>
/// Represents a URI (Uniform Resource Identifier) record as defined in RFC 7553.
/// A URI record allows a domain to publish one or more URIs, potentially
/// with priority/weight for load balancing or backup links.
/// </summary>
/// <remarks>
/// The on-wire RDATA format is:
/// <list type="number">
///   <item><description>Priority (16 bits, network order)</description></item>
///   <item><description>Weight (16 bits, network order)</description></item>
///   <item><description>Target (variable-length string, typically UTF-8)</description></item>
/// </list>
/// Example:
/// <code>
/// example.com.  3600  IN  URI  10  1  "https://www.example.com"
/// </code>
/// </remarks>
[DNSRecord(DNSClassId.IN, 256, "URI")]
[DNSTextRecord("{Priority} {Weight} {Target}")]
public class URI : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the 16-bit priority. Lower values indicate higher
	/// preference among multiple URI records at the same owner.
	/// </summary>
	[DNSField]
	public ushort Priority { get; set; }

	/// <summary>
	/// Gets or sets the 16-bit weight, used to determine relative proportions
	/// of traffic for records with the same priority.
	/// </summary>
	[DNSField]
	public ushort Weight { get; set; }

	/// <summary>
	/// Gets or sets the target URI as a string. Typically encoded in UTF-8.
	/// There's no explicit length prefix in the spec, so your reflection approach
	/// may handle this as leftover RDATA.
	/// </summary>
	[DNSField]
	public string Target { get; set; }
}
