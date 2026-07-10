# Utils.Net — Quality and Security Audit (2026-07-10)

Static audit of `Utils.Net`, with security treated as the primary concern. Findings are not fixed unless explicitly stated. This document consolidates four audit passes over command/response transports, SMTP, POP3, NNTP, DNS, NTP, ICMP, ARP, Wake-on-LAN and the small legacy protocol clients.

## Critical findings

### 1. Authentication secrets are written to logs
`CommandResponseClient` logs complete outgoing commands and `CommandResponseServer` logs complete incoming lines. This exposes POP3 `PASS`, SMTP `AUTH PLAIN`, SMTP `AUTH LOGIN` continuations and other reversible Base64 credentials.

**Fix:** add protocol-aware redaction; never log secret-bearing arguments or authentication continuations; make raw tracing explicit opt-in and still redacted.

**Priority:** P0.

### 2. Network input, queues and message bodies are unbounded
Line reads have no maximum length, the server command queue is unbounded, SMTP DATA and NNTP POST are buffered fully in memory, and clients collect complete multiline replies.

**Fix:** bounded line readers, bounded channels, byte/line/count/time limits, streaming APIs and deterministic disconnect on limit violation.

**Priority:** P0.

### 3. Server session state survives reuse across connections
`CommandResponseServer.StartAsync` does not reset contexts, queues or error state. SMTP authentication/relay flags, POP3 deletion marks and NNTP selected-group/posting state are stored on reusable server instances.

**Fix:** make instances explicitly single-use or move all state into a fresh per-connection session object. Test sequential reuse with two streams.

**Priority:** P0.

## High-severity findings

### 4. SMTP AUTH is advertised and accepted without TLS
`AUTH PLAIN LOGIN` is offered whenever an authenticator exists, independently of transport protection.

**Fix:** make TLS state explicit and refuse cleartext authentication by default.

### 5. SMTP local-domain policy fails open
The default predicate treats every domain as local.

**Fix:** require an explicit policy or default to rejection.

### 6. Client APIs permit CR/LF command injection
Caller values are interpolated directly into POP3/SMTP/NNTP command lines.

**Fix:** reject CR, LF, NUL and prohibited control characters in every line argument; add protocol-specific validators.

### 7. Authentication has no brute-force protection
SMTP and POP3 authentication attempts have no delay, limit or external throttling hook.

**Fix:** add per-connection limits and an injectable rate-limiting policy keyed by endpoint and account.

### 8. APOP relies on MD5 but is not marked as legacy
The API can appear safer than USER/PASS although it uses obsolete MD5.

**Fix:** mark it obsolete/compatibility-only and recommend TLS regardless of mechanism.

### 9. NTP replies are unauthenticated and not bound to the request
The first UDP datagram is accepted without endpoint, packet-length, mode, stratum, leap-state or originate-timestamp validation. There is no timeout or cancellation.

**Fix:** bind the UDP socket, validate the complete response, add timeout/cancellation and document that unauthenticated NTP is not a trusted security clock.

### 10. DNS replies are not bound to the query
The resolver does not verify source endpoint, transaction ID, QR bit, opcode or the returned question tuple.

**Fix:** validate all query/response correlation fields and reject reserved or inconsistent flags.

### 11. Handler exceptions can permanently stop a server session
Only cancellation is caught around the processing loop. Exceptions from authenticators, stores, formatters or handlers escape.

**Fix:** isolate each command dispatch, map expected failures to protocol replies and close deterministically on unexpected faults.

### 12. NNTP posting is unauthenticated and unbounded
Selecting a group is sufficient to call POST, and the article is fully buffered.

**Fix:** add authorization, limits, required-header validation and streaming/spooling.

### 13. DNS parser ignores the actual received length
`Datas.Length` is recorded but primitive reads use the allocated array length or direct indexing.

**Fix:** every primitive read must check the actual datagram length; reject packets shorter than the DNS header.

### 14. DNS RDATA boundaries are not enforced
Reads can consume beyond `RDLength`; `BytesLeft` may become negative and exact consumption is not checked.

**Fix:** maintain an immutable RDATA end offset and enforce it on every sequential read.

### 15. DNS compression-pointer recursion can corrupt the sequential cursor
When a compression pointer targets a name that is not already cached, `ReadDomainName` recursively changes `Position` but does not restore the original sequential cursor on that return path. Later fields can therefore be parsed from the pointer target rather than from the bytes following the pointer.

**Risk:** crafted packets can desynchronize parsing, reinterpret unrelated bytes as record fields, or trigger denial of service.

**Fix:** separate the sequential cursor from pointer-target traversal; always restore the caller cursor in a `finally`; track visited offsets in addition to a depth limit.

**Priority:** P1.

## Medium-severity findings

### 16. SMTP envelope paths are parsed too permissively
Empty, malformed, overlong or ambiguous addresses are accepted.

**Fix:** implement a deliberately limited strict SMTP-path parser with explicit null reverse-path support.

### 17. Remote protocol data is logged without sanitization or truncation
Control characters and oversized remote strings can forge or flood logs.

**Fix:** sanitize, truncate and lower raw protocol content to Debug/Trace.

### 18. Client-side responses are accumulated without limits
Generic command responses and POP3 payloads can grow indefinitely.

**Fix:** byte/count limits and streaming retrieval.

### 19. DNS truncation and modern response sizes are not handled
The resolver uses a fixed 512-byte buffer, no EDNS(0), and no TCP retry on TC.

**Fix:** bounded EDNS payload support and TCP fallback.

### 20. Concurrent server writes are not serialized
Unsolicited responses and command replies share a `StreamWriter` without one output queue/lock.

**Fix:** serialize all output through one bounded writer pipeline.

### 21. Cancellation is not propagated to protocol dependencies
Authenticator and store interfaces generally lack cancellation tokens.

**Fix:** add cancellation-aware overloads and propagate the session token.

### 22. DNS label syntax validation is incomplete
Reserved `01`/`10` label forms are treated as pointers; 63-byte label and 255-byte name limits are not explicit; labels are decoded as arbitrary UTF-8.

**Fix:** accept pointers only for `11xxxxxx`, reject reserved forms, enforce limits and preserve raw label octets until presentation.

### 23. ICMP asynchronous operations can wait indefinitely
`ReceiveTimeout` is set but task-based `ReceiveAsync` has no explicit cancellation.

**Fix:** use cancellation-aware socket calls with a linked timeout token.

### 24. ICMP request construction and reply parsing are incorrect
IPv4 sends an Echo Reply type instead of Echo Request, ignores the actual received length, assumes a too-small buffer and does not correlate identifier/sequence/source.

**Fix:** correct the packet type, parse only received bytes, account for IP headers and validate reply identity.

### 25. DNS flag setters can corrupt unrelated header bits
`DNSHeader.ReservedFlags` and `DNSHeader.ErrorCode` use expressions based on `Flags | ~mask` followed by `& value`. This is not the standard masked replacement operation and can clear unrelated flag bits when setting Z/RCODE values.

**Risk:** a header assembled or modified by the library may silently lose QR, opcode, recursion, AD/CD or other flags. Any future response-validation logic that mutates these fields can become unreliable.

**Fix:** use `(Flags & ~mask) | (newValue & mask)` consistently and add exhaustive bit-preservation tests.

**Priority:** P2.

### 26. Unknown DNS record types/classes can crash parsing
`ReadRequestRecords` and `ReadResponse` index `requestClassNames` and `readers` directly. Unknown but valid extension records, unsupported classes or malformed values raise `KeyNotFoundException` instead of being represented as opaque RDATA or rejected cleanly.

**Risk:** a remote DNS server can reliably cause lookup failure; a single unsupported additional record can discard an otherwise useful answer.

**Fix:** use `TryGetValue`; preserve unknown records as bounded opaque RDATA; distinguish unsupported records from malformed packets.

**Priority:** P2.

### 27. Legacy TCP protocol clients have no timeout, cancellation or payload limits
`TimeProtocolClient`, `EchoClient` and `QuoteOfTheDayClient` use uncancelled connect/read operations. QOTD calls `ReadToEndAsync` with no size limit; Echo waits for exactly the sent byte count unless the peer closes.

**Risk:** untrusted or faulty peers can retain sockets/tasks indefinitely or exhaust memory.

**Fix:** add cancellation tokens, connect/read deadlines and explicit maximum response sizes.

**Priority:** P2.

### 28. Client connection state is misleading and reconnect behavior is broken
`CommandResponseClient.IsConnected` is `!_disconnected`, so a newly constructed client reports connected before any stream exists. After listener termination `_disconnected` is never reset by `ConnectAsync`, and disposal permanently disposes synchronization primitives.

**Risk:** consumers may make authorization or workflow decisions from an invalid state indicator; attempted reuse fails in surprising ways.

**Fix:** model explicit states (`Created`, `Connecting`, `Connected`, `Disconnecting`, `Disposed`), make the client single-use or implement a complete reset, and derive `IsConnected` from that state.

**Priority:** P2.

## Strong type and API design work

### 29. Introduce a dedicated `MacAddress` specific type
`PhysicalAddress` accepts arbitrary byte lengths and therefore cannot enforce the six-byte invariant required by classic Ethernet MAC addresses and Wake-on-LAN magic packets.

Create a dedicated immutable strong type, preferably generated through the planned `Specific<T>` source-generator mechanism:

```csharp
public readonly partial struct MacAddress : ISpecific<PhysicalAddress>;
```

The generated/custom validation must guarantee exactly six bytes at construction and parsing time. The type should provide:

- canonical `XX:XX:XX:XX:XX:XX` formatting;
- parsing of colon, hyphen and compact hexadecimal forms;
- value equality and stable hashing based on the six bytes;
- conversion to `PhysicalAddress` and `ReadOnlySpan<byte>` without exposing mutable state;
- rejection of multicast/broadcast addresses only where a specific API requires a unicast address, not globally;
- an explicit broadcast constant where useful.

Update `WakeOnLan`, `ArpUtils`, `ArpPacket` and other Ethernet-specific APIs to accept `MacAddress`. Keep compatibility overloads taking `PhysicalAddress` only if they validate and delegate to the strong type.

**Priority:** P3 design improvement, but it should be implemented with the Wake-on-LAN validation fix.

### 30. Wake-on-LAN parameters still require runtime validation
Even after introducing `MacAddress`, validate UDP port range, broadcast-address family and cancellation. Document that Wake-on-LAN is unauthenticated and should not be treated as an authorization mechanism.

**Priority:** P3.

## Required regression and security tests

- Verify that passwords, AUTH payloads and continuation credentials never appear in logs.
- Reject CR/LF/NUL injection in every line-oriented public API.
- Fuzz oversized lines, command floods, endless SMTP DATA, endless NNTP POST and multiline client replies.
- Verify TLS-gated SMTP authentication and fail-closed local-domain configuration.
- Test authentication throttling and session-state isolation across reused instances.
- Reject spoofed/malformed NTP and DNS replies; test timeout/cancellation and DNS TCP fallback.
- Fuzz DNS with truncated headers, inflated counts, invalid RDLENGTH, reserved labels, pointer loops, uncached pointers and cross-RDATA reads.
- Exhaustively verify that each DNS flag setter preserves all unrelated bits.
- Parse unknown DNS types/classes as bounded opaque records without losing the complete response.
- Verify handler exceptions and concurrent output behavior.
- Test ICMP cancellation, request type, received-length parsing and reply correlation.
- Test cancellation and maximum-response limits for Time, Echo and QOTD clients.
- Test all accepted `MacAddress` textual forms, equality, formatting and rejection of non-six-byte values.
- Verify Wake-on-LAN packet layout using `MacAddress` and reject invalid ports/address families.

## Priority roadmap

| Priority | Finding |
|---|---|
| P0 | Secret-bearing commands and authentication continuations are logged |
| P0 | Unbounded lines, queues and protocol payloads |
| P0 | Server authentication/protocol state survives instance reuse |
| P1 | SMTP AUTH without TLS and fail-open relay configuration |
| P1 | CR/LF command injection and missing authentication throttling |
| P1 | Legacy APOP/MD5 presented without sufficient warning |
| P1 | NTP/DNS replies are not bound to requests |
| P1 | Handler exceptions can terminate sessions |
| P1 | NNTP posting lacks authorization and limits |
| P1 | DNS parser length/RDATA/cursor invariants are broken |
| P2 | SMTP validation, logging sanitation and output serialization |
| P2 | DNS truncation, labels, flag setters and unknown-record handling |
| P2 | ICMP timeout/construction/reply-correlation defects |
| P2 | Legacy clients lack deadlines, cancellation and size limits |
| P2 | Command-response client lifecycle state is unreliable |
| P3 | Introduce `MacAddress` and harden Wake-on-LAN API parameters |

## Deployment warning

Until the P0/P1 findings are fixed, do not expose the protocol servers directly to an untrusted network without external TLS, strict limits, rate limiting, deadlines, per-connection instances and log redaction. Do not use `NtpClient` or the RFC 868 client as a trusted security clock. Do not rely on `DNSLookup`/`DNSPacketReader` for security-sensitive resolution until transport correlation and parser invariants are fixed.