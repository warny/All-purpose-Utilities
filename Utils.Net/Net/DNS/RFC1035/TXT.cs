using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents a TXT record in DNS, as defined in RFC 1035 Section 3.3.14.
/// TXT records store arbitrary human-readable text, often used for descriptive
/// or application-specific data (e.g., SPF, DKIM, etc. in modern usage).
/// </summary>
/// <remarks>
/// <para>
/// A TXT record can contain one or more character-string segments,
/// which can be combined into a single textual representation.
/// In practice, many DNS libraries and servers treat TXT data as either
/// a concatenated string or a list of separate segments.
/// </para>
/// <para>
/// For example:
/// <code>
/// example.com.  3600  IN  TXT  "Some descriptive text here"
/// </code>
/// The <c>Text</c> property would hold the value <c>"Some descriptive text here"</c>.
/// </para>
/// <para>
/// Although RFC 1035 states "one or more &lt;character-string&gt;s," many modern implementations
/// treat this as a single text block. To conform to multi-segment usage, your library might need
/// additional logic for splitting or concatenating strings if required by a specific environment.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x10)]
[DNSTextRecord("{Text}")]
public class TXT : DNSResponseDetail
{
	/*
        TXT RDATA format (RFC 1035, Section 3.3.14):

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                   TXT-DATA                    /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        TXT-DATA may contain one or more <character-string> fields.
        In modern usage, it often holds simple ASCII text,
        but can be used for various key-value data as well.
    */

	/// <summary>
	/// Gets or sets the text content of this TXT record.
	/// In many DNS scenarios, this is either a single string or
	/// a concatenation of multiple character-string segments.
	/// </summary>
	[DNSField]
	public string Text { get; set; }

	/// <summary>
	/// Returns the string content of the TXT record.
	/// </summary>
	public override string ToString() => Text;
}
