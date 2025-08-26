# Utils.Net Library

The **Utils.Net** package groups network-related helpers and a minimal DNS protocol implementation.
It targets **.NET 9** and is designed to be portable across platforms.

## Features

- DNS packet reader and writer with support for common record types
- ICMP utilities enabling basic ping and traceroute functionality
- Helpers to query network interfaces and parameters from the operating system
- Wake-on-LAN magic packet helpers
- ARP packet creation utilities
- URI and query string manipulation helpers
- Parsing of mail addresses and IP range calculations
- Clients for basic network services like Echo, Quote of the Day, Time protocol and NTP

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

// Send a Wake-on-LAN packet
await Utils.Net.WakeOnLan.SendMagicPacketAsync(System.Net.NetworkInformation.PhysicalAddress.Parse("01-23-45-67-89-AB"));

// Build an ARP request
var arpRequest = Utils.Net.ArpUtils.CreateRequest(System.Net.IPAddress.Parse("192.168.1.1"), System.Net.NetworkInformation.PhysicalAddress.Parse("00-11-22-33-44-55"), System.Net.IPAddress.Parse("192.168.1.2"));

// Retrieve time using NTP
DateTime utcNow = await Utils.Net.NtpClient.GetTimeAsync("pool.ntp.org");

// Fetch the quote of the day
string quote = await Utils.Net.QuoteOfTheDayClient.GetQuoteAsync("djxmmx.net");

// Communicate with a command/response service
using var tcp = new System.Net.Sockets.TcpClient();
await tcp.ConnectAsync("localhost", 25);
using var cmdClient = new Utils.Net.CommandResponseClient { NoOpInterval = TimeSpan.FromMinutes(1) };
await cmdClient.ConnectAsync(tcp.GetStream());
IReadOnlyList<Utils.Net.ServerResponse> replies = await cmdClient.SendCommandAsync("NOOP");
await cmdClient.DisconnectAsync("QUIT", TimeSpan.FromSeconds(1));

// Build a simple command/response server
var server = new Utils.Net.CommandResponseServer();
server.CommandReceived += cmd =>
    System.Threading.Tasks.Task.FromResult<IEnumerable<Utils.Net.ServerResponse>>(
        new[] { new Utils.Net.ServerResponse(200, "OK") });
await server.StartAsync(tcp.GetStream());
```
