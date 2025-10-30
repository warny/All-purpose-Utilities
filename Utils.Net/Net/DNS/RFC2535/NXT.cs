namespace Utils.Net.DNS.RFC2535;

/// <summary>
/// Represents an NXT (Next) record in DNS as specified by
/// <see href="https://www.rfc-editor.org/rfc/rfc2535#section-5.1">RFC 2535 §5.1</see>.
/// </summary>
/// <remarks>
/// <para>
/// The NXT record is used in DNSSEC to prove the non-existence of certain names or types
/// in a zone. It provides a "next domain name" which, together with a type bitmap, defines
/// an interval in the canonical ordering of names in the zone. Any name not covered by an NXT
/// record is guaranteed not to exist in the zone.
/// </para>
/// <para>
/// The NXT record RDATA consists of:
/// <list type="bullet">
///   <item>
///     <description>
///       <b>NextDomainName</b>: A domain name which is the next name in the zone's canonical order.
///       If the zone is considered circular, the last NXT record will point back to the zone apex.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>TypeBitMap</b>: A variable-length bitmap where each bit corresponds to a DNS RR type.
///       A bit set to 1 indicates that at least one RR of that type exists for the owner name.
///       The bit for type 0 is always 0 because type 0 is not used. The bitmap must be interpreted
///       according to the rules in <see href="https://www.rfc-editor.org/rfc/rfc2535#section-5.2">RFC 2535 §5.2</see>.
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>The wire format is:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                  Next Domain Name              /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                    Type Bit Map                /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para>
/// In a secure DNS response, an NXT record (and its associated signature) is included in the
/// authority section to indicate that no records exist in the gap between the owner name of the
/// NXT and the <b>NextDomainName</b> in canonical order. This mechanism is part of the DNSSEC
/// protocol for securely denying the existence of domain names or RR types.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x1E)]
[DNSTextRecord("{NextDomainName} {TypeBitMap}")]
public class NXT : DNSResponseDetail
{

    /// <summary>
    /// Gets or sets the next domain name in the zone's canonical order.
    /// This field is used to define the interval in which no RR exists.
    /// </summary>
    [DNSField]
    public DNSDomainName NextDomainName { get; set; }

    /// <summary>
    /// Gets or sets the type bitmap which indicates the RR types present for the owner name.
    /// Each bit corresponds to an RR type; a set bit means that at least one RR of that type exists.
    /// Trailing zero octets are prohibited, and the bitmap must be interpreted in accordance
    /// with <see href="https://www.rfc-editor.org/rfc/rfc2535#section-5.2">RFC 2535 §5.2</see>.
    /// </summary>
    [DNSField]
    public byte[] TypeBitMap { get; set; }
}