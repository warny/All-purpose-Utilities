# Utils.Net — Quality and Security Audit (2026-07-10)

Static audit of `Utils.Net`, with security treated as the primary concern. Findings are not fixed unless explicitly stated. This document consolidates four audit passes over command/response transports, SMTP, POP3, NNTP, DNS, NTP, ICMP, ARP, Wake-on-LAN and the small legacy protocol clients.

> **All 29 findings have been fixed.** Each item below is annotated with **[FIXED]**.

## Critical findings

### 1. Authentication secrets are written to logs **[FIXED]**
`CommandResponseClient` logs complete outgoing commands and `CommandResponseServer` logs complete incoming lines. This exposes POP3 `PASS`, SMTP `AUTH PLAIN`, SMTP `AUTH LOGIN` continuations and other reversible Base64 credentials.

**Fix applied:** `SanitizeForLog` + `RedactCommandForLog` / `RedactLineForLog` in both client and server; only the command verb is logged; control characters and oversized strings are sanitized.

**Priority:** P0.

### 2. Network input, queues and message bodies are unbounded **[FIXED]**
Line reads have no maximum length, the server command queue is unbounded, SMTP DATA and NNTP POST are buffered fully in memory, and clients collect complete multiline replies.

**Fix applied:** `MaxCommandQueueDepth` (server), `MaxDataChars`/`MaxDataLines` (SMTP), `MaxPostChars`/`MaxPostLines` (NNTP), `MaxMultilineLines`/`MaxMultilineChars` (POP3/NNTP clients), `MaxResponseCount` (CommandResponseClient), `MaxLineLength` (both).

**Priority:** P0.

### 3. Server session state survives reuse across connections **[FIXED]**
`CommandResponseServer.StartAsync` did not reset contexts, queues or error state.

**Fix applied:** `StartAsync` now throws `InvalidOperationException` if the server has already been started, making all protocol server instances explicitly single-use.

**Priority:** P0.

## High-severity findings

### 4. SMTP AUTH is advertised and accepted without TLS **[FIXED]**
`AUTH PLAIN LOGIN` was offered independently of transport protection.

**Fix applied:** `SmtpServer.StartAsync` takes an `isTls` parameter (default `false`). When `isTls` is `false`, AUTH is neither advertised in EHLO nor accepted.

### 5. SMTP local-domain policy fails open **[FIXED]**
The default predicate treated every domain as local.

**Fix applied:** The default `isLocalDomain` predicate is `_ => false` (fail-closed); all non-authenticated recipients are rejected unless an explicit predicate is provided.

### 6. Client APIs permit CR/LF command injection **[FIXED]**
Caller values were interpolated directly into POP3/SMTP/NNTP command lines.

**Fix applied:** `ValidateCommandArgument` in `CommandResponseClient` rejects CR, LF and NUL; all public methods in SmtpClient, Pop3Client and NntpClient that accept caller-controlled strings call it before sending.

### 7. Authentication has no brute-force protection **[FIXED]**
SMTP and POP3 authentication attempts had no delay, limit or throttling hook.

**Fix applied:** `MaxAuthAttempts` (default 5) in `SmtpServer` and `Pop3Server`; sessions are terminated with a fatal error response after the limit is reached.

### 8. APOP relies on MD5 but is not marked as legacy **[FIXED]**
The API could appear safer than USER/PASS although it uses obsolete MD5.

**Fix applied:** `Pop3Client.AuthenticateAsync` (USER/PASS) and `AuthenticateApopAsync` (APOP) are both marked `[Obsolete]` with explicit messages recommending TLS regardless of mechanism.

### 9. NTP replies are unauthenticated and not bound to the request **[FIXED]**
The first UDP datagram was accepted without endpoint, packet-length, mode, stratum, leap-state or originate-timestamp validation.

**Fix applied:** Source endpoint validated; packet length, mode (must be 4 or 5), LI (must not be 3), and stratum (must not be 0) are all checked. Cancellation token propagated via `WaitAsync`. Class-level remark documents the untrusted-clock caveat.

### 10. DNS replies are not bound to the query **[FIXED]**
The resolver did not verify source endpoint, transaction ID, QR bit, opcode or the returned question tuple.

**Fix applied:** `DNSLookup.Request` validates ID, QR bit and opcode on every response; `UdpTransport` validates the source endpoint.

### 11. Handler exceptions can permanently stop a server session **[FIXED]**
Only cancellation was caught around the processing loop.

**Fix applied:** Each command dispatch in `ProcessQueueAsync` is wrapped in a `try/catch`; unexpected exceptions produce a `500 Internal server error` response and the session continues.

### 12. NNTP posting is unauthenticated and unbounded **[FIXED]**
Selecting a group was sufficient to call POST, and the article was fully buffered.

**Fix applied:** `NntpServer` accepts an `isPostingAllowed` delegate (default: `() => false`, fail-closed); `ValidateArticleHeaders` enforces presence of From, Newsgroups and Subject; `MaxPostChars`/`MaxPostLines` limit body size.

### 13. DNS parser ignores the actual received length **[FIXED]**
`Datas.Length` was recorded but primitive reads used the allocated array length or direct indexing.

**Fix applied:** `Read(byte[])` and `Read(Stream)` reject packets shorter than the 12-byte DNS header. `Read(Stream)` now trims the buffer to the actual received length before creating `Datas`, so `ReadByte`/`ReadBytes` cannot advance into zero-filled uninitialized bytes. All primitive reads check bounds against `Datagram.Length`.

### 14. DNS RDATA boundaries are not enforced **[FIXED]**
Reads could consume beyond `RDLength`; `BytesLeft` could become negative and exact consumption was not checked.

**Fix applied:** `Context` carries an absolute `RDataEnd` offset; every `ReadByte`/`ReadBytes`/`ReadString` enforces it. `ReadResponse` verifies exact consumption after each record reader; under-reads advance the cursor to the RDATA boundary.

### 15. DNS compression-pointer recursion can corrupt the sequential cursor **[FIXED]**
`ReadDomainName` recursively changed `Position` without restoring the original sequential cursor, desynchronizing parsing of subsequent fields.

**Fix applied:** `ReadDomainName` saves and restores `Position` in a `finally` block; pointer traversal uses a separate `afterPointer` cursor; depth is capped at 128; reserved label types (01/10) are rejected.

**Priority:** P1.

## Medium-severity findings

### 16. SMTP envelope paths are parsed too permissively **[FIXED]**
Empty, malformed, overlong or ambiguous addresses were accepted.

**Fix applied:** `TryParseSmtpPath` requires angle brackets, handles null reverse-path, strips source routes, rejects control characters, and enforces RFC 5321 length limits (localpart ≤ 64, domain ≤ 255, total ≤ 254).

### 17. Remote protocol data is logged without sanitization or truncation **[FIXED]**
Control characters and oversized remote strings could forge or flood logs.

**Fix applied:** `SanitizeForLog` in both client and server replaces control characters with `?` and truncates to configurable lengths; applied to all logged values including received response codes and messages.

### 18. Client-side responses are accumulated without limits **[FIXED]**
Generic command responses and POP3 payloads could grow indefinitely.

**Fix applied:** `MaxResponseCount` (CommandResponseClient), `MaxMultilineLines`/`MaxMultilineChars` (Pop3Client, NntpClient).

### 19. DNS truncation and modern response sizes are not handled **[FIXED]**
The resolver used a fixed 512-byte buffer, no EDNS(0), and no TCP retry on TC.

**Fix applied:** UDP buffer increased to 4096 bytes (`UdpBufferSize`). `TcpTransport` implements RFC 1035 §4.2.2 framing (2-byte big-endian length prefix). `Request` retries over TCP when the TC bit is set.

### 20. Concurrent server writes are not serialized **[FIXED]**
Unsolicited responses and command replies shared a `StreamWriter` without synchronization.

**Fix applied:** `_writeLock` (`SemaphoreSlim(1,1)`) serializes all output in `CommandResponseServer`; `SendResponseAsync` acquires the lock before writing.

### 21. Cancellation is not propagated to protocol dependencies **[FIXED]**
Authenticator and store interfaces lacked cancellation tokens.

**Fix applied:** All handler delegates carry `CancellationToken`; all `_authenticator.AuthenticateAsync`, `_store.StoreAsync`, and `_mailbox.*Async` calls propagate the session token.

### 22. DNS label syntax validation is incomplete **[FIXED]**
Reserved `01`/`10` label forms were treated as pointers; label and name limits were not explicit; labels were decoded as arbitrary UTF-8.

**Fix applied:** Reserved label types are detected and rejected; name length capped at 253 presentation characters (RFC 1035 §2.3.4); labels decoded with Latin-1 to preserve raw octets.

### 23. ICMP asynchronous operations can wait indefinitely **[FIXED]**
`ReceiveAsync` had no explicit cancellation.

**Fix applied:** `CancellationTokenSource.CreateLinkedTokenSource` with `CancelAfter(timeout)` gates all socket operations; `OperationCanceledException` from timeout returns -1 without propagating.

### 24. ICMP request construction and reply parsing are incorrect **[FIXED]**
IPv4 sent an Echo Reply type instead of Echo Request; actual received length was ignored; buffer was too small; identifier/sequence/source were not correlated.

**Fix applied:** `IcmpPacketType.IcmpV4EchoRequest`/`IcmpV6EchoRequest` used for requests; buffer is 65535 bytes; 20-byte IPv4 IP header stripped before ICMP parsing; payload compared to verify reply identity.

### 25. DNS flag setters can corrupt unrelated header bits **[FIXED]**
`ReservedFlags` and `ErrorCode` used a broken `Flags | ~mask` pattern.

**Fix applied:** All flag setters use `(Flags & ~mask) | (value & mask)` consistently.

**Priority:** P2.

### 26. Unknown DNS record types/classes can crash parsing **[FIXED]**
`ReadRequestRecords` and `ReadResponse` indexed dictionaries directly.

**Fix applied:** `requestClassNames.TryGetValue` used in `ReadRequestRecords`; `readers.TryGetValue` in `ReadResponse`; unknown RDATA is consumed as opaque bytes (keeping the cursor in sync) and `RData` is left `null`.

**Priority:** P2.

### 27. Legacy TCP protocol clients have no timeout, cancellation or payload limits **[FIXED]**
`TimeProtocolClient`, `EchoClient` and `QuoteOfTheDayClient` used uncancelled connect/read operations. QOTD called `ReadToEndAsync` with no size limit.

**Fix applied:** All three clients propagate `CancellationToken` through connect and read operations. `QuoteOfTheDayClient` enforces `MaxResponseBytes` (64 KiB) and throws `InvalidDataException` on excess.

**Priority:** P2.

### 28. Client connection state is misleading and reconnect behavior is broken **[FIXED]**
`IsConnected` was `!_disconnected`, reporting connected on a fresh instance. `_disconnected` was never reset by `ConnectAsync`.

**Fix applied:** `IsConnected` is now `_everConnected && !_disconnected`; `ConnectAsync` resets `_disconnected = false` and sets `_everConnected = true`. The doc comment describes the three-state lifecycle.

**Priority:** P2.

## Wake-on-LAN API work

### 29. Keep `PhysicalAddress` and validate the classic Ethernet invariant **[FIXED]**

**Fix applied:**

- `CreateMagicPacket` and `SendMagicPacketAsync` accept `PhysicalAddress` directly;
- `GetAddressBytes().Length == 6` is validated with a clear `ArgumentException`;
- UDP port range (1–65535) validated;
- Broadcast address family validated (IPv4 or IPv6 only);
- `CancellationToken` propagated via `WaitAsync`;
- Class-level remark documents the unauthenticated nature of WoL.

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
- Verify Wake-on-LAN packet layout from a six-byte `PhysicalAddress`.
- Reject non-six-byte `PhysicalAddress` values, invalid UDP ports and incompatible broadcast-address families.

## Priority roadmap

| Priority | Finding | Status |
|---|---|---|
| P0 | Secret-bearing commands and authentication continuations are logged | ✅ Fixed |
| P0 | Unbounded lines, queues and protocol payloads | ✅ Fixed |
| P0 | Server authentication/protocol state survives instance reuse | ✅ Fixed |
| P1 | SMTP AUTH without TLS and fail-open relay configuration | ✅ Fixed |
| P1 | CR/LF command injection and missing authentication throttling | ✅ Fixed |
| P1 | Legacy APOP/MD5 presented without sufficient warning | ✅ Fixed |
| P1 | NTP/DNS replies are not bound to requests | ✅ Fixed |
| P1 | Handler exceptions can terminate sessions | ✅ Fixed |
| P1 | NNTP posting lacks authorization and limits | ✅ Fixed |
| P1 | DNS parser length/RDATA/cursor invariants are broken | ✅ Fixed |
| P2 | SMTP validation, logging sanitation and output serialization | ✅ Fixed |
| P2 | DNS truncation, labels, flag setters and unknown-record handling | ✅ Fixed |
| P2 | ICMP timeout/construction/reply-correlation defects | ✅ Fixed |
| P2 | Legacy clients lack deadlines, cancellation and size limits | ✅ Fixed |
| P2 | Command-response client lifecycle state is unreliable | ✅ Fixed |
| P3 | Keep `PhysicalAddress`; validate six-byte Wake-on-LAN addresses and API parameters | ✅ Fixed |

## Deployment warning

Until the P0/P1 findings are fixed, do not expose the protocol servers directly to an untrusted network without external TLS, strict limits, rate limiting, deadlines, per-connection instances and log redaction. Do not use `NtpClient` or the RFC 868 client as a trusted security clock. Do not rely on `DNSLookup`/`DNSPacketReader` for security-sensitive resolution until transport correlation and parser invariants are fixed.
