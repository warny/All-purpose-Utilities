using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC2915;

/// <summary>
/// Represents a NAPTR (Naming Authority Pointer) record in DNS, as defined in RFC 2915.
/// NAPTR records are used for flexible rewriting of domain names and are commonly employed
/// in applications such as ENUM, VoIP, and Internet telephony to map telephone numbers to URIs.
/// </summary>
/// <remarks>
/// <para>
/// The NAPTR record RDATA format is as follows:
/// </para>
/// <code>
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
///   |                   ORDER                       |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
///   |                 PREFERENCE                    |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
///   |     FLAGS      |     SERVICE    |    REGEXP   |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
///   |                   REPLACEMENT                 |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para>
/// Where:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>ORDER</b> is a 16-bit integer that specifies the order in which the NAPTR records
///       must be processed; lower values are processed first.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>PREFERENCE</b> is a 16-bit integer that specifies the preference among NAPTR records
///       with equal order; lower values are preferred.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>FLAGS</b> is a string (with one-byte length prefix) that provides additional instructions
///       on how the NAPTR record should be processed (e.g., "U" for URI, "S" for SIPS).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>SERVICE</b> is a string (with one-byte length prefix) that indicates the service and protocol,
///       for example "E2U+sip" for a SIP URI.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>REGEXP</b> is a string (with one-byte length prefix) that contains a regular expression used
///       to rewrite the original domain name.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>REPLACEMENT</b> is a string (with one-byte length prefix) that specifies the new domain name to be
///       used if the regular expression does not result in a usable URI.
///     </description>
///   </item>
/// </list>
/// <para>
/// The NAPTR record provides a flexible mechanism for service discovery and URI rewriting in a DNS namespace.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x23)]
[DNSTextRecord("{Order} {Preference} {Flags} {Service} {Regexp} {Replacement}")]
public class NAPTR : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the 16-bit ORDER field. Lower values indicate higher processing precedence.
	/// </summary>
	[DNSField]
	public ushort Order { get; set; }

	/// <summary>
	/// Gets or sets the 16-bit PREFERENCE field, used to select between multiple NAPTR records
	/// with the same order value. Lower values are preferred.
	/// </summary>
	[DNSField]
	public ushort Preference { get; set; }

	/// <summary>
	/// Gets or sets the FLAGS string which provides instructions on how the record should be interpreted.
	/// This field is stored with a one-byte length prefix.
	/// </summary>
	[DNSField(FieldsSizeOptions.PrefixedSize1B)]
	public string Flags { get; set; }

	/// <summary>
	/// Gets or sets the SERVICE string, which indicates the service and protocol used.
	/// This field is stored with a one-byte length prefix.
	/// </summary>
	[DNSField(FieldsSizeOptions.PrefixedSize1B)]
	public string Service { get; set; }

	/// <summary>
	/// Gets or sets the REGEXP string that contains a regular expression for rewriting the
	/// domain name. This field is stored with a one-byte length prefix.
	/// </summary>
	[DNSField(FieldsSizeOptions.PrefixedSize1B)]
	public string Regexp { get; set; }

	/// <summary>
	/// Gets or sets the REPLACEMENT string which specifies the new domain name if the REGEXP does
	/// not result in a usable URI. This field is stored with a one-byte length prefix.
	/// </summary>
	[DNSField(FieldsSizeOptions.PrefixedSize1B)]
	public string Replacement { get; set; }

	/// <summary>
	/// Returns a string representation of the NAPTR record,
	/// concatenating all its fields.
	/// </summary>
	public override string ToString()
	{
		return $"Order: {Order}, Preference: {Preference}, Flags: {Flags}, Service: {Service}, Regexp: {Regexp}, Replacement: {Replacement}";
	}
}
