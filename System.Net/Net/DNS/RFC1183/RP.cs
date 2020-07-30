using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1183
{
    [DNSClass(17)]
    public class RP : DNSResponseDetail
    {
        [DNSField]
        public string MBoxDName { get; set; }
        [DNSField]
        public string TxtDName { get; set; }

		public override string ToString() => $"{MBoxDName}\t{TxtDName}";

    }
}
