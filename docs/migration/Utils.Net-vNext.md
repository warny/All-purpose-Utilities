# omy.Utils.Net — Migration Notes (vNext)

This document describes breaking changes introduced in the next major release of `omy.Utils.Net`
that require callers to update their code.

---

## ArpPacket — read-only hardware/protocol fields

### What changed

`ArpPacket.HardwareType`, `ArpPacket.ProtocolType`, `ArpPacket.HardwareAddressLength`, and
`ArpPacket.ProtocolAddressLength` were previously writable properties. They are now read-only
expression-bodied properties that always return the fixed Ethernet/IPv4 constants.

### Why

`ArpPacket` is specialized for Ethernet over IPv4. The previous writable setters allowed callers
to set values that the serializer cannot represent; the type would accept the mutation but then
throw at `ToBytes()` time. Making the fields read-only expresses the invariant in the API itself
and eliminates an entire class of invalid states at compile time.

### How to migrate

Remove any assignments to these properties. If you need to construct packets for other hardware or
protocol types, use a different serialization approach — `ArpPacket` only supports the
Ethernet/IPv4 binding.

**Before:**

```csharp
var packet = new ArpPacket();
packet.HardwareType = 1;      // was allowed, now compile error
packet.ProtocolType = 0x0800; // was allowed, now compile error
```

**After:**

```csharp
var packet = new ArpPacket();
// HardwareType and ProtocolType are always 1 and 0x0800 — no assignment needed.
```

---

## ArpPacket — Operation validation in ToBytes() and Read()

### What changed

`ArpPacket.ToBytes()` now throws `InvalidOperationException` when `Operation` is not
`ArpOperation.Request` (1) or `ArpOperation.Reply` (2).

`ArpPacket.Read()` now throws `InvalidDataException` when the operation field in the wire-format
data is not 1 or 2.

### Why

The previous implementation silently accepted and forwarded unsupported operation codes. The only
operations defined for Ethernet/IPv4 ARP are Request and Reply; other values are not valid for
this binding.

### How to migrate

Ensure `Operation` is set to `ArpOperation.Request` or `ArpOperation.Reply` before calling
`ToBytes()`. When parsing untrusted data, wrap `Read()` in a `try/catch` for
`InvalidDataException`.

---

## DNSHeader.Append — removed in vNext

### What changed

`DNSHeader.Append(DNSHeader)` has been removed. Use `DNSHeader.MergeRecordsFrom(DNSHeader)` instead.

### Why

`Append` had ambiguous semantics around flag handling. `MergeRecordsFrom` enforces a precise merge
contract: both headers must agree on all semantic flag fields (QrBit, OpCode, ErrorCode,
AuthoritativeAnswer, MessageTruncated, AuthenticDatas, CheckingDisabled, RecursionDesired,
RecursionPossible, ReservedFlags). Only distinct records from the source are added to the target.
The merge is atomic: all clones are prepared before any collection is mutated.

### How to migrate

Replace `a.Append(b)` with `a.MergeRecordsFrom(b)`. Ensure both headers carry identical flag values
before calling `MergeRecordsFrom`, as differing flags now throw `InvalidOperationException`.

---

## NetworkParameters — DNS server ordering follows OS enumeration order

### What changed

`NetworkParameters.SelectDnsServers` previously sorted active interfaces — favouring those with a
default gateway and using `IPv4InterfaceProperties.Index` as a tiebreaker. Both of these sort
keys have been removed.

DNS servers are now collected in strict OS enumeration order:
1. Iterate `NetworkInterface.GetAllNetworkInterfaces()` in the order the OS provides.
2. Include every `OperationalStatus.Up` interface, regardless of whether it has a default gateway.
3. Within each interface, preserve the order of `DnsAddresses`.
4. Exclude wildcard/unspecified and multicast addresses.
5. Deduplicate, keeping the first-seen occurrence.

### Why

The previous "gateway-first" heuristic silently broke split-DNS, VPN, and point-to-point
configurations where the most specific resolver is reachable through an interface without a
default gateway. A VPN resolver listed first by the OS would be moved after a public DNS server
on an Ethernet interface that happened to have a default gateway, causing all internal names to
resolve through the wrong server.

OS enumeration order is the closest approximation to what the system administrator or
VPN software intended.

### Impact

The order of DNS servers returned by `DnsServers` and `PrimaryDns` may change on machines with
multiple active network interfaces. If your application relied on the previous gateway-first
ordering, review whether the new OS-enumeration order is correct for your network topology.

### Note on VPN and split-DNS

If a VPN interface appears first in OS enumeration, its resolver is now correctly prioritised.
A negative DNS response from that resolver (NXDOMAIN) is a valid response and prevents fallback
to a subsequent public resolver for the same name — which is the expected split-DNS behaviour.

---

## NTP — internal transport changes (no migration required)

The UDP transport used by `NtpClient` (`UdpNtpTransport`) is an internal implementation detail.
Its constructor and `ExchangeAsync` method are not part of the public API. No migration is required
for callers of the public `NtpClient.GetTimeAsync` overloads.

---

## DnsLookupException and NtpQueryException — failure list is now defensively copied

### What changed

The `Failures` property of `DnsLookupException` and `NtpQueryException` now stores an internal
copy of the list passed to the constructor. Mutating the original list after constructing the
exception no longer affects `Failures`.

### Why

Exceptions are intended to be immutable once constructed. Exposing a reference to the caller's
list violated that contract and could cause non-deterministic behaviour if the list was mutated
after the exception was thrown.

### Impact

This is a behavioral fix, not a signature change. Code that reads `Failures` is unaffected.
Code that relies on the exception reflecting post-construction mutations to the original list
must be updated — though such patterns were never correct.

---

## SMTP, POP3, and NNTP protocol contracts in 2.0

SMTP mailbox strings are now parsed strictly by `SmtpPath`. Angle brackets, whitespace, controls, source routes, and embedded ESMTP parameters are rejected. SMTPUTF8 requires opt-in both while parsing and in `SmtpMailOptions`. SASL PLAIN and LOGIN use strict UTF-8 and reject NUL in each credential field.

An SMTP mail transaction and an AUTH LOGIN challenge sequence now hold one exclusive exchange lease. After `MAIL FROM`, a framed failure is recovered with verified `RSET` under `TransactionRecoveryTimeout` (five seconds by default). Failure after DATA acceptance or failed RSET poisons the session.

`ProtocolResponseException` replaces generic response-message `IOException` failures and exposes protocol, sanitized verb, code, severity, immutable response lines, and enhanced status. A normal framed negative response does not poison the connection; cancellation, EOF, framing loss, streaming consumer failure, and multiline limit overruns do.

POP3 STAT/LIST/UIDL and NNTP GROUP/LIST/STAT/NEXT now reject missing, extra, overflowing, negative, or duplicate mandatory values. `NextAsync` returns `null` only for NNTP 421. `NewNewsAsync` returns NNTP message-id strings rather than integers.

Streaming overloads accept `TextWriter` for POP3 RETR and NNTP ARTICLE/HEADER/BODY. Materializing wrappers use these bounded streaming paths. Defaults are 100,000 lines, 10 Mi characters, and 40 MiB (UTF-8 count).
