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
        private readonly NetworkInterface[] _networkInterfaces;
        private readonly IReadOnlyList<NetworkInterface> _networkInterfacesView;
        private readonly IPAddress[] _dnsServers;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkParameters"/> class and snapshots network information.
        /// </summary>
        public NetworkParameters()
        {
            _networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            _networkInterfacesView = Array.AsReadOnly(_networkInterfaces);

            var snapshots = new List<NetworkInterfaceSnapshot>(_networkInterfaces.Length);
            foreach (NetworkInterface networkInterface in _networkInterfaces)
            {
                snapshots.Add(CreateSnapshot(networkInterface));
            }

            _dnsServers = SelectDnsServers(snapshots);
            PrimaryDns = _dnsServers.Length > 0 ? _dnsServers[0] : null;
        }

        /// <summary>
        /// Gets the network interfaces detected on the host at construction time as a stable
        /// read-only view. The same instance is returned on every call; callers cannot replace
        /// or add elements.
        /// </summary>
        public IReadOnlyList<NetworkInterface> NetworkInterfaces => _networkInterfacesView;

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

            if (networkInterface.OperationalStatus == OperationalStatus.Up)
            {
                IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
                dns = ipProperties.DnsAddresses?.ToArray() ?? (IReadOnlyList<IPAddress>)Array.Empty<IPAddress>();
            }

            return new NetworkInterfaceSnapshot(networkInterface.OperationalStatus, dns);
        }

        /// <summary>
        /// Selects and orders the DNS servers from a set of interface snapshots. Pure function:
        /// depends only on its input, which makes DNS discovery testable without real interfaces.
        /// </summary>
        /// <remarks>
        /// Any interface whose <see cref="NetworkInterfaceSnapshot.Status"/> is
        /// <see cref="OperationalStatus.Up"/> contributes its DNS servers, whether or not it has a
        /// default gateway. This deliberately includes VPN, point-to-point and gateway-less
        /// interfaces. Ordering strictly follows the OS enumeration order of interfaces (the order
        /// returned by <see cref="NetworkInterface.GetAllNetworkInterfaces"/>), then by DNS
        /// address order within each interface. Wildcard/unspecified and multicast resolver
        /// addresses are excluded, and duplicates are removed while preserving the first-seen
        /// priority.
        /// </remarks>
        internal static IPAddress[] SelectDnsServers(IReadOnlyList<NetworkInterfaceSnapshot> snapshots)
        {
            if (snapshots is null)
                throw new ArgumentNullException(nameof(snapshots));

            var active = snapshots
                .Where(item => item.Status == OperationalStatus.Up);

            var result = new List<IPAddress>();
            var seen = new HashSet<IPAddress>();
            foreach (var item in active)
            {
                foreach (IPAddress dns in item.DnsAddresses)
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
    /// <param name="DnsAddresses">The DNS resolver addresses configured on the interface.</param>
    internal sealed record NetworkInterfaceSnapshot(
        OperationalStatus Status,
        IReadOnlyList<IPAddress> DnsAddresses);
}
