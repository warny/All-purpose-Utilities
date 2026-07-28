using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;

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

            List<IPAddress> dnsServers = new List<IPAddress>();
            foreach (NetworkInterface networkInterface in NetworkInterfaces)
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
                    if (ipProperties.GatewayAddresses == null || ipProperties.GatewayAddresses.Count == 0) continue;
                    IPAddressCollection dnsAddresses = ipProperties.DnsAddresses;

                    foreach (IPAddress dnsAdress in dnsAddresses)
                    {
                        dnsServers.Add(dnsAdress);
                    }
                }
            }
            _dnsServers = dnsServers.ToArray();
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
        public IPAddress[] DnsServers => (IPAddress[])_dnsServers.Clone();
    }
}
