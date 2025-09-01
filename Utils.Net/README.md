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
- POP3 client for retrieving e-mail using a command/response model
- SMTP client and server for transmitting e-mail using a command/response model with optional authentication (PLAIN and LOGIN), relay control, VRFY/EXPN/HELP commands and ESMTP extensions

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
var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole());
using var cmdClient = new Utils.Net.CommandResponseClient
{
    NoOpInterval = TimeSpan.FromMinutes(1),
    Logger = loggerFactory.CreateLogger<Utils.Net.CommandResponseClient>()
};
await cmdClient.ConnectAsync(tcp.GetStream());
IReadOnlyList<Utils.Net.ServerResponse> replies = await cmdClient.SendCommandAsync("NOOP");
// Each response line exposes a Severity value allowing callers to inspect
// preliminary, completion or error statuses. The client waits until a line
// with at least completion severity is received before returning all lines.
bool stillConnected = cmdClient.IsConnected;
await cmdClient.DisconnectAsync("QUIT", TimeSpan.FromSeconds(1));

// Build a command/response server with command mapping and contexts
var server = new Utils.Net.CommandResponseServer();
server.MaxConsecutiveErrors = 3; // Shutdown after three consecutive errors
server.RegisterCommand("LOGIN", (ctx, args) =>
{
    ctx.Add("AUTH");
    return System.Threading.Tasks.Task.FromResult<IEnumerable<Utils.Net.ServerResponse>>(
        new[] { new Utils.Net.ServerResponse("200", Utils.Net.ResponseSeverity.Completion, "Logged in") });
});
server.RegisterCommand("LIST", (ctx, args) =>
    System.Threading.Tasks.Task.FromResult<IEnumerable<Utils.Net.ServerResponse>>(
        new[] { new Utils.Net.ServerResponse("200", Utils.Net.ResponseSeverity.Completion, "Listed") }),
    "AUTH");
// Attach logging
server.Logger = loggerFactory.CreateLogger<Utils.Net.CommandResponseServer>();
await server.StartAsync(tcp.GetStream());

// Retrieve messages using the POP3 client
using var pop3 = new Utils.Net.Pop3Client();
await pop3.ConnectAsync("mail.example.com", 110);
await pop3.AuthenticateApopAsync("user", "pass");
IReadOnlyDictionary<int, string> uids = await pop3.ListUniqueIdsAsync();
string firstMessage = await pop3.RetrieveAsync(1);
await pop3.ResetAsync();
await pop3.QuitAsync();

// Host a POP3 server with a custom mailbox
class MemoryMailbox : Utils.Net.IPop3Mailbox
{
    public Task<bool> AuthenticateAsync(string user, string password, CancellationToken token = default) => Task.FromResult(true);
    public Task<bool> AuthenticateApopAsync(string user, string timestamp, string digest, CancellationToken token = default) => Task.FromResult(true);
    public Task<IReadOnlyDictionary<int, int>> ListAsync(CancellationToken token = default) => Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());
    public Task<string> RetrieveAsync(int id, CancellationToken token = default) => Task.FromResult(string.Empty);
    public Task<IReadOnlyDictionary<int, string>> ListUidsAsync(CancellationToken token = default) => Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
    public Task DeleteAsync(int id, CancellationToken token = default) => Task.CompletedTask;
}
var mailbox = new MemoryMailbox();
var pop3Server = new Utils.Net.Pop3Server(mailbox);
await pop3Server.StartAsync(tcp.GetStream());

// Send an e-mail using the SMTP client
using var smtp = new Utils.Net.SmtpClient();
await smtp.ConnectAsync("mail.example.com", 25);
await smtp.EhloAsync("client.example.com");
await smtp.AuthenticateAsync("user", "pass", Utils.Net.SmtpAuthenticationMechanism.Login);
await smtp.HelpAsync();
await smtp.SendMailAsync("alice@example.com", new[] { "bob@example.com" }, "Subject: Hi\r\n\r\nHello");
await smtp.QuitAsync();

// Host an SMTP server with an in-memory store
class MemoryStore : Utils.Net.ISmtpMessageStore
{
    public Task StoreAsync(Utils.Net.SmtpMessage message, CancellationToken token = default) => Task.CompletedTask;
}
class MemoryAuthenticator : Utils.Net.ISmtpAuthenticator
{
    public Task<Utils.Net.SmtpAuthenticationResult> AuthenticateAsync(string user, string password, CancellationToken token = default) =>
        Task.FromResult(new Utils.Net.SmtpAuthenticationResult(true, true));
}
var authenticator = new MemoryAuthenticator();
var store = new MemoryStore();
var smtpServer = new Utils.Net.SmtpServer(store, d => d == "example.com", authenticator);
await smtpServer.StartAsync(tcp.GetStream());
```
