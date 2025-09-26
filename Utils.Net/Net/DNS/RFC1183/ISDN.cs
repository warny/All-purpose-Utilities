using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1183
{
	/// <summary>
        /// Represents an ISDN (Integrated Services Digital Network) DNS record,
        /// as originally defined in <see href="https://www.rfc-editor.org/rfc/rfc1183#section-3.2">RFC 1183 §3.2</see>. This record stores
	/// an ISDN telephone number (and optionally a subaddress).
	/// </summary>
	/// <remarks>
	/// <para>
        /// According to <see href="https://www.rfc-editor.org/rfc/rfc1183#section-3.2">RFC 1183 §3.2</see>, an ISDN record can contain:
	/// <list type="bullet">
	/// <item><description>The ISDN phone number, in a digit string.</description></item>
	/// <item><description>An optional subaddress.</description></item>
	/// </list>
	/// However, this class currently only stores a single <see cref="PhoneNumber"/> field,
	/// and does not parse or serialize a subaddress if present. Consequently, it may be
	/// incomplete for scenarios requiring a subaddress or more detailed data.
	/// </para>
        /// <para>
        /// In modern DNS usage, ISDN records are virtually never used, and the specification
        /// in <see href="https://www.rfc-editor.org/rfc/rfc1183">RFC 1183</see> is largely historical. Consult the RFC if you need a more comprehensive
        /// representation (including subaddresses).
        /// </para>
        /// <para>The record may store two character-strings: the ISDN number and an optional subaddress.</para>
        /// <code>
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// /                 &lt;ISDN-number&gt;                /
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// /               &lt;subaddress&gt; (optional)        /
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// </code>
        /// </remarks>
[DNSRecord(DNSClassId.IN, 0x14)]
[DNSTextRecord("{PhoneNumber}")]
public class ISDN : DNSResponseDetail
	{
		

		/// <summary>
		/// Gets or sets the ISDN phone number. This record class does not handle
		/// any subaddress portion or other additional data fields.
		/// </summary>
		/// <remarks>
		/// Since the original RFC 1183 format allows two distinct strings (ISDN number
		/// and optional subaddress), usage requiring a subaddress is not fully covered.
		/// </remarks>
		[DNSField]
		public string PhoneNumber { get; set; }

		/// <summary>
		/// Returns the phone number as a string. Note that any subaddress is not
		/// supported in this minimal implementation.
		/// </summary>
		public override string ToString() => PhoneNumber ?? string.Empty;
	}
}
