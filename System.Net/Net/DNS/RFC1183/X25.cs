using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1183;

/// <summary>
/// Represents an X.25 PSDN address record, as defined in RFC 1183 Section 3.1 (type code 19).
/// This record stores a single X.25 PSDN address (e.g., a numeric string like "311061700956").
/// </summary>
/// <remarks>
/// <para>
/// In RFC 1183, the X25 record is used to associate a domain name with an X.25
/// Public Switched Data Network (PSDN) address. Typically, this is just a single
/// string representing the X.25 address. 
/// </para>
/// <para>
/// Although X.25 was historically important, modern DNS usage rarely includes X25 RRs.
/// This implementation only holds a single string for the PSDN, and does not attempt
/// to parse any optional subaddress or additional fields.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x13)]
public class X25 : DNSResponseDetail
{
	/*
        X25 RDATA format (RFC 1183, Section 3.1):

        +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /                   PSDN                      /
        +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        where "PSDN" is a single character-string specifying the
        X.25 PSDN address, e.g., "311061700956".
    */

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
