using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC3123;

/// <summary>
/// Represents an APL (Address Prefix List) record as defined in RFC 3123.
/// An APL record is used to specify a list of address prefixes along with a negation flag,
/// indicating which address ranges are included or excluded.
/// </summary>
/// <remarks>
/// <para>
/// The RDATA section of an APL record consists of zero or more items (<c>&lt;apitem&gt;</c>) each of the form:
/// </para>
/// <code>
///  +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
///  |                          ADDRESSFAMILY                        |
///  +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
///  |             PREFIX            | N |         AFDLENGTH         |
///  +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
///  /                            AFDPART                            /
///  |                                                               |
///  +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
/// </code>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>ADDRESSFAMILY</b>: a 16-bit unsigned value as assigned by IANA.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>PREFIX</b>: an 8-bit unsigned binary coded prefix length. Its interpretation is address-family specific.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>N</b>: a 1-bit negation flag, indicating if the "!" character was present in the textual format.
///       A value of 1 means the prefix is negated, and 0 means it is not.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>AFDLENGTH</b>: a 7-bit unsigned integer representing the length in octets of the following AFDPART.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>AFDPART</b>: the address family dependent part, whose length is specified by AFDLENGTH.
///     </description>
///   </item>
/// </list>
/// <para>
/// This class encapsulates a single APL item. In a full APL record, there may be zero or more such items.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x2A)]
[DNSTextRecord("{AddressFamily} {Prefix} {flagAndAfdLength} {AfdPart}")]
public class APL : DNSResponseDetail
{
	/*
           The RDATA section of an APL record consists of zero or more items (<apitem>) of the
           form

              +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
              |                          ADDRESSFAMILY                        |
              +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
              |             PREFIX            | N |         AFDLENGTH         |
              +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
              /                            AFDPART                            /
              |                                                               |
              +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+

              ADDRESSFAMILY     16 bit unsigned value as assigned by IANA
                                (see IANA Considerations)
              PREFIX            8 bit unsigned binary coded prefix length.
                                Upper and lower bounds and interpretation of
                                this value are address family specific.
              N                 negation flag, indicates the presence of the
                                "!" character in the textual format.  It has
                                the value "1" if the "!" was given, "0" else.
              AFDLENGTH         length in octets of the following address
                                family dependent part (7 bit unsigned).
              AFDPART           address family dependent part.  See below.

           This document defines the AFDPARTs for address families 1 (IPv4) and
           2 (IPv6).  Future revisions may deal with additional address families.
        */

	/// <summary>
	/// Gets or sets the address family for this APL item.
	/// This is a 16-bit unsigned value (as per <see cref="IANAddressFamily"/>)
	/// assigned by IANA.
	/// </summary>
	[DNSField]
	public IANAddressFamily AddressFamily { get; set; }

	/// <summary>
	/// Gets or sets the prefix length (in bits) for this APL item.
	/// The prefix indicates how many of the high-order bits of the address are significant.
	/// </summary>
	[DNSField]
	public byte Prefix { get; set; }

	/// <summary>
	/// Composite field that stores both the negation flag and the AFDLENGTH.
	/// The most significant bit (bit 7) is used for the negation flag (N), and the
	/// remaining 7 bits represent the AFDLENGTH (the length of the AFDPART).
	/// </summary>
	[DNSField]
	private byte flagAndAfdLength;

	/// <summary>
	/// Gets or sets the AFDLENGTH, which is the length in octets of the address family dependent part (AFDPART).
	/// This value is extracted from the lower 7 bits of <see cref="flagAndAfdLength"/>.
	/// </summary>
	private byte AfdLength
	{
		get => (byte)(flagAndAfdLength & 0b0111_1111);
		set => flagAndAfdLength = (byte)((value & 0b0111_1111) | (flagAndAfdLength & 0b1000_0000));
	}

	/// <summary>
	/// Gets or sets a value indicating whether the negation flag is set.
	/// The negation flag (N) is stored in the most significant bit of <see cref="flagAndAfdLength"/>.
	/// If set to true, the prefix is negated (i.e., excluded).
	/// </summary>
	public bool Negate
	{
		get => (flagAndAfdLength & 0b1000_0000) != 0;
		set => flagAndAfdLength = (byte)((value ? 0b1000_0000 : 0) | (flagAndAfdLength & 0b0111_1111));
	}

	/// <summary>
	/// Gets or sets the address family dependent part (AFDPART) as a byte array.
	/// Its length must match the value specified by <see cref="AfdLength"/>.
	/// </summary>
	[DNSField(nameof(AfdLength))]
	public byte[] AfdPart { get; set; }
}
