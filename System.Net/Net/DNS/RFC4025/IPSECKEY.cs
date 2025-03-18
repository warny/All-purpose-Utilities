using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Utils.Net.DNS.RFC4025
{
    [DNSRecord(DNSClass.IN, 0x2D)]
    public class IPSECKEY : DNSResponseDetail
    {
        /*
            The RDATA for an IPSECKEY RR consists of a precedence value, a
            gateway type, a public key, algorithm type, and an optional gateway
            address.

                0                   1                   2                   3
                0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
                +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                |  precedence   | gateway type  |  algorithm  |     gateway     |
                +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-------------+                 +
                ~                            gateway                            ~
                +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                |                                                               /
                /                          public key                           /
                /                                                               /
                +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-|
        */
        [DNSField]
        public byte Precedence { get; set; }

        [DNSField]
        private GatewayType gatewayType;

        public GatewayType GatewayType => gatewayType;

        [DNSField]
        public IPSecAlgorithm SecAlgorithm { get; set; }

        [DNSField(4, Condition = "GatewayType==Utils.Net.DNS.GatewayType.IPV4GatewayAddress")]
        private IPAddress gatewayAddressIPv4 = null;

        [DNSField(16, Condition = "GatewayType==Utils.Net.DNS.GatewayType.IPV6GatewayAddress")]
        private IPAddress gatewayAddressIPv6 = null;

        public IPAddress GatewayAddress
        {
            get => gatewayAddressIPv4 ?? gatewayAddressIPv6;
            set
            {
                if (value is null)
                {
                    gatewayType = GatewayType.NoGateway;
                } else {
                    gatewayType = value.AddressFamily switch
                    {
                        System.Net.Sockets.AddressFamily.InterNetwork => GatewayType.IPV4GatewayAddress,
                        System.Net.Sockets.AddressFamily.InterNetworkV6 => GatewayType.IPV6GatewayAddress,
                        _ => throw new NotSupportedException("IPSECKEY only supports IPv4 and IPv6 addresses or domain names")
                    };
                }
                gatewayAddressIPv4 = value.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? value : null;
                gatewayAddressIPv6 = value.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? value : null;
                gatewayDomainName = null;
            }
        }

        [DNSField(Condition = "GatewayType==Utils.Net.DNS.GatewayType.WireEncodedDomain")]
        private DNSDomainName? gatewayDomainName = null;

        public DNSDomainName? GatewayDomainName
        {
            get => gatewayDomainName;
            set
            {
                gatewayType = value is null ? GatewayType.NoGateway : GatewayType.WireEncodedDomain;
                gatewayAddressIPv4 = null;
                gatewayAddressIPv6 = null;
                gatewayDomainName = value;
            }
        }
        

        public byte[] PublicKey { get; set; }
    }
}
