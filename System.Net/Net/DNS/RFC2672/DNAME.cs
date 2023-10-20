using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC2672
{
    [DNSRecord("IN", 0x27)]
    public class DNAME 
    {
        [DNSField]
        public DNSDomainName Target { get; set; }
    }
}
