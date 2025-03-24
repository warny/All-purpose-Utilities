namespace Utils.Net.DNS.RFC1183;

/// <summary>
/// Represents an RP (Responsible Person) record, as defined in RFC 1183 Section 2.2.
/// This record identifies the mailbox of the person responsible for a particular DNS zone
/// and an optional TXT record with additional information.
/// </summary>
/// <remarks>
/// <para>
/// The RP record contains two domain names:
/// <list type="bullet">
///   <item>
///     <description>
///     <see cref="MBoxDName"/>: A domain name that specifies the mailbox for the responsible person.
///     By convention, the first label of the name is used to replace the <c>'.'</c> with <c>'@'</c>
///     to form a typical email address. For example, <c>john.doe.example.com</c> might map to <c>john.doe@example.com</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///     <see cref="TxtDName"/>: A domain name for a TXT record containing more information (e.g., about
///     the responsible person). If this name points to the root (e.g., <c>.</c>), it indicates that no
///     additional information is available.
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>
/// An example usage might be:
/// <code>
/// example.com.  86400  IN  RP  john.doe.example.com.  info.example.com.
/// </code>
/// This indicates that <c>john.doe@example.com</c> is responsible for <c>example.com</c>, and
/// <c>info.example.com</c> might hold a TXT record with further contact or host data.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x11)]
public class RP : DNSResponseDetail
{
	/*
            RP RDATA format (RFC 1183, Section 2.2):

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                 MBOXDNAME                    /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                 TXTSDNAME                    /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            MBOXDNAME: A domain name that, with its first label replaced by '@',
                       identifies the email address of the responsible person.
            TXTSDNAME: A domain name pointing to a TXT record that may contain
                       additional info. If '.' (root), then no TXT data is provided.
        */

	/// <summary>
	/// Gets or sets the domain name identifying the mailbox of the responsible person.
	/// By convention, the first label can be translated into an '@' character to form
	/// an email address, e.g. <c>john.doe.example.com</c> ? <c>john.doe@example.com</c>.
	/// </summary>
	[DNSField]
	public DNSDomainName MBoxDName { get; set; }

	/// <summary>
	/// Gets or sets the domain name that points to a TXT record holding more
	/// information about the responsible person or host. If this is set to the
	/// root label ('.'), it indicates there is no additional information.
	/// </summary>
	[DNSField]
	public DNSDomainName TxtDName { get; set; }

	/// <summary>
	/// Returns a simple string containing the mailbox domain name and TXT domain name
	/// separated by a tab. For example: "john.doe.example.com   info.example.com".
	/// </summary>
	public override string ToString() => $"{MBoxDName}\t{TxtDName}";
}
