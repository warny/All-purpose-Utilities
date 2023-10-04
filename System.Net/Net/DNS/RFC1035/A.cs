using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Utils.Net.DNS.RFC1035
{
    /// <summary>
    /// IPAddress, used for both RFC1035.A and RFC1886.AAAA records
    /// </summary>
    [DNSClass(0x01, "A")]
    [DNSClass(0x1C, "AAAA")]
    public sealed class A : DNSResponseDetail
    {
        /*
            A RDATA format

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                |                    ADDRESS                    |
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            ADDRESS         A 32 bit Internet address.

            Hosts that have multiple Internet addresses will have multiple A
            records.
            A records cause no additional section processing.  The RDATA section of
            an A line in a master file is an Internet address expressed as four
            decimal numbers separated by dots without any imbedded spaces (e.g.,
            "10.2.0.52" or "192.0.5.6").
         */

        internal override ushort ClassId => ipAddress.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork => 0x01,
            System.Net.Sockets.AddressFamily.InterNetworkV6 => 0x1C,
            _ => throw new NotSupportedException("A and AAAA records only support IPV4 and IPV6 addresses")
        };

        public override string Name => ipAddress.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork => "A",
            System.Net.Sockets.AddressFamily.InterNetworkV6 => "AAAA",
            _ => throw new NotSupportedException("A and AAAA records only support IPV4 and IPV6 addresses")
        };

        [DNSField]
        private System.Net.IPAddress ipAddress = null;

        public IPAddress IPAddress
        {
            get => ipAddress;
            set {
                if (value.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork && value.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    throw new NotSupportedException("A and AAAA records only support IPV4 and IPV6 addresses");
                }
                ipAddress = value;
            }
        }

		public override string ToString() => IPAddress.ToString();
	}
}
                                       