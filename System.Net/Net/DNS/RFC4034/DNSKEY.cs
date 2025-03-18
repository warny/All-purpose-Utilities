using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC4034
{
    [DNSRecord(DNSClass.IN, 0x30)]
    public class DNSKEY : DNSResponseDetail
    {


        [DNSField]
        public ushort Flags { get; set; }

        [DNSField]
        public byte Protocol { get; set; }
        
        [DNSField]
        public byte Algorithm { get; set; }

        [DNSField]
        public byte[] Key { get; set; }
    }
}
