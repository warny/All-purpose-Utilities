using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net;

namespace Utils.Net.DNS.RFC2052;

/// <summary>
/// Define a specific server for an application
/// </summary>
/// <see cref="https://datatracker.ietf.org/doc/html/rfc2782"/>
[DNSRecord("IN", 0x21)]
public class SRV : DNSResponseDetail
{
    [DNSField]
    public ushort Priority { get; set; }
    [DNSField]
    public ushort Weight { get; set; }
    [DNSField]
    public ushort Port { get; set; }
    [DNSField]
    public DNSDomainName Server { get; set; }
}
