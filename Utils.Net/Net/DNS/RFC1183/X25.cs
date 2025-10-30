using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1183;

/// <summary>
/// Represents an X.25 PSDN address record, as defined in
/// <see href="https://www.rfc-editor.org/rfc/rfc1183#section-3.1">RFC 1183 §3.1</see> (type code 19).
/// This record stores a single X.25 PSDN address (e.g., a numeric string like "311061700956").
/// </summary>
/// <remarks>
/// <para>
/// In <see href="https://www.rfc-editor.org/rfc/rfc1183#section-3.1">RFC 1183 §3.1</see>, the X25 record is used to associate a domain name with an X.25
/// Public Switched Data Network (PSDN) address. Typically, this is just a single
/// string representing the X.25 address. 
/// </para>
/// <para>
/// Although X.25 was historically important, modern DNS usage rarely includes X25 RRs.
/// This implementation only holds a single string for the PSDN, and does not attempt
/// to parse any optional subaddress or additional fields.
/// </para>
/// <para>The wire format stores one character-string:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                   PSDN                      /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x13)]
[DNSTextRecord("{PSDN}")]
public class X25 : DNSResponseDetail
{


    /// <summary>
    /// Gets or sets the PSDN (Public Switched Data Network) address as a string.
    /// Example: "311061700956".
    /// </summary>
    [DNSField]
    public string PSDN { get; set; }

    /// <summary>
    /// Returns the PSDN address as a string, or an empty string if <see cref="PSDN"/> is null.
    /// </summary>
    public override string ToString() => PSDN ?? string.Empty;
}
