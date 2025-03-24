using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC2671;

/// <summary>
/// Represents an OPT (Option) record as defined in RFC 2671.
/// OPT records are used to convey additional options and information in DNS messages,
/// most notably as part of the EDNS (Extension mechanisms for DNS) protocol.
/// </summary>
/// <remarks>
/// <para>
/// The RDATA section of an OPT record is structured as follows:
/// </para>
/// <code>
///                         +0 (MSB)                            +1 (LSB)
///             +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
///          0: |                          OPTION-CODE                          |
///             +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
///          2: |                         OPTION-LENGTH                         |
///             +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
///          4: |                                                               |
///             /                          OPTION-DATA                          /
///             /                                                               /
///             +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
/// </code>
/// <para>
/// Where:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>OPTION-CODE</b>: A 16-bit field assigned by IANA that identifies the specific option.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>OPTION-LENGTH</b>: A 16-bit value indicating the size (in octets) of the OPTION-DATA.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>OPTION-DATA</b>: Variable-length data whose format depends on the OPTION-CODE.
///     </description>
///   </item>
/// </list>
/// <para>
/// OPT records are typically used as part of EDNS to extend the size and functionality of DNS messages.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x29)]
public class OPT : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the option code. This 16-bit field is assigned by IANA and identifies
	/// the specific option contained in the OPT record.
	/// </summary>
	[DNSField]
	public ushort OptionCode { get; set; }

	/// <summary>
	/// Gets or sets the option data. This field contains the data for the option,
	/// with its length specified by a 2-byte prefix during serialization.
	/// The format of this data depends on the OPTION-CODE.
	/// </summary>
	[DNSField(FieldsSizeOptions.PrefixedSize2B)]
	public byte[] OptionsData { get; set; }
}
