using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents an HINFO (Host Information) record, as defined in RFC 1035.
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
/// See RFC 1010 for standard values of CPU and OS.
/// </remarks>
[DNSRecord(DNSClass.IN, 0x0D)]
[DNSTextRecord("{Info}")]
public class HINFO : DNSResponseDetail
{
	/*
            HINFO RDATA format

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                      CPU                      /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                       OS                      /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            CPU: A <character-string> specifying the CPU type.
            OS:  A <character-string> specifying the operating system type.

            HINFO records are used to acquire general information about a host. The main use
            is for protocols such as FTP that can use special procedures when interacting
            between similar machines or operating systems.
        */

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
