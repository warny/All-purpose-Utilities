using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Utils.Net.DNS.RFC1035;
using Utils.Net.DNS.RFC1183;
using Utils.Net.DNS.RFC1876;
using Utils.Net.DNS.RFC2052;

namespace Utils.Net.DNS
{
	public class DNSFactory
	{
		public static Type[] DNSTypes { get; } = [
			typeof(Default),
			typeof(A), typeof(CNAME),
			typeof(SOA), typeof(MX), typeof(MINFO), typeof(SRV),
			typeof(HINFO), typeof(TXT), typeof(NULL),
			typeof(NS), typeof(MB), typeof(MG), typeof(MR),
			typeof(PTR), typeof(WKS),
			typeof(AFSDB), typeof(ISDN), typeof(RP), typeof(RT), typeof(X25),
			typeof(LOC)
		];
        public static Type[] ObsoletesDNSTypes { get; } = [
            typeof(MD),
            typeof(MF),
        ];

	}
}
