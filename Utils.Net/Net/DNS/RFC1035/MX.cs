using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035
{
	/// <summary>
        /// Represents an MX (Mail Exchange) record in the DNS, as described in
        /// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.9">RFC 1035 §3.3.9</see>
        /// and further clarified by <see href="https://www.rfc-editor.org/rfc/rfc974">RFC 974</see>.
        /// The MX record specifies a mail server responsible
	/// for accepting email on behalf of a domain, along with a preference value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each MX record consists of:
	/// <list type="bullet">
	/// <item>
	/// <description>
	/// <see cref="Preference"/>: A 16-bit integer indicating priority. Lower values are
	/// preferred, meaning a mail transfer agent should try servers in ascending order
	/// of <see cref="Preference"/>.
	/// </description>
	/// </item>
	/// <item>
	/// <description>
	/// <see cref="Exchange"/>: A domain name specifying the mail exchange host
	/// (e.g., <c>mail.example.com</c>).
	/// </description>
	/// </item>
	/// </list>
	/// </para>
	/// <para>
	/// For instance, if <c>example.com</c> has two MX records, one with preference 10 and another
	/// with preference 20, the sending mail server will first attempt the host with preference 10.
	/// If that fails or is unavailable, it tries the host with preference 20.
	/// </para>
        /// <para>
        /// The DNS library may perform additional processing (e.g., retrieving the A or AAAA address
        /// of the <see cref="Exchange"/>) when resolving MX records.
        /// </para>
        /// <para>The RDATA layout defined in
        /// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.9">RFC 1035 §3.3.9</see> is:</para>
        /// <code>
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// |                  PREFERENCE                   |
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// /                   EXCHANGE                    /
        /// /                                               /
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// </code>
        /// <para>
        /// <c>PREFERENCE</c> is the priority indicator (lower values preferred) and <c>EXCHANGE</c>
        /// is a <c>&lt;domain-name&gt;</c> identifying the target mail server. Implementations often
        /// follow up with address lookups for the exchange host.
        /// </para>
        /// </remarks>
[DNSRecord(DNSClassId.IN, 0x0F)]
[DNSTextRecord("{Preference} {Exchange}")]
public class MX : DNSResponseDetail
        {
                /// <summary>
		/// Gets or sets the 16-bit preference value for this MX record. A lower value indicates
		/// a higher priority in mail routing.
		/// </summary>
		[DNSField]
		public ushort Preference { get; set; }

		/// <summary>
		/// Gets or sets the domain name of the mail exchange server (e.g. <c>mail.example.com</c>).
		/// </summary>
		[DNSField]
		public DNSDomainName Exchange { get; set; }

		/// <summary>
		/// Returns a string representation containing the mail exchange name and its preference,
		/// for example: "mail.example.com (10)".
		/// </summary>
		public override string ToString() => $"{Exchange} ({Preference})";
	}
}
