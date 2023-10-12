using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC2163
{
    internal class PX : DNSResponseDetail
    {
        [DNSField]
        public ushort Preference { get; set; }

        [DNSField]
        public DNSDomainName Map822 { get; set; }

        [DNSField]
        public DNSDomainName MapX400 { get; set; }
    }
}
