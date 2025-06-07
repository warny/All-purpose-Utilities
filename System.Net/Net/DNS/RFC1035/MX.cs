using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035
{
	/// <summary>
	/// Represents an MX (Mail Exchange) record in the DNS, as described in RFC 1035 Section 3.3.9
	/// and further clarified by RFC 974. The MX record specifies a mail server responsible
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
	/// </remarks>
[DNSRecord(DNSClass.IN, 0x0F)]
[DNSTextRecord("{Preference} {Exchange}")]
public class MX : DNSResponseDetail
	{
		/*
            MX RDATA format (RFC 1035, Section 3.3.9):

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                |                  PREFERENCE                   |
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                   EXCHANGE                    /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            PREFERENCE      A 16 bit integer that specifies the preference for
                            this mail exchange (lower = higher priority).

            EXCHANGE        A <domain-name> for the mail server receiving mail
                            on behalf of the owner name.

            MX records may trigger additional section processing for A/AAAA RRs
            associated with EXCHANGE. For details, see RFC 974 and RFC 1035.
        */

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
