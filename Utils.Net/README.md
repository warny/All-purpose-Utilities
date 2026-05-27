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

## ICMP examples

`IcmpUtils` sends raw ICMP packets. **Raw sockets require administrator / root privileges.**

### Ping

```csharp
using System.Net;

int ms = await Utils.Net.IcmpUtils.SendEchoRequestAsync(IPAddress.Parse("8.8.8.8"));
if (ms >= 0)
    Console.WriteLine($"Reply in {ms} ms");
else
    Console.WriteLine("Timeout");
```

### Traceroute

```csharp
using System.Net;

var hops = await Utils.Net.IcmpUtils.TracerouteAsync(IPAddress.Parse("8.8.8.8"), maxHops: 30, timeout: 1000);
foreach (var hop in hops)
    Console.WriteLine(hop); // "3: 10.0.0.1 - 12ms"
```

## Wake-on-LAN example

```csharp
using System.Net.NetworkInformation;

var mac = PhysicalAddress.Parse("AA-BB-CC-DD-EE-FF");
await Utils.Net.WakeOnLan.SendMagicPacketAsync(mac);
```

Broadcast to a specific subnet instead of the global broadcast address:

```csharp
using System.Net;
using System.Net.NetworkInformation;

var mac = PhysicalAddress.Parse("AA-BB-CC-DD-EE-FF");
await Utils.Net.WakeOnLan.SendMagicPacketAsync(mac, IPAddress.Parse("192.168.1.255"), port: 9);
```

## ARP example

`ArpUtils` builds ARP request packets for use in raw Ethernet frames.

```csharp
using System.Net;
using System.Net.NetworkInformation;

PhysicalAddress senderMac = PhysicalAddress.Parse("AA-BB-CC-DD-EE-FF");
IPAddress senderIp   = IPAddress.Parse("192.168.1.10");
IPAddress targetIp   = IPAddress.Parse("192.168.1.1");

Utils.Net.Arp.ArpPacket packet = Utils.Net.ArpUtils.CreateRequest(senderIp, senderMac, targetIp);
byte[] rawBytes = packet.ToBytes(); // send via a raw socket or pcap library
```

## NTP example

```csharp
DateTime utcNow = await Utils.Net.NtpClient.GetTimeAsync("pool.ntp.org");
Console.WriteLine($"NTP time: {utcNow:u}");
```

## Time Protocol (RFC 868) example

```csharp
DateTime utcNow = await Utils.Net.TimeProtocolClient.GetTimeAsync("time.nist.gov");
Console.WriteLine($"Time: {utcNow:u}");
```

## Echo (RFC 862) example

```csharp
string reply = await Utils.Net.EchoClient.EchoAsync("localhost", "hello");
Console.WriteLine(reply); // "hello"
```

## Quote of the Day (RFC 865) example

```csharp
string quote = await Utils.Net.QuoteOfTheDayClient.GetQuoteAsync("djxmmx.net");
Console.WriteLine(quote);
```

## SMTP examples

`SmtpClient` inherits from `CommandResponseClient`. Always wrap the TCP stream with `SslStream` before authenticating (the `AuthenticateAsync` method emits an `[Obsolete]` warning to discourage plain-text usage).

### Send a message

```csharp
using Utils.Net;

await using var smtp = new SmtpClient();
await smtp.ConnectAsync("smtp.example.com");

IReadOnlyList<string> extensions = await smtp.EhloAsync("myclient.local");

string body =
    "From: sender@example.com\r\n" +
    "To: recipient@example.com\r\n" +
    "Subject: Hello\r\n" +
    "\r\n" +
    "Hello from omy.Utils.Net!";

await smtp.SendMailAsync(
    from: "sender@example.com",
    recipients: ["recipient@example.com"],
    data: body);

await smtp.QuitAsync();
```

### Authenticate over TLS

```csharp
using System.Net.Security;
using System.Net.Sockets;
using Utils.Net;

using TcpClient tcp = new();
await tcp.ConnectAsync("smtp.example.com", 587);
SslStream ssl = new(tcp.GetStream());
await ssl.AuthenticateAsClientAsync("smtp.example.com");

await using SmtpClient smtp = new();
await smtp.ConnectAsync(ssl, leaveOpen: false);
await smtp.EhloAsync("myclient.local");

#pragma warning disable CS0618 // intentional: transport is TLS-protected
await smtp.AuthenticateAsync("user@example.com", "s3cr3t");
#pragma warning restore CS0618

await smtp.SendMailAsync("user@example.com", ["to@example.com"], "Subject: Hi\r\n\r\nBody");
await smtp.QuitAsync();
```

## POP3 examples

### List and retrieve messages

```csharp
using Utils.Net;

await using Pop3Client pop3 = new();
await pop3.ConnectAsync("pop3.example.com");

var (messageCount, mailboxSize) = await pop3.GetStatAsync();
Console.WriteLine($"{messageCount} messages, {mailboxSize} bytes");

IReadOnlyDictionary<int, int> listing = await pop3.ListAsync();
foreach (var (id, size) in listing)
{
    string raw = await pop3.RetrieveAsync(id);
    Console.WriteLine($"--- Message {id} ({size} bytes) ---");
    Console.WriteLine(raw);
}

await pop3.QuitAsync();
```

### Authenticate with APOP (challenge-response, no plain-text password)

```csharp
using Utils.Net;

await using Pop3Client pop3 = new();
await pop3.ConnectAsync("pop3.example.com");
await pop3.AuthenticateApopAsync("alice", "s3cr3t");

IReadOnlyDictionary<int, string> uids = await pop3.ListUniqueIdsAsync();
foreach (var (id, uid) in uids)
    Console.WriteLine($"{id}: {uid}");

await pop3.QuitAsync();
```

### Delete messages

```csharp
using Utils.Net;

await using Pop3Client pop3 = new();
await pop3.ConnectAsync("pop3.example.com");

#pragma warning disable CS0618
await pop3.AuthenticateAsync("alice", "s3cr3t"); // only over TLS
#pragma warning restore CS0618

await pop3.DeleteAsync(1);
await pop3.QuitAsync(); // deletion is committed on QUIT
```

## NNTP examples

### Browse and read articles

```csharp
using Utils.Net;

await using NntpClient nntp = new();
await nntp.ConnectAsync("news.example.com");

var groups = await nntp.ListAsync();
foreach (var (group, last, first) in groups)
    Console.WriteLine($"{group}: {first}-{last}");

var (count, firstId, lastId) = await nntp.GroupAsync("comp.lang.csharp");
Console.WriteLine($"{count} articles ({firstId}..{lastId})");

string article = await nntp.ArticleAsync(firstId);
Console.WriteLine(article);

await nntp.QuitAsync();
```

### Post an article

```csharp
using Utils.Net;

await using NntpClient nntp = new();
await nntp.ConnectAsync("news.example.com");

await nntp.GroupAsync("comp.lang.csharp");

string article =
    "From: author@example.com\r\n" +
    "Newsgroups: comp.lang.csharp\r\n" +
    "Subject: Test post\r\n" +
    "\r\n" +
    "Hello from omy.Utils.Net!";

await nntp.PostAsync(article);
await nntp.QuitAsync();
```

### List newsgroups created after a date

```csharp
using Utils.Net;

await using NntpClient nntp = new();
await nntp.ConnectAsync("news.example.com");

IReadOnlyList<string> newGroups = await nntp.NewGroupsAsync(DateTime.UtcNow.AddDays(-7));
foreach (string group in newGroups)
    Console.WriteLine(group);

await nntp.QuitAsync();
```

## SmtpServer example

`SmtpServer` handles the SMTP protocol on an already-accepted stream. Implement `ISmtpMessageStore` to receive delivered messages, and optionally `ISmtpAuthenticator` to validate credentials.

```csharp
using System.Net;
using System.Net.Sockets;
using Utils.Net;

// Minimal in-memory store
class InboxStore : ISmtpMessageStore
{
    public List<SmtpMessage> Messages { get; } = new();
    public Task StoreAsync(SmtpMessage message, CancellationToken ct = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}

// Accept one connection and handle it
var store = new InboxStore();
var listener = new TcpListener(IPAddress.Loopback, 2525);
listener.Start();
TcpClient tcp = await listener.AcceptTcpClientAsync();

using SmtpServer smtp = new(store, isLocalDomain: domain => domain == "example.com");
await smtp.StartAsync(tcp.GetStream());
await smtp.Completion;

Console.WriteLine($"Received {store.Messages.Count} message(s)");
```

To handle multiple clients, call `StartAsync` + `Completion` in a loop (one `SmtpServer` instance per accepted connection).

### With authentication

```csharp
class StaticAuthenticator : ISmtpAuthenticator
{
    public Task<SmtpAuthenticationResult> AuthenticateAsync(string user, string password, CancellationToken ct = default)
    {
        bool ok = user == "alice" && password == "s3cr3t";
        return Task.FromResult(new SmtpAuthenticationResult(ok, canRelay: ok));
    }
}

using SmtpServer smtp = new(store, isLocalDomain: _ => true, authenticator: new StaticAuthenticator());
await smtp.StartAsync(tcp.GetStream());
await smtp.Completion;
```

## Pop3Server example

Implement `IPop3Mailbox` to expose mailbox data. The server handles `USER`/`PASS`, `APOP`, `STAT`, `LIST`, `RETR`, `DELE`, and `QUIT`.

```csharp
using System.Net;
using System.Net.Sockets;
using Utils.Net;

class MemoryMailbox : IPop3Mailbox
{
    private readonly Dictionary<int, string> _messages = new()
    {
        [1] = "From: sender@example.com\r\nSubject: Hello\r\n\r\nBody text."
    };

    public Task<bool> AuthenticateAsync(string user, string password, CancellationToken ct = default)
        => Task.FromResult(user == "alice" && password == "s3cr3t");

    public Task<bool> AuthenticateApopAsync(string user, string timestamp, string digest, CancellationToken ct = default)
        => Task.FromResult(false); // APOP not supported in this example

    public Task<IReadOnlyDictionary<int, int>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<int, int>>(
            _messages.ToDictionary(kv => kv.Key, kv => Encoding.UTF8.GetByteCount(kv.Value)));

    public Task<IReadOnlyDictionary<int, string>> ListUidsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<int, string>>(
            _messages.ToDictionary(kv => kv.Key, kv => $"uid-{kv.Key}"));

    public Task<string> RetrieveAsync(int id, CancellationToken ct = default)
        => Task.FromResult(_messages.TryGetValue(id, out var m) ? m : throw new KeyNotFoundException());

    public Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _messages.Remove(id);
        return Task.CompletedTask;
    }
}

var listener = new TcpListener(IPAddress.Loopback, 1100);
listener.Start();
TcpClient tcp = await listener.AcceptTcpClientAsync();

using Pop3Server pop3 = new(new MemoryMailbox());
await pop3.StartAsync(tcp.GetStream());
await pop3.Completion;
```

## NntpServer example

Implement `INntpArticleStore` to back the newsgroup data. The server handles `GROUP`, `LIST`, `ARTICLE`, `HEADER`, `BODY`, `POST`, `NEXT`, and `QUIT`.

```csharp
using System.Net;
using System.Net.Sockets;
using Utils.Net;

class MemoryArticleStore : INntpArticleStore
{
    private readonly Dictionary<string, (DateTime Created, Dictionary<int, string> Articles)> _groups = new()
    {
        ["comp.test"] = (DateTime.UtcNow, new() { [1] = "From: a@b\r\nSubject: Test\r\n\r\nHello!" })
    };
    private int _nextId = 2;

    public Task<IReadOnlyCollection<string>> ListGroupsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<string>>(_groups.Keys.ToList());

    public Task<DateTime?> GetGroupCreationDateAsync(string group, CancellationToken ct = default)
        => Task.FromResult(_groups.TryGetValue(group, out var g) ? (DateTime?)g.Created : null);

    public Task<IReadOnlyDictionary<int, string>> ListAsync(string group, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<int, string>>(
            _groups.TryGetValue(group, out var g) ? g.Articles : new Dictionary<int, string>());

    public Task<IReadOnlyCollection<int>> ListNewsSinceAsync(string group, DateTime sinceUtc, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<int>>(new List<int>());

    public Task<string?> RetrieveAsync(string group, int id, CancellationToken ct = default)
        => Task.FromResult(_groups.TryGetValue(group, out var g) && g.Articles.TryGetValue(id, out var a) ? a : (string?)null);

    public Task<int> AddAsync(string group, string article, CancellationToken ct = default)
    {
        if (!_groups.TryGetValue(group, out var g)) throw new KeyNotFoundException();
        int id = _nextId++;
        g.Articles[id] = article;
        return Task.FromResult(id);
    }
}

var listener = new TcpListener(IPAddress.Loopback, 1190);
listener.Start();
TcpClient tcp = await listener.AcceptTcpClientAsync();

using NntpServer nntp = new(new MemoryArticleStore());
await nntp.StartAsync(tcp.GetStream());
await nntp.Completion;
```

## CommandResponseClient — custom text protocol

`CommandResponseClient` provides the generic engine behind `SmtpClient`, `Pop3Client`, and `NntpClient`. Inherit from it to build a client for any line-oriented, numeric-code protocol.

```csharp
using Utils.Net;

public class FingerClient : CommandResponseClient
{
    public override int DefaultPort => 79;

    public async Task<string> QueryAsync(string user)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync(user);
        return string.Join("\n", responses.Select(r => r.Message ?? r.Code));
    }
}

// Usage
await using FingerClient finger = new();
await finger.ConnectAsync("example.com");
string info = await finger.QueryAsync("alice");
Console.WriteLine(info);
```

## CommandResponseServer — custom text protocol server

`CommandResponseServer` is the server-side counterpart. Use `RegisterCommand` to map verbs to handlers. Contexts let you guard commands that require a prior step (e.g., authentication before data access).

```csharp
using System.Net;
using System.Net.Sockets;
using Utils.Net;

var listener = new TcpListener(IPAddress.Loopback, 7979);
listener.Start();
TcpClient tcp = await listener.AcceptTcpClientAsync();

using CommandResponseServer server = new();

server.RegisterCommand("HELLO", (ctx, args) =>
{
    ctx.Add("GREETED");
    string name = args.Length > 0 ? args[0] : "stranger";
    return Task.FromResult<IEnumerable<ServerResponse>>(
        [new ServerResponse("200", ResponseSeverity.Completion, $"Hi, {name}!")]);
});

server.RegisterCommand("SECRET", (ctx, args) =>
{
    return Task.FromResult<IEnumerable<ServerResponse>>(
        [new ServerResponse("200", ResponseSeverity.Completion, "42")]);
}, requiredContexts: "GREETED"); // only allowed after HELLO

server.RegisterCommand("QUIT", (ctx, args) =>
{
    return Task.FromResult<IEnumerable<ServerResponse>>(
        [new ServerResponse("221", ResponseSeverity.Completion, "Bye")]);
});

await server.StartAsync(tcp.GetStream());
await server.Completion;
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
