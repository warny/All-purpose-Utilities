using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035
{
	/// <summary>
        /// Represents a MINFO record, considered experimental and largely obsolete as per
        /// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.7">RFC 1035 §3.3.7</see>.
	/// The MINFO record provides mail-related information (responsible mailbox, error mailbox)
	/// for a mailing list or mailbox owner.
	/// </summary>
        /// <remarks>
        /// <para>
        /// In modern DNS usage, <c>MINFO</c> has been superseded by <c>MX</c> records and other mechanisms.
        /// It is marked with <see cref="ObsoleteAttribute"/> to indicate it should generally not be
        /// used in production.
        /// </para>
        /// <para>
        /// <strong>RMAILBX</strong> (<see cref="RMailBx"/>) is the mailbox responsible for the mailing list or
        /// mailbox. <strong>EMAILBX</strong> (<see cref="EMailBx"/>) is the mailbox for error messages related
        /// to that list or mailbox. Each is stored as a <see cref="DNSDomainName"/> struct, which cannot be null,
        /// but can be an empty or default domain name.
        /// </para>
        /// <para>The RDATA format consists of two domain names:</para>
        /// <code>
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// /                    RMAILBX                    /
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// /                    EMAILBX                    /
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// </code>
        /// <para>Both fields are <c>&lt;domain-name&gt;</c> values identifying responsible and error mailboxes.</para>
        /// </remarks>
[DNSRecord(DNSClassId.IN, 0x0E)]
[DNSTextRecord("{RMailBx} {EMailBx}")]
[Obsolete("MINFO (mail info) records are obsolete; use MX records instead.")]
public class MINFO : DNSResponseDetail
        {
		/// <summary>
		/// Gets or sets the <see cref="DNSDomainName"/> of the mailbox that will receive error messages (EMAILBX).
		/// </summary>
		[DNSField]
		public DNSDomainName EMailBx { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="DNSDomainName"/> of the mailbox responsible for the mailing list or mailbox (RMAILBX).
		/// </summary>
		[DNSField]
		public DNSDomainName RMailBx { get; set; }

		/// <summary>
		/// Returns a formatted string containing the EMAILBX and RMAILBX values. Because
		/// <see cref="DNSDomainName"/> is a struct, these fields can never be null, but may be empty.
		/// </summary>
		public override string ToString()
		{
			return $"EMAILBX:\t{EMailBx}\nRMAILBX:\t{RMailBx}";
		}
	}
}
