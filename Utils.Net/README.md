# omy.Utils.Net (network helpers)

`omy.Utils.Net` focuses on network protocols and helpers you can consume directly from NuGet. It includes DNS, ICMP, ARP, Wake-on-LAN, URI/query string helpers, and command/response clients.

## Install
```bash
dotnet add package omy.Utils.Net
```

## Supported frameworks
- net9.0

## Features
- DNS packet reader/writer with support for common record types.
- ICMP utilities for ping/traceroute and ARP packet creation helpers.
- Wake-on-LAN helpers to emit magic packets.
- URI builder with editable query string collections.
- Command/response client plus POP3, SMTP, NNTP, NTP, Echo, and Quote-of-the-Day helpers.
- Network interface helpers to inspect addresses and parameters.

## Quick usage
```csharp
// Manipulate a query string
var builder = new Utils.Net.UriBuilderEx("http://example.com/?key1=value1&key2=value2");
builder.QueryString["key3"].Add("value3");
string url = builder.ToString();

// Perform a DNS lookup
var dns = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader response = dns.Request("A", "example.com");
var address = ((Utils.Net.DNS.RFC1035.Address)response.Responses[0].RData).IPAddress;
```

## Related packages
- `omy.Utils` – foundational helpers used by networking utilities.
- `omy.Utils.IO` – stream helpers used by protocol implementations.
