using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net;

namespace Utils.Net.DNS.RFC2052
{
    [DNSClass(0x21)]
    public class SRV : DNSResponseDetail
    {
        [DNSField]
        public string Server { get; set; }
        [DNSField]
        public ushort Priority { get; set; }
        [DNSField]
        public ushort Weight { get; set; }
        [DNSField]
        public ushort Port { get; set; }
    }
}
