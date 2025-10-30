using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1183;

/// <summary>
/// Represents an AFSDB (Andrew File System Database) record, as defined in
/// <see href="https://www.rfc-editor.org/rfc/rfc1183#section-1.2">RFC 1183 §1.2</see>.
/// This record helps locate AFS or related database servers for a particular domain.
/// </summary>
/// <remarks>
/// <para>
/// The AFSDB record indicates:
/// <list type="bullet">
///   <item><description><see cref="Preference"/>: A 16-bit integer specifying the priority 
///   (lower values are more preferred).</description></item>
///   <item><description><see cref="AFSServer"/>: The domain name of a host that provides 
///   AFS services for the owner name.</description></item>
/// </list>
/// </para>
/// <para>
/// A typical usage might look like:
/// <code>
/// example.com.  3600  IN  AFSDB  1  afsdbserver.example.com.
/// </code>
/// This means that the domain <c>example.com</c> has an AFSDB record pointing
/// to <c>afsdbserver.example.com</c> with priority 1 (preferred over higher numbers).
/// </para>
/// <para>
/// The AFSDB record can also be used for other similar database services, depending on
/// the subtype indicated by the <see cref="Preference"/> field. See
/// <see href="https://www.rfc-editor.org/rfc/rfc1183">RFC 1183</see> and related
/// documents for more details on AFS usage and potential multi-server configurations.
/// </para>
/// <para>The wire format contains the preference followed by the server name:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                  PREFERENCE                   |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                    SERVER                     /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x12)]
[DNSTextRecord("{Preference} {AFSServer}")]
public class AFSDB : DNSResponseDetail
{


    /// <summary>
    /// Gets or sets the 16-bit preference value for this AFSDB record. A lower number indicates
    /// a higher priority among multiple AFSDB records at the same owner name.
    /// </summary>
    [DNSField]
    public ushort Preference { get; set; }

    /// <summary>
    /// Gets or sets the domain name of the AFS (Andrew File System) server.
    /// </summary>
    [DNSField]
    public DNSDomainName AFSServer { get; set; }

    /// <summary>
    /// Returns a string showing the server name followed by the preference in parentheses,
    /// e.g. "afsserver.example.com (10)".
    /// </summary>
    public override string ToString() => $"{AFSServer} ({Preference})";
}
