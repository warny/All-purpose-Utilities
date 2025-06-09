using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC6844;

/// <summary>
/// Represents a CAA (Certificate Authority Authorization) record, as defined in RFC 6844.
/// A CAA record specifies which Certificate Authorities (CAs) are permitted
/// to issue certificates for the owner’s domain.
/// </summary>
/// <remarks>
/// The on-wire format for the RDATA includes:
/// <list type="bullet">
///   <item><description>Flags (1 octet)</description></item>
///   <item><description>Tag length (1 octet)</description></item>
///   <item><description>Tag (Tag length bytes)</description></item>
///   <item><description>Value (the remainder of the RDATA)</description></item>
/// </list>
/// Example usage might be <c>0 issue "letsencrypt.org"</c> meaning that only
/// Let's Encrypt is authorized to issue certificates for this domain.
/// </remarks>
[DNSRecord(DNSClassId.IN, 257, "CAA")]
[DNSTextRecord("{Flags} {Tag} {Value}")]
public class CAA : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the 1-octet flags field. Typically zero for standard usage;
	/// certain bits may be used for future extensions (e.g., the critical bit).
	/// </summary>
	[DNSField]
	public byte Flags { get; set; }

	/// <summary>
	/// Gets or sets the Tag portion. The tag has a 1-byte length prefix in the DNS RDATA.
	/// Common tags include "issue", "issuewild", or "iodef".
	/// </summary>
	[DNSField(FieldsSizeOptions.PrefixedSize1B)]
	public string Tag { get; set; }

	/// <summary>
	/// Gets or sets the Value portion, containing the CA or policy data.
	/// E.g., "letsencrypt.org" or "mailto:admin@example.com" for iodef reports.
	/// How your reflection logic handles leftover RDATA might need customization here.
	/// </summary>
	[DNSField]
	public string Value { get; set; }
}
