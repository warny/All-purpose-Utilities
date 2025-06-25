using System;
using System.Linq;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents a fallback or "default" DNS record for scenarios where the record type is unrecognized,
/// or when a custom processing path is desired for type code zero (which is reserved/unused).
/// Despite the TXT-like comment, this class is annotated with <c>[DNSRecord(DNSClass.IN, 0x00)]</c>,
/// indicating it is treated as a placeholder for type <c>0</c>. It can store textual data via the
/// <see cref="Text"/> property.
/// </summary>
/// <remarks>
/// <para>
/// In standard DNS, record type <c>0</c> is undefined/reserved. This class may serve as a fallback
/// for unrecognized record types or experimental usage. If you intend to store actual TXT records,
/// consider using the official TXT record type code (<c>0x10</c>, decimal 16) instead.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x00)]
[DNSTextRecord("{Datas}")]
public class Default : DNSResponseDetail
{
	/*
            Example of a bytes RDATA format:

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                   DATAS                       /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            bytes    One or more bytes

            Typically, TXT RRs have the type code 0x10 (16 decimal). However, this
            class uses type code 0 for demonstration or fallback.
        */

	/// <summary>
	/// Gets or sets an arbitrary string of text for this fallback record.
	/// </summary>
	[DNSField]
	public byte[] Datas { get; set; }

	/// <summary>
	/// Returns the text content of this fallback record.
	/// </summary>
	/// <returns>
	/// The value of the <see cref="Text"/> property, or an empty string if it is <c>null</c>.
	/// </returns>
	public override string ToString() => "[ " + string.Join(" ", Datas.Select(x=>x.ToString("X2"))) + " ]" ?? string.Empty;
}
