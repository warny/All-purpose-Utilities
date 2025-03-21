using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents an SOA (Start of Authority) record, which defines zone-wide parameters
/// including the primary name server, mailbox of the zone administrator, and various
/// time intervals affecting caching and zone transfers.
/// </summary>
/// <remarks>
/// <para>
/// The SOA record (type 0x06) is mandatory at the apex of every DNS zone. It includes:
/// <list type="bullet">
///   <item>
///     <description><see cref="MName"/>: The primary name server for this zone (the origin of zone data).</description>
///   </item>
///   <item>
///     <description><see cref="RName"/>: The mailbox of the person responsible for this zone, typically in the form
///     of a domain name, e.g. <c>hostmaster.example.com</c> with a dot replacing the <c>@</c>.</description>
///   </item>
///   <item>
///     <description><see cref="Serial"/>: A 32-bit version number for the zone. Secondary name servers use this
///     to detect zone updates.</description>
///   </item>
///   <item>
///     <description><see cref="Refresh"/>: The time interval in seconds before secondary name servers should
///     check for updates from the primary.</description>
///   </item>
///   <item>
///     <description><see cref="Retry"/>: If a refresh attempt fails, the time (in seconds) to wait before retrying.</description>
///   </item>
///   <item>
///     <description><see cref="Expire"/>: The upper limit (in seconds) on how long a secondary may continue serving
///     data if it cannot contact the primary.</description>
///   </item>
///   <item>
///     <description><see cref="Minimum"/>: The minimum TTL (in seconds) to be exported with any RR from this zone.
///     Historically also used as the negative-caching TTL in some contexts.</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// In typical zone files, the <c>SOA</c> record looks like:
/// <code>
/// example.com.  IN  SOA  ns1.example.com. hostmaster.example.com. (
///       2023031501 ; Serial
///       10800      ; Refresh (3 hours)
///       3600       ; Retry (1 hour)
///       604800     ; Expire (7 days)
///       3600       ; Minimum (1 hour)
/// )
/// </code>
/// See RFC 1035 Section 3.3.13 and related documentation for more details.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x06)]
public class SOA : DNSResponseDetail
{
	/*
        SOA RDATA format (RFC 1035 Section 3.3.13):

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                     MNAME                     /
            /                                               /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                     RNAME                     /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    SERIAL                     |
            |                                               |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    REFRESH                    |
            |                                               |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                     RETRY                     |
            |                                               |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    EXPIRE                     |
            |                                               |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    MINIMUM                    |
            |                                               |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        Field Details:
            MNAME:   Primary master name server for this zone.
            RNAME:   Mailbox of the person responsible for the zone (as a domain name).
            SERIAL:  32-bit version number of the zone file.
            REFRESH: Interval (seconds) before a secondary checks for an update.
            RETRY:   Interval (seconds) to wait before retrying a failed refresh.
            EXPIRE:  Upper limit (seconds) for how long a secondary should use the
                        zone data if unable to contact the primary.
            MINIMUM: Minimum TTL (seconds) for records in this zone (used in queries).
    */

	/// <summary>
	/// Gets or sets the domain name of the primary master name server for this zone.
	/// </summary>
	[DNSField]
	public DNSDomainName MName { get; set; }

	/// <summary>
	/// Gets or sets the mailbox domain name of the person responsible for this zone.
	/// Commonly written as something like "hostmaster.example.com" with a '.' in place of '@'.
	/// </summary>
	[DNSField]
	public DNSDomainName RName { get; set; }

	/// <summary>
	/// Gets or sets the serial number of this zone. Incrementing this value
	/// indicates to secondaries that new data is available.
	/// </summary>
	[DNSField]
	public uint Serial { get; set; }

	/// <summary>
	/// Gets or sets the refresh interval, in seconds, that a secondary should
	/// wait before checking for updates from the primary.
	/// </summary>
	[DNSField]
	public uint Refresh { get; set; }

	/// <summary>
	/// Gets or sets the retry interval, in seconds, for a secondary to wait
	/// if the refresh attempt fails.
	/// </summary>
	[DNSField]
	public uint Retry { get; set; }

	/// <summary>
	/// Gets or sets the expire value, in seconds, indicating how long a secondary
	/// may continue using the zone data if it cannot reach the primary.
	/// </summary>
	[DNSField]
	public uint Expire { get; set; }

	/// <summary>
	/// Gets or sets the minimum TTL, in seconds, used as a lower bound for
	/// all resource record TTLs in the zone.
	/// </summary>
	[DNSField]
	public uint Minimum { get; set; }

	/// <summary>
	/// Returns a multi-line string describing the main parameters of the SOA record.
	/// </summary>
	public override string ToString() => 
		$"""
		MName   :\t{MName}
		RName   :\t{RName}
		Serial  :\t{Serial}
		Refresh :\t{Refresh}
		Retry   :\t{Retry}
		Expire  :\t{Expire}
		Minimum :\t{Minimum}
		""";
}
