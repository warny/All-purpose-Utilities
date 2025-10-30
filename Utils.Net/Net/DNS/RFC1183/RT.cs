using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1183;

/// <summary>
/// Represents an RT (Route Through) record, as defined in
/// <see href="https://www.rfc-editor.org/rfc/rfc1183#section-2.3">RFC 1183 §2.3</see>.
/// The RT record specifies an intermediate host that should be used when
/// routing to the owner of the record, along with a preference value.
/// </summary>
/// <remarks>
/// <para>
/// The RT record includes:
/// <list type="bullet">
///   <item>
///     <description>
///     <see cref="Preference"/>: A 16-bit integer specifying the relative
///     preference of this route. Lower values indicate more preferred routes.
///     </description>
///   </item>
///   <item>
///     <description>
///     <see cref="DnsName"/>: The domain name of the intermediate host or router
///     that should be used for routing to the owner name of this record.
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>
/// Example usage might look like:
/// <code>
/// example.com.  IN  RT  10  gateway.example.net.
/// </code>
/// indicating that traffic to <c>example.com</c> should be routed through
/// <c>gateway.example.net</c> with preference 10. Lower <see cref="Preference"/>
/// values mean higher priority.
/// </para>
/// <para>
/// The RT record does not trigger additional section processing (no extra
/// A/AAAA records included by default). In modern networking, explicit routing
/// via DNS is rarely practiced, and the RT record is largely historical.
/// </para>
/// <para>The wire layout is:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                  PREFERENCE                   |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                   EXCHANGER                   /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x15)]
[DNSTextRecord("{Preference} {DnsName}")]
public class RT : DNSResponseDetail
{


    /// <summary>
    /// Gets or sets the 16-bit preference value for this RT record.
    /// Lower numbers indicate a more preferred route.
    /// </summary>
    [DNSField]
    public ushort Preference { get; set; }

    /// <summary>
    /// Gets or sets the domain name of the intermediate host for routing
    /// traffic to the owner of this RT record.
    /// </summary>
    [DNSField]
    public DNSDomainName DnsName { get; set; }

    /// <summary>
    /// Returns a string showing the preference and domain name, separated by a tab or space.
    /// For example: "10   gateway.example.net".
    /// </summary>
    public override string ToString() => $"{Preference}\t{DnsName}";
}
