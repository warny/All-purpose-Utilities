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
        public IPAddress IPAddress {
            get => ipAddress;
            set {
                if (value.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) throw new NotSupportedException("Les enregistrements AAAA ne suppote que les adresse IPV6");
                ipAddress = value;
            }
        }
 		private System.Net.IPAddress ipAddress = null;

        protected internal override void Read(DNSDatagram datagram, DNSFactory factory)
        {
            IPAddress = new IPAddress(datagram.ReadBytes(16));
        }

        protected internal override void Write(DNSDatagram datagram, DNSFactory factory)
        {
            datagram.Write(IPAddress.GetAddressBytes());
        }


        public override string ToString()
        {
            return ipAddress.ToString();
        }
    }
}
