using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utils.Net.DNS.RFC1035
{
	[DNSClass(0x0A)]
	public class NULL : DNSResponseDetail
	{
        /*
            NULL RDATA format (EXPERIMENTAL)

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                  <anything>                   /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            Anything at all may be in the RDATA field so long as it is 65535 octets
            or less.
        */
        [DNSField(-1)]
        public byte[] Datas { get; set; }

		public override string ToString() => "{ " + string.Join(" ", Datas.Select(d=>d.ToString("X2"))) + "}";
	}
}
