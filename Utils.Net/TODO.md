# Utils.Net — Quality and Security Audit (2026-07-10)

Static audit of the `Utils.Net` package with security treated as the primary concern. The review covers client/server command-response abstractions, SMTP, POP3, NNTP, DNS, NTP, ICMP/ARP and related network helpers. Findings below are not fixed yet unless explicitly stated.

## Critical security findings

### 1. Authentication secrets are written to logs
`CommandResponseClient.SendCommandAsync` logs the full command line, while `CommandResponseServer.ListenLoop` logs the full line received from the peer. This exposes secrets for commands and authentication exchanges such as `PASS`, `AUTH PLAIN`, `AUTH LOGIN`, POP3 `USER`/`PASS`, and Base64-encoded username/password challenge responses. Base64 is reversible encoding, not encryption.

**Risk:** credentials can leak to application logs, consoles, centralized collectors, APM systems and support bundles.

**Proposed fix:** introduce protocol-aware log redaction. Log only the command verb and non-sensitive metadata. Redact all arguments for `PASS`, `AUTH`, `APOP`, and all lines received while an authentication continuation state is active. Move raw protocol tracing to an explicit opt-in diagnostic feature that still redacts secrets.

**Severity:** Critical.

### 2. Network input, queues and message bodies are unbounded
Both client and server read with `StreamReader.ReadLine()` without a maximum line length. `CommandResponseServer` uses an unbounded `ConcurrentQueue<string>`, and `SmtpServer` buffers the entire DATA payload in an unbounded `List<string>`. POP3 and generic command-response clients also accumulate complete multiline responses in memory.

**Risk:** a remote peer can cause memory exhaustion by sending a huge unterminated line, flooding commands faster than they are processed, sending an unlimited SMTP DATA body, or returning an endless multiline response.

**Proposed fix:** use bounded line readers and configurable protocol limits; replace unbounded queues with bounded channels; enforce maximum command length, response count, message size, recipient count and multiline duration; disconnect when a limit is exceeded; provide streaming APIs for large payloads.

**Severity:** Critical.

### 3. Server session state survives reuse across connections
`CommandResponseServer.StartAsync` does not clear `_contexts`, queued commands or error state. Protocol servers also keep connection-specific fields on the server object: `SmtpServer` retains `_isAuthenticated`, `_canRelay`, `_loginUser`, sender and recipients; `Pop3Server` retains `_user`, `_deleted` and its APOP timestamp; `NntpServer` retains the selected group/article and posting state.

**Risk:** if a server instance is reused sequentially for another stream, the new peer may inherit authentication/relay privileges or protocol state from the previous connection. For SMTP this can become an authentication bypass and unauthorized relay path. POP3 deletion marks and NNTP posting/group state can also cross session boundaries.

**Proposed fix:** make server objects explicitly single-use and reject a second `StartAsync`, or move all connection state into a fresh per-session object created for every connection. Clear all contexts, queues, authentication state and protocol state on start and shutdown. Add tests that reuse the same instance with two streams and verify complete isolation.

**Severity:** Critical.

## High-severity security findings

### 4. SMTP AUTH is advertised and accepted on unencrypted transports
`SmtpServer` advertises `AUTH PLAIN LOGIN` whenever an authenticator exists, but it has no knowledge of whether the underlying stream is protected by TLS. It accepts both mechanisms on cleartext streams.

**Risk:** usernames and passwords can be intercepted directly on the network.

**Proposed fix:** make transport security state explicit; do not advertise or accept cleartext authentication mechanisms unless TLS is active; require an explicit dangerous compatibility option to override this default; add first-class TLS connection APIs.

**Severity:** High.

### 5. Local-domain policy fails open
`SmtpServer` defaults `isLocalDomain` to `_ => true`. If the caller omits the predicate, every domain is treated as local and the relay-protection branch is bypassed.

**Risk:** an incomplete configuration accepts mail for arbitrary domains and may become an open relay depending on the message-store/delivery integration.

**Proposed fix:** require an explicit local-domain policy, or default to `_ => false`. Reject empty and malformed domains before policy evaluation.

**Severity:** High.

### 6. Client APIs permit CR/LF command injection
User-controlled values are interpolated directly into protocol lines, including POP3 username/password, SMTP EHLO domain, sender, recipient and several generic command arguments. `WriteLineAsync` then transmits the resulting string unchanged.

**Risk:** an argument containing `\r` or `\n` can inject additional protocol commands and alter the session state.

**Proposed fix:** reject carriage return, line feed, NUL and other prohibited control characters in every line-oriented argument; add protocol-specific validation for SMTP paths, host names, user names and identifiers; keep multiline writes behind dedicated APIs only.

**Severity:** High.

### 7. SMTP authentication has no brute-force protection
The server calls the authenticator for every attempt without delay, rate limit, temporary lockout or per-connection attempt cap. `MaxConsecutiveErrors` defaults to zero and is not an authentication-specific defense.

**Risk:** online password guessing and resource exhaustion.

**Proposed fix:** add an injectable authentication-throttling policy; enforce a safe per-connection limit by default; support external rate limiting by remote endpoint and account; use uniform failure timing where practical.

**Severity:** High.

### 8. APOP relies on MD5 but is not marked as legacy
`Pop3Client.AuthenticateApopAsync` implements the protocol-required MD5 digest. The older USER/PASS method is marked obsolete, while APOP is not, which can incorrectly imply that APOP is a modern secure alternative.

**Proposed fix:** mark APOP as obsolete/compatibility-only, document MD5 weakness, and recommend TLS-protected transport regardless of the POP3 authentication mechanism.

**Severity:** High.

### 9. NTP responses are unauthenticated and insufficiently validated
`NtpClient.GetTimeAsync` sends a 48-byte UDP request and accepts the first datagram returned. It does not connect the UDP socket to the selected server, validate the source endpoint, check the packet length before reading bytes 40-47, validate the NTP mode/version/stratum/leap state, or compare the originate timestamp with the request. It also has no timeout or cancellation token.

**Risk:** spoofed or malformed UDP responses can set an arbitrary time, trigger exceptions, or block the caller indefinitely. This is especially dangerous if the result influences certificate validation, token expiry, audit timestamps or security decisions.

**Proposed fix:** connect the UDP socket to the intended endpoint; add timeout/cancellation; require a valid 48+ byte server response; validate mode, version, stratum and leap indicator; bind the response to the request using the originate/transmit timestamps; clearly document that unauthenticated NTP is not suitable as a trusted security clock. Consider NTS or an OS time service for trusted time.

**Severity:** High.

### 10. DNS replies are not bound to the query
`DNSLookup.Request` returns the first datagram received and parses it without verifying that the sender endpoint is the requested name server, that the transaction ID matches, that the packet is a response, or that the question section matches the requested name/type/class. `ReceiveFrom` is allowed to overwrite the endpoint variable, but the resulting endpoint is never checked.

**Risk:** off-path or local-network spoofing can inject forged DNS answers. A random 16-bit transaction ID alone is not enough when the response is not checked against it.

**Proposed fix:** connect the UDP socket or compare the returned endpoint; verify ID, QR bit, opcode, question count and exact question tuple; reject malformed/reserved flags; randomize source ports through normal socket binding; expose whether data was DNSSEC-validated rather than trusting AD blindly.

**Severity:** High.

### 11. Protocol handler exceptions can terminate the server processing loop
`CommandResponseServer.ProcessQueueAsync` only catches `OperationCanceledException`. Exceptions from command handlers, authenticators, message/article stores, formatters or response enumeration escape the loop and permanently stop processing that connection.

**Risk:** malformed input that triggers a downstream parser/store exception, or a deliberately failing dependency, can cause a reliable denial of service. The client may remain connected while no further commands are processed.

**Proposed fix:** catch exceptions around each command dispatch; map expected protocol/input errors to safe responses; log sanitized diagnostics; close the connection on unexpected faults; ensure cleanup and completion state are deterministic.

**Severity:** High.

### 12. NNTP posting is unauthenticated and unbounded
`NntpServer` permits `POST` after merely selecting a group. There is no authentication or authorization hook, and the full article is buffered in `_postLines` until a terminating dot arrives.

**Risk:** any connected peer can post arbitrary content and exhaust memory, subject only to what the backing store later accepts.

**Proposed fix:** add explicit posting authorization, advertise posting capability accurately, enforce article/line limits and timeouts, validate required headers, and stream or spool large articles instead of buffering them entirely.

**Severity:** High.

## Medium-severity findings

### 13. SMTP envelope addresses are not validated strictly
`SmtpServer.ExtractAddress` only extracts text between the first `<` and `>`, or returns the raw first argument. Empty values, missing domains, control characters, ignored suffixes and excessive lengths are accepted.

**Proposed fix:** implement a deliberately limited but strict SMTP path parser; reject ambiguous syntax and control characters; enforce length limits; handle the null reverse-path explicitly rather than treating arbitrary empty input as valid.

**Severity:** Medium to High.

### 14. Untrusted protocol data is logged without sanitization or truncation
Received response codes/messages and commands are written to logs without filtering control characters or limiting size.

**Risk:** log forging, terminal/control-sequence injection, excessive log volume and accidental disclosure of message content.

**Proposed fix:** sanitize control characters, truncate remote data, lower raw protocol content to Debug/Trace, and never log message bodies or authentication material.

**Severity:** Medium.

### 15. Client-side responses and POP3 payloads have no resource limits
`SendCommandAsync`, POP3 multiline readers and message retrieval collect all data before returning. Numeric response parsing can also throw on oversized values.

**Proposed fix:** enforce response line/count/byte limits, use `TryParse` with explicit range validation, and expose streaming retrieval methods.

**Severity:** Medium.

### 16. DNS truncation and modern UDP response sizes are not handled
`DNSLookup` allocates a fixed 512-byte buffer. It does not negotiate EDNS(0), inspect the TC flag and retry over TCP, or distinguish truncation from a complete response. All transport and parse exceptions are swallowed while iterating name servers.

**Risk:** valid modern DNS replies may be silently truncated or discarded, while callers receive only a generic exception with no diagnostic cause. Security-related records such as DNSSEC chains are particularly likely to exceed 512 bytes.

**Proposed fix:** support EDNS(0) with a bounded larger UDP payload; retry over TCP when TC is set; retain and expose the final meaningful exception; distinguish timeout, malformed response and server failure.

**Severity:** Medium.

### 17. Concurrent server writes are not serialized
`CommandResponseServer.SendResponseAsync` writes directly to the shared `StreamWriter`, while `ProcessQueueAsync` also writes responses. No send lock protects these paths.

**Risk:** unsolicited and command responses can interleave or corrupt the text protocol framing, potentially producing ambiguous state transitions or leaking portions of unrelated responses.

**Proposed fix:** route every outbound line through a single serialized writer/queue and apply backpressure and output limits.

**Severity:** Medium.

### 18. Cancellation tokens are not propagated into protocol dependencies
SMTP, POP3 and NNTP handlers generally call authenticator/store methods without a cancellation token. A slow or malicious backend can therefore keep a connection handler alive after client disconnect or server shutdown.

**Proposed fix:** add cancellation-aware interface overloads and propagate the per-session token through authentication, listing, retrieval, storage, deletion and posting operations.

**Severity:** Medium.

## Required regression and security tests

- Verify that passwords, AUTH payloads and continuation credentials never appear in logs.
- Reject CR/LF/NUL injection in every public line-oriented client API.
- Reject oversized command lines and terminate the connection deterministically.
- Bound queued commands and verify backpressure/disconnection behavior.
- Enforce SMTP DATA size and recipient-count limits.
- Verify that SMTP AUTH is neither advertised nor accepted without TLS by default.
- Verify that omitting the local-domain policy does not accept arbitrary remote domains.
- Add authentication throttling/attempt-limit tests.
- Add malicious multiline-response and endless-DATA/POST tests.
- Mark and test legacy APOP behavior/documentation.
- Reuse each server instance with two streams and verify that no authentication, relay, deletion, selected-group or posting state crosses sessions.
- Reject NTP responses from the wrong endpoint, undersized packets, invalid modes/strata and mismatched originate timestamps; test timeout/cancellation.
- Reject DNS responses with the wrong endpoint, ID, QR bit or question; test TC-to-TCP fallback.
- Verify that handler/backend exceptions close or recover the session without leaving a dead connection.
- Verify serialized output when unsolicited and command responses are emitted concurrently.

## Priority roadmap

| Priority | Finding |
|---|---|
| P0 | Secret-bearing commands and authentication continuations are logged |
| P0 | Unbounded lines, queues, SMTP DATA and multiline responses |
| P0 | Server authentication and protocol state can survive instance reuse |
| P1 | SMTP AUTH accepted without TLS |
| P1 | Local-domain policy fails open |
| P1 | CR/LF command injection in client APIs |
| P1 | No authentication throttling |
| P1 | APOP/MD5 not clearly deprecated |
| P1 | NTP responses are unauthenticated and not bound to the request |
| P1 | DNS responses are not validated against the query |
| P1 | Handler exceptions can terminate the processing loop |
| P1 | NNTP posting lacks authorization and limits |
| P2 | Weak SMTP envelope validation |
| P2 | Unsanitized remote data in logs |
| P2 | Unbounded client response accumulation |
| P2 | DNS truncation/TCP fallback missing |
| P2 | Concurrent server writes are not serialized |
| P2 | Cancellation is not propagated to backend operations |

## Deployment warning

Until the P0/P1 findings are fixed, do not expose `CommandResponseServer`, `SmtpServer`, `Pop3Server` or `NntpServer` directly to an untrusted network without an external TLS terminator, strict connection and payload limits, rate limiting, timeouts, per-connection server instances and log redaction. Do not use `NtpClient` as a trusted security clock, and do not rely on `DNSLookup` for security-sensitive name resolution until response validation is implemented.