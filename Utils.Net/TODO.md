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

## High-severity security findings

### 3. SMTP AUTH is advertised and accepted on unencrypted transports
`SmtpServer` advertises `AUTH PLAIN LOGIN` whenever an authenticator exists, but it has no knowledge of whether the underlying stream is protected by TLS. It accepts both mechanisms on cleartext streams.

**Risk:** usernames and passwords can be intercepted directly on the network.

**Proposed fix:** make transport security state explicit; do not advertise or accept cleartext authentication mechanisms unless TLS is active; require an explicit dangerous compatibility option to override this default; add first-class TLS connection APIs.

**Severity:** High.

### 4. Local-domain policy fails open
`SmtpServer` defaults `isLocalDomain` to `_ => true`. If the caller omits the predicate, every domain is treated as local and the relay-protection branch is bypassed.

**Risk:** an incomplete configuration accepts mail for arbitrary domains and may become an open relay depending on the message-store/delivery integration.

**Proposed fix:** require an explicit local-domain policy, or default to `_ => false`. Reject empty and malformed domains before policy evaluation.

**Severity:** High.

### 5. Client APIs permit CR/LF command injection
User-controlled values are interpolated directly into protocol lines, including POP3 username/password, SMTP EHLO domain, sender, recipient and several generic command arguments. `WriteLineAsync` then transmits the resulting string unchanged.

**Risk:** an argument containing `\r` or `\n` can inject additional protocol commands and alter the session state.

**Proposed fix:** reject carriage return, line feed, NUL and other prohibited control characters in every line-oriented argument; add protocol-specific validation for SMTP paths, host names, user names and identifiers; keep multiline writes behind dedicated APIs only.

**Severity:** High.

### 6. SMTP authentication has no brute-force protection
The server calls the authenticator for every attempt without delay, rate limit, temporary lockout or per-connection attempt cap. `MaxConsecutiveErrors` defaults to zero and is not an authentication-specific defense.

**Risk:** online password guessing and resource exhaustion.

**Proposed fix:** add an injectable authentication-throttling policy; enforce a safe per-connection limit by default; support external rate limiting by remote endpoint and account; use uniform failure timing where practical.

**Severity:** High.

### 7. APOP relies on MD5 but is not marked as legacy
`Pop3Client.AuthenticateApopAsync` implements the protocol-required MD5 digest. The older USER/PASS method is marked obsolete, while APOP is not, which can incorrectly imply that APOP is a modern secure alternative.

**Proposed fix:** mark APOP as obsolete/compatibility-only, document MD5 weakness, and recommend TLS-protected transport regardless of the POP3 authentication mechanism.

**Severity:** High.

## Medium-severity findings

### 8. SMTP envelope addresses are not validated strictly
`SmtpServer.ExtractAddress` only extracts text between the first `<` and `>`, or returns the raw first argument. Empty values, missing domains, control characters, ignored suffixes and excessive lengths are accepted.

**Proposed fix:** implement a deliberately limited but strict SMTP path parser; reject ambiguous syntax and control characters; enforce length limits; handle the null reverse-path explicitly rather than treating arbitrary empty input as valid.

**Severity:** Medium to High.

### 9. Untrusted protocol data is logged without sanitization or truncation
Received response codes/messages and commands are written to logs without filtering control characters or limiting size.

**Risk:** log forging, terminal/control-sequence injection, excessive log volume and accidental disclosure of message content.

**Proposed fix:** sanitize control characters, truncate remote data, lower raw protocol content to Debug/Trace, and never log message bodies or authentication material.

**Severity:** Medium.

### 10. Client-side responses and POP3 payloads have no resource limits
`SendCommandAsync`, POP3 multiline readers and message retrieval collect all data before returning. Numeric response parsing can also throw on oversized values.

**Proposed fix:** enforce response line/count/byte limits, use `TryParse` with explicit range validation, and expose streaming retrieval methods.

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
- Add malicious multiline-response and endless-DATA tests.
- Mark and test legacy APOP behavior/documentation.

## Priority roadmap

| Priority | Finding |
|---|---|
| P0 | Secret-bearing commands and authentication continuations are logged |
| P0 | Unbounded lines, queues, SMTP DATA and multiline responses |
| P1 | SMTP AUTH accepted without TLS |
| P1 | Local-domain policy fails open |
| P1 | CR/LF command injection in client APIs |
| P1 | No authentication throttling |
| P1 | APOP/MD5 not clearly deprecated |
| P2 | Weak SMTP envelope validation |
| P2 | Unsanitized remote data in logs |
| P2 | Unbounded client response accumulation |

## Deployment warning

Until the P0/P1 findings are fixed, do not expose `CommandResponseServer`, `SmtpServer`, `Pop3Server` or `NntpServer` directly to an untrusted network without an external TLS terminator, strict connection and payload limits, rate limiting, timeouts and log redaction.