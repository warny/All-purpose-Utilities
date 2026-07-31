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

## DNSHeader.Append — obsolete, prefer MergeRecordsFrom

### What changed

`DNSHeader.Append(DNSHeader)` is now marked `[Obsolete]`. Use `DNSHeader.MergeRecordsFrom(DNSHeader)`
instead.

### Why

`Append` has ambiguous semantics around flag handling. `MergeRecordsFrom` documents the precise
merge policy: target flags (ID, ErrorCode, AuthoritativeAnswer, etc.) are always preserved; only
distinct records from the source are added.

### How to migrate

Replace `a.Append(b)` with `a.MergeRecordsFrom(b)`.

---

## NetworkParameters — DNS server ordering no longer uses interface index as metric

### What changed

`NetworkParameters.SelectDnsServers` previously sorted interfaces by `IPv4InterfaceProperties.Index`
as a tiebreaker (labelled "metric"). This value is an interface identifier, not a routing priority.
The sort key has been removed; interfaces with the same gateway status are now returned in OS
enumeration order.

### Why

Using the interface index as a routing metric produced incorrect ordering on many systems.
OS enumeration order is a more faithful representation of the system's DNS priority.

### Impact

The relative order of DNS servers from interfaces with the same gateway status may change.
In practice this only affects machines with multiple active network interfaces (e.g. Wi-Fi and
Ethernet simultaneously) where the previously incorrect sort was already unreliable.

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
