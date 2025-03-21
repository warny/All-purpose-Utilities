namespace Utils.Net.DNS.RFC2052;

/// <summary>
/// Represents an SRV (Service) record, which specifies the location of servers
/// for a particular service and protocol within a domain. SRV records are
/// defined in RFC 2782 (originally RFC 2052).
/// </summary>
/// <remarks>
/// <para>
/// An SRV record indicates:
/// <list type="bullet">
///   <item><description><see cref="Priority"/>: A 16-bit integer indicating the preference for this target server.
///   Lower values are more preferred.</description></item>
///   <item><description><see cref="Weight"/>: A 16-bit load-balancing weight. If multiple servers have the same
///   <see cref="Priority"/>, clients should distribute connections proportionally based on these weights.</description></item>
///   <item><description><see cref="Port"/>: The TCP or UDP port on which the service is provided.</description></item>
///   <item><description><see cref="Server"/>: The domain name of the target host providing the service.</description></item>
/// </list>
/// </para>
/// <para>
/// For example, a typical SRV record might look like:
/// <code>
/// _sip._tcp.example.com. IN SRV 10 60 5060 sipserver.example.com.
/// </code>
/// This indicates that for the service <c>_sip</c> over TCP, <c>example.com</c> uses
/// <c>sipserver.example.com</c> on port 5060 with priority 10 and weight 60.
/// </para>
/// <para>
/// SRV records facilitate service discovery by allowing multiple servers to be specified,
/// each with its own priority and weight. Clients look up the SRV record to find the
/// hostname and port for the given service, potentially balancing load or choosing
/// higher-priority servers first.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x21)]
public class SRV : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the priority for this target host.
	/// Lower values indicate higher priority.
	/// </summary>
	[DNSField]
	public ushort Priority { get; set; }

	/// <summary>
	/// Gets or sets the weight for load balancing among multiple targets
	/// that share the same <see cref="Priority"/>.
	/// </summary>
	[DNSField]
	public ushort Weight { get; set; }

	/// <summary>
	/// Gets or sets the TCP or UDP port on which the service is provided.
	/// </summary>
	[DNSField]
	public ushort Port { get; set; }

	/// <summary>
	/// Gets or sets the domain name of the target host providing the service.
	/// </summary>
	[DNSField]
	public DNSDomainName Server { get; set; }

	/// <summary>
	/// Returns a string summarizing the SRV record, e.g. "Priority Weight Port ServerName".
	/// </summary>
	public override string ToString() => $"{Priority} {Weight} {Port} {Server}";
}
