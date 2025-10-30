using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC2230;

/// <summary>
/// Represents a KX (Key Exchange) record in DNS as defined by RFC 2230.
/// A KX record specifies a host that is willing to act as a mail exchange for the owner name,
/// along with a preference value to indicate priority when multiple KX records are present.
/// </summary>
/// <remarks>
/// <para>
/// The KX record RDATA format is defined as follows:
/// </para>
/// <code>
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
///   |                  PREFERENCE                   |
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
///   /                   EXCHANGER                   /
///   /                                               /
///   +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para>
/// The fields are defined as:
/// <list type="bullet">
///   <item>
///     <description><b>PREFERENCE</b>: A 16-bit non-negative integer indicating
///     the relative priority of this record among others for the same owner.
///     Lower values indicate a higher priority.</description>
///   </item>
///   <item>
///     <description><b>EXCHANGER</b>: A domain name that specifies a host willing to
///     act as a mail exchange for the owner name. This field is not compressed.</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// When processing a KX record, DNS resolvers must include an additional lookup for the
/// A record corresponding to the <b>EXCHANGER</b> field. If IPv6 is supported, the resolver
/// should also look up the AAAA record for the exchanger.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x24)]
[DNSTextRecord("{Preference} {Exchanger}")]
public class KX : DNSResponseDetail
{
    /// <summary>
    /// Gets or sets the 16-bit preference value for this KX record.
    /// Lower values indicate a higher preference when multiple KX records are present.
    /// </summary>
    [DNSField]
    public ushort Preference { get; set; }

    /// <summary>
    /// Gets or sets the domain name of the host that is willing to act as a mail exchange.
    /// This field specifies the target host for additional A (and AAAA) record lookups.
    /// </summary>
    [DNSField]
    public DNSDomainName Exchanger { get; set; }
}
