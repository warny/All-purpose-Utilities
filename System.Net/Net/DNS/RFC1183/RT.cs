using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1183;

[DNSRecord("IN", 0x15)]
public class RT : DNSResponseDetail
{
    [DNSField]
    public ushort Preference { get; set; }
    [DNSField]
    public DNSDomainName DnsName { get; set; }

		public override string ToString() => $"{Preference}\t{DnsName}";
	}
