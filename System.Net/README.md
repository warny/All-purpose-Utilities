# Utils.Net Library

The **Utils.Net** package groups network-related helpers and a minimal DNS protocol implementation.
It targets **.NET 9** and is designed to be portable across platforms.

## Features

- DNS packet reader and writer with support for common record types
- ICMP utilities enabling basic ping and traceroute functionality
- Helpers to query network interfaces and parameters from the operating system
- URI and query string manipulation helpers
- Parsing of mail addresses and IP range calculations

## Usage examples
```csharp
// Manipulate a query string
var builder = new Utils.Net.UriBuilderEx("http://example.com/?key1=value1&key2=value2");
builder.QueryString["key3"].Add("value3");
string url = builder.ToString();

// Perform a DNS lookup
var dns = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader response = dns.Request("A", "example.com");
var address = ((Utils.Net.DNS.RFC1035.Address)response.Responses[0].RData).IPAddress;

// Send an ICMP echo request
int rtt = await Utils.Net.IcmpUtils.SendEchoRequestAsync(System.Net.IPAddress.Parse("8.8.8.8"));
```
