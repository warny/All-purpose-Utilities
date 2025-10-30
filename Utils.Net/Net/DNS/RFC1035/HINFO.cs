using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents an HINFO (Host Information) record, as defined in
/// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.2">RFC 1035 §3.3.2</see>.
/// </summary>
/// <remarks>
/// The HINFO record provides host-specific data about the CPU architecture and operating system.
/// Traditionally, this is stored as two separate character strings (CPU and OS). However, this
/// example consolidates both into a single <see cref="Info"/> field. If you prefer to store
/// CPU and OS separately, you can replace <c>Info</c> with two string fields annotated with
/// <see cref="DNSFieldAttribute"/>.
/// <para>
/// Example (traditional two-field approach):
/// <code>
/// [DNSField] public string CPU { get; set; }
/// [DNSField] public string OS  { get; set; }
/// </code>
/// </para>
/// <para>The wire-format RDATA (<see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.2">RFC 1035 §3.3.2</see>) is:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                      CPU                      /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                       OS                      /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para>
/// Each field is a <c>&lt;character-string&gt;</c>. <c>CPU</c> describes the processor type,
/// and <c>OS</c> identifies the operating system. See
/// <see href="https://www.rfc-editor.org/rfc/rfc1010">RFC 1010</see> for common values.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x0D)]
[DNSTextRecord("{Info}")]
public class HINFO : DNSResponseDetail
{
    /// <summary>
    /// Gets or sets a string containing CPU and OS information. This example uses a single
    /// field, but you may split it into two fields if desired (CPU and OS).
    /// </summary>
    [DNSField]
    public string Info { get; set; }

    /// <summary>
    /// Returns the entire stored text in <see cref="Info"/>.
    /// </summary>
    public override string ToString() => Info;
}
