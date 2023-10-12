using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1183;

/// <summary>
/// Responsible person
/// </summary>
[DNSClass(0x11)]
public class RP : DNSResponseDetail
{
    [DNSField]
    public DNSDomainName MBoxDName { get; set; }
    [DNSField]
    public DNSDomainName TxtDName { get; set; }

		public override string ToString() => $"{MBoxDName}\t{TxtDName}";

}
