using System;
using System.Linq;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents a fallback or "default" DNS record for scenarios where the record type is unrecognized,
/// or when a custom processing path is desired for type code zero (which is reserved/unused).
/// Despite the TXT-like comment, this class is annotated with <c>[DNSRecord(DNSClass.IN, 0x00)]</c>,
/// indicating it is treated as a placeholder for type <c>0</c>. The raw payload is stored in the
/// <see cref="Datas"/> property.
/// </summary>
/// <remarks>
/// <para>
/// In standard DNS, record type <c>0</c> is undefined/reserved. This class may serve as a fallback
/// for unrecognized record types or experimental usage. If you intend to store actual TXT records,
/// consider using the official TXT record type code (<c>0x10</c>, decimal 16) instead.
/// </para>
/// <para>The illustrative RDATA layout is:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                   DATAS                       /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para>
/// The payload is interpreted as raw bytes. TXT records normally use type code 16 (0x10), but this
/// fallback keeps the unparsed data accessible for experimentation.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x00)]
[DNSTextRecord("{Datas}")]
public class Default : DNSResponseDetail
{
    /// <summary>
    /// Gets or sets the raw data payload associated with this fallback record.
    /// </summary>
    [DNSField]
    public byte[] Datas { get; set; }

    /// <summary>
    /// Returns a textual representation of the bytes stored in <see cref="Datas"/>.
    /// </summary>
    /// <returns>
    /// A string that lists each byte as a hexadecimal value enclosed in brackets. The caller must
    /// ensure <see cref="Datas"/> is not <c>null</c> before calling this method.
    /// </returns>
    public override string ToString() => "[ " + string.Join(" ", Datas.Select(x => x.ToString("X2"))) + " ]" ?? string.Empty;
}
