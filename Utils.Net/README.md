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

## DNS examples

`DNSLookup` sends DNS queries over UDP and returns a `DNSHeader` whose `Responses` list contains typed `DNSResponseRecord` entries. Cast `RData` to the record-specific class to access its fields.

### A record — IPv4 address

```csharp
var dns = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader response = dns.Request("A", "example.com");

foreach (var record in response.Responses)
{
    var a = (Utils.Net.DNS.RFC1035.Address)record.RData;
    Console.WriteLine($"{record.Name} -> {a.IPAddress}  (TTL {record.TTL}s)");
}
```

### AAAA record — IPv6 address

```csharp
var dns = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader response = dns.Request("AAAA", "example.com");

foreach (var record in response.Responses)
{
    var aaaa = (Utils.Net.DNS.RFC1035.Address)record.RData;
    Console.WriteLine($"{record.Name} -> {aaaa.IPAddress}");
}
```

### MX records — mail servers

```csharp
var dns = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader response = dns.Request("MX", "example.com");

foreach (var record in response.Responses)
{
    var mx = (Utils.Net.DNS.RFC1035.MX)record.RData;
    Console.WriteLine($"Priority {mx.Preference}: {mx.Exchange}");
}
// Priority 10: mail1.example.com
// Priority 20: mail2.example.com
```

### NS records — authoritative name servers

```csharp
var dns = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader response = dns.Request("NS", "example.com");

foreach (var record in response.Responses)
{
    var ns = (Utils.Net.DNS.RFC1035.NS)record.RData;
    Console.WriteLine(ns.DNSName);
}
```

### TXT records — SPF, DKIM, and other text data

```csharp
var dns = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader response = dns.Request("TXT", "example.com");

foreach (var record in response.Responses)
{
    var txt = (Utils.Net.DNS.RFC1035.TXT)record.RData;
    Console.WriteLine(txt.Text);
}
// v=spf1 include:_spf.example.com ~all
```

### CNAME record — canonical name alias

```csharp
var dns = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader response = dns.Request("CNAME", "www.example.com");

var cname = (Utils.Net.DNS.RFC1035.CNAME)response.Responses[0].RData;
Console.WriteLine($"www.example.com is an alias for {cname.CName}");
```

### PTR record — reverse DNS lookup

Reverse lookups query the `in-addr.arpa` zone with the octets of the IPv4 address reversed.

```csharp
var dns = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader response = dns.Request("PTR", "1.0.0.93.in-addr.arpa");

var ptr = (Utils.Net.DNS.RFC1035.PTR)response.Responses[0].RData;
Console.WriteLine($"93.0.0.1 resolves to {ptr.PTRName}");
```

### Custom DNS server

Pass one or more name server IP addresses to bypass the system resolver.

```csharp
using System.Net;

var dns = new Utils.Net.DNSLookup(IPAddress.Parse("8.8.8.8"), IPAddress.Parse("8.8.4.4"));
Utils.Net.DNS.DNSHeader response = dns.Request("A", "example.com");
var address = ((Utils.Net.DNS.RFC1035.Address)response.Responses[0].RData).IPAddress;
```

### Inspecting authority and additional records

`DNSHeader` also exposes `Authorities` and `Additionals` alongside `Responses`.

```csharp
var dns = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader response = dns.Request("NS", "example.com");

foreach (var auth in response.Authorities)
    Console.WriteLine($"Authority: {auth}");

foreach (var add in response.Additionals)
    Console.WriteLine($"Additional: {add}");
```

## Related packages
- `omy.Utils` – foundational helpers used by networking utilities.
- `omy.Utils.IO` – stream helpers used by protocol implementations.

## Security recommendations
- Always use encrypted transports (for example `SslStream`) before sending credentials over POP3/SMTP protocols.
- Treat `Obsolete` warnings on insecure authentication helpers as security warnings, not cosmetic warnings.
- Validate and constrain all external inputs (domain names, headers, protocol lines, and payload lengths).
- Apply explicit timeouts and maximum payload sizes for network calls that can read untrusted remote data.
- Run dependency vulnerability checks regularly in CI:

```bash
dotnet list package --vulnerable --include-transitive
```
