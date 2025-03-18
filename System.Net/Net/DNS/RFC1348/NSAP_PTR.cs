using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1348;

/// <summary>
/// domain name-to-NSAP mapping
/// </summary>
/// <see cref="https://datatracker.ietf.org/doc/html/rfc1348"/>
[DNSRecord(DNSClass.IN, 0x16, "NSAP-PTR")]
public class NSAP_PTR : DNSResponseDetail
{
    [DNSField]
    public DNSDomainName DomainName;
}
