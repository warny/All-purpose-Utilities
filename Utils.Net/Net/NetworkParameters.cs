using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace Utils.Net
{
    /// <summary>
    /// Provides access to host networking metadata such as interfaces and DNS servers.
    /// </summary>
    public class NetworkParameters
    {
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
            DnsServers = dnsServers.ToArray();
        }

        /// <summary>
        /// Gets the network interfaces detected on the host at construction time.
        /// </summary>
        public NetworkInterface[] NetworkInterfaces { get; private set; }
        /// <summary>
        /// Gets the preferred DNS server resolved from the network interfaces.
        /// </summary>
        public IPAddress PrimaryDns => DnsServers[0];
        /// <summary>
        /// Gets the collection of DNS servers discovered for the active network interfaces.
        /// </summary>
        public IPAddress[] DnsServers { get; private set; }

    }
}
