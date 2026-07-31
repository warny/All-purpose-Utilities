using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Utils.Net
{
    /// <summary>
    /// Provides access to host networking metadata such as interfaces and DNS servers.
    /// </summary>
    public class NetworkParameters
    {
        private readonly IPAddress[] _dnsServers;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkParameters"/> class and snapshots network information.
        /// </summary>
        public NetworkParameters()
        {
            NetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            var snapshots = new List<NetworkInterfaceSnapshot>(NetworkInterfaces.Length);
            foreach (NetworkInterface networkInterface in NetworkInterfaces)
            {
                snapshots.Add(CreateSnapshot(networkInterface));
            }

            _dnsServers = SelectDnsServers(snapshots);
            PrimaryDns = _dnsServers.Length > 0 ? _dnsServers[0] : null;
        }

        /// <summary>
        /// Gets the network interfaces detected on the host at construction time.
        /// </summary>
        public NetworkInterface[] NetworkInterfaces { get; private set; }

        /// <summary>
        /// Gets the preferred DNS server resolved from the network interfaces, or <see langword="null"/> if no DNS server was discovered.
        /// </summary>
        public IPAddress? PrimaryDns { get; }

        /// <summary>
        /// Gets the collection of DNS servers discovered for the active network interfaces.
        /// </summary>
        /// <remarks>Returns a defensive copy; mutating the result does not affect this instance.</remarks>
        public IPAddress[] DnsServers => (IPAddress[])_dnsServers.Clone();

        /// <summary>
        /// Builds a pure snapshot of the DNS-relevant properties of a single network interface.
        /// </summary>
        private static NetworkInterfaceSnapshot CreateSnapshot(NetworkInterface networkInterface)
        {
            IReadOnlyList<IPAddress> dns = Array.Empty<IPAddress>();
            IReadOnlyList<GatewayIPAddressInformation> gateways = Array.Empty<GatewayIPAddressInformation>();
            int? metric = null;

            if (networkInterface.OperationalStatus == OperationalStatus.Up)
            {
                IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
                dns = ipProperties.DnsAddresses?.ToArray() ?? (IReadOnlyList<IPAddress>)Array.Empty<IPAddress>();
                gateways = ipProperties.GatewayAddresses?.ToArray() ?? (IReadOnlyList<GatewayIPAddressInformation>)Array.Empty<GatewayIPAddressInformation>();
                metric = TryGetMetric(ipProperties);
            }

            return new NetworkInterfaceSnapshot(networkInterface.OperationalStatus, metric, dns, gateways);
        }

        private static int? TryGetMetric(IPInterfaceProperties ipProperties)
        {
            // The routing metric is only meaningful on the IPv4/IPv6 interface properties and is
            // not available on every platform. Treat any failure as "unknown metric".
            try
            {
                IPv4InterfaceProperties v4 = ipProperties.GetIPv4Properties();
                if (v4 is not null)
                    return v4.Index;
            }
            catch (NetworkInformationException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }
            return null;
        }

        /// <summary>
        /// Selects and orders the DNS servers from a set of interface snapshots. Pure function:
        /// depends only on its input, which makes DNS discovery testable without real interfaces.
        /// </summary>
        /// <remarks>
        /// Any interface whose <see cref="NetworkInterfaceSnapshot.Status"/> is
        /// <see cref="OperationalStatus.Up"/> contributes its DNS servers, whether or not it has a
        /// default gateway. This deliberately includes VPN and point-to-point interfaces that carry
        /// a resolver but advertise no gateway. Ordering is deterministic:
        /// interfaces that have at least one gateway are preferred over those without, then by
        /// ascending metric (unknown metric last), then by OS enumeration order, then by DNS order
        /// within an interface. Wildcard/unspecified and multicast resolver addresses are excluded,
        /// and duplicates are removed while preserving the first-seen priority.
        /// </remarks>
        internal static IPAddress[] SelectDnsServers(IReadOnlyList<NetworkInterfaceSnapshot> snapshots)
        {
            if (snapshots is null)
                throw new ArgumentNullException(nameof(snapshots));

            var ordered = snapshots
                .Select((snapshot, index) => (snapshot, index))
                .Where(item => item.snapshot.Status == OperationalStatus.Up)
                .OrderByDescending(item => item.snapshot.Gateways.Count > 0)
                .ThenBy(item => item.snapshot.Metric ?? int.MaxValue)
                .ThenBy(item => item.index);

            var result = new List<IPAddress>();
            var seen = new HashSet<IPAddress>();
            foreach (var item in ordered)
            {
                foreach (IPAddress dns in item.snapshot.DnsAddresses)
                {
                    if (dns is null)
                        continue;
                    if (dns.Equals(IPAddress.Any) || dns.Equals(IPAddress.IPv6Any)
                        || dns.Equals(IPAddress.None) || dns.Equals(IPAddress.IPv6None))
                        continue;
                    if (IsMulticast(dns))
                        continue;
                    if (seen.Add(dns))
                        result.Add(dns);
                }
            }

            return result.ToArray();
        }

        private static bool IsMulticast(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return address.IsIPv6Multicast;

            byte[] bytes = address.GetAddressBytes();
            return bytes.Length == 4 && bytes[0] >= 224 && bytes[0] <= 239;
        }
    }

    /// <summary>
    /// A pure, testable snapshot of the DNS-relevant properties of a single network interface.
    /// </summary>
    /// <param name="Status">The operational status of the interface.</param>
    /// <param name="Metric">The routing metric of the interface, or <see langword="null"/> when unknown.</param>
    /// <param name="DnsAddresses">The DNS resolver addresses configured on the interface.</param>
    /// <param name="Gateways">The default gateways configured on the interface.</param>
    internal sealed record NetworkInterfaceSnapshot(
        OperationalStatus Status,
        int? Metric,
        IReadOnlyList<IPAddress> DnsAddresses,
        IReadOnlyList<GatewayIPAddressInformation> Gateways);
}
