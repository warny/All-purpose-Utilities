using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1706
{
    [DNSClass(0x16)]
    public class NSAP : DNSResponseDetail
    {
        [DNSField]
        public string Datas { get; set; }

        public override string ToString() => Datas;
    }
}
