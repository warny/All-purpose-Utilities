using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Utils.Net.DNS.RFC1886
{
    [DNSClass(0x1C)]
    public class AAAA : DNSResponseDetail
    {
        [DNSField(-1)]
        private byte[] ipAddressBytes
        {
            get => ipAddress.GetAddressBytes();
            set => ipAddress = new IPAddress(value);
        }

        public IPAddress IPAddress {
            get => ipAddress;
            set {
                if (value.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) throw new NotSupportedException("AAAA records only support IPV6 addresses");
                ipAddress = value;
            }
        }
 		private System.Net.IPAddress ipAddress = null;

        public override string ToString()
        {
            return ipAddress.ToString();
        }
    }
}
