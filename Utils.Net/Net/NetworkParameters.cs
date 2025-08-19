using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace Utils.Net
{
    /// <summary>
    /// Description résumée de NetworkParams.
    /// </summary>
    public class NetworkParameters
    {
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

        public NetworkInterface[] NetworkInterfaces { get; private set; }
        public IPAddress PrimaryDns => DnsServers[0];
        public IPAddress[] DnsServers { get; private set; }

    }
}
