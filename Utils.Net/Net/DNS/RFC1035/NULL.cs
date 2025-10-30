using System.Linq;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents a NULL (experimental) DNS record as described in
/// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.10">RFC 1035 §3.3.10</see>.
/// This record type can hold arbitrary data (up to 65535 bytes) and is primarily
/// a placeholder for experimental or interim purposes.
/// </summary>
/// <remarks>
/// <para>
/// A NULL record is an experimental record type (code 0x0A) that carries opaque
/// binary data in its RDATA field (<see cref="Datas"/>). This data may be up
/// to 65535 bytes in length.
/// </para>
/// <para>
/// Since the NULL RR is not intended for standard DNS usage, most resolvers
/// will ignore or discard it unless specifically designed to handle custom
/// data in DNS. Use of this record for production services is discouraged.
/// </para>
/// <para>The on-wire format simply stores arbitrary bytes:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                  &lt;anything&gt;                   /
/// /                                               /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para>
/// The payload has no defined semantics; consumers must agree on meaning out-of-band.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x0A)]
[DNSTextRecord("{Datas}")]
public class NULL : DNSResponseDetail
{
    /// <summary>
    /// Gets or sets the raw binary data for this NULL record.
    /// May be up to 65535 bytes in length.
    /// </summary>
    [DNSField]
    public byte[] Datas { get; set; }

    /// <summary>
    /// Returns a string consisting of the bytes in hexadecimal form,
    /// for example: "{ 54 78 1E 3A ... }".
    /// </summary>
    public override string ToString() =>
        "{ " + string.Join(" ", Datas.Select(d => d.ToString("X2"))) + " }";
}
