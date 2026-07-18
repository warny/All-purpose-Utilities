# Utils.Reflection — Fresh audit (2026-07-19)

Fresh review of the current `Utils.Reflection` code after the previous audit items were moved to `DONE-*` files. This document intentionally focuses on defects and contract gaps still visible in the current implementation. No production code is changed by this commit.

## Critical findings

### 1. Method identity is not preserved across interface modules

Worker calls identify a method only by `MethodInfo.MetadataToken`. At load time, tokens from `interfaceType.GetMethods()` are placed in one numeric allowlist. At call time, the token is resolved through `interfaceType.Module.ResolveMethod(token)`.

Metadata tokens are unique only inside one module. An inherited interface method declared in another assembly/module can therefore have the same numeric token as an unrelated method in the main interface module. The numeric allowlist can accept the token, after which `ResolveMethod` resolves a different method.

**Fix:** transmit and validate a stable method identity that includes the declaring assembly/module plus the method token, or build a load-time command table assigning private protocol method IDs directly to validated `MethodInfo` instances. Do not resolve caller-supplied tokens against a different module.

**Priority: P0 — remote-dispatch integrity.**

### 2. `Unload` can dispose a native mapping while a call is executing

`HandleCall` retrieves a `LoadedInterface` value from the concurrent dictionary and then invokes it. A concurrent `Unload` can remove that dictionary entry and immediately call `Dispose` on the same instance after `HandleCall` has already retrieved it.

The current documentation says the losing call should fail with an unknown-handle error, but that is only true when removal wins before lookup. When lookup wins first, disposal can occur during native execution, creating a use-after-free race for the native library handle and emitted delegates.

**Fix:** introduce per-handle lifetime coordination. Calls must acquire a lease/read lock or increment an active-call count before accessing the instance. Unload must first mark the handle as closing, reject new calls, wait for active calls to finish, and only then dispose it.

**Priority: P0 — native-resource safety.**

## High-priority findings

### 3. Shutdown acknowledges success without guaranteeing that active calls stopped

On `Shutdown`, the worker waits at most five seconds for dispatched tasks, ignores timeout/fault information, writes a successful shutdown response, and returns. Calls still running after the drain window can be terminated when the worker process exits even though the host received a successful shutdown acknowledgement.

**Fix:** define explicit graceful and forced shutdown semantics. A graceful response must only be sent after all accepted calls have completed and all mappings have been disposed. If the deadline expires, return a distinct forced/partial-shutdown status or let the host kill the process without claiming graceful success.

**Priority: P1 — lifecycle correctness.**

### 4. Loaded mappings are not explicitly disposed when the worker loop ends

The worker-local `loaded` dictionary owns native mapping instances. `Run` returns on shutdown or end-of-stream without a `finally` block that removes and disposes every remaining instance.

Process termination eventually releases OS resources, but managed/native cleanup code, library-specific shutdown, buffers, and diagnostics are skipped. This also makes in-process tests of `EmitWorkerHost.Run` observe different ownership semantics from the real worker process.

**Fix:** wrap the complete loop in `try/finally`; stop accepting new calls, drain or cancel active calls according to the chosen policy, then dispose every remaining mapping exactly once.

**Priority: P1 — deterministic cleanup.**

### 5. A host-side write failure leaves a request pending until its timeout

`SendAndReceive` adds the request's completion source to `pending` before serializing/writing the request. If serialization or `writer.WriteLine` throws, the method exits without removing that pending entry. The timeout callback later classifies the request as an abandoned worker call and may contribute to worker retirement, even though the request was never sent.

**Fix:** wrap serialization/write in `try/catch`; remove and fault the exact pending entry immediately on failure. Count a call as abandoned only after a complete frame has been successfully written.

**Priority: P1 — protocol state integrity.**

### 6. Pool workers are never replaced after a connection fault or retirement

`EmitWorkerPool` caches one worker for its complete lifetime. Once that worker closes, faults, or retires after abandoned calls, later `Emit` operations keep using the same poisoned object and fail permanently.

**Fix:** expose a reliable worker health state. Under the pool lock, replace a faulted worker before loading a new interface. Clearly define what happens to existing proxies and avoid automatic retry of calls whose side effects may be indeterminate.

**Priority: P1 — availability and lifecycle contract.**

### 7. Timeout values are stored without an explicit public validation contract

`EmitWorkerPool` accepts arbitrary nullable `TimeSpan` values and forwards them later to `CancellationTokenSource`. Zero, negative, excessively large, or infinite values therefore fail late and inconsistently, potentially after spawning a process or loading an interface.

**Fix:** validate load/call timeouts in constructors and entry points before allocating resources. Explicitly decide whether `Timeout.InfiniteTimeSpan` is supported; otherwise require a positive finite duration within the runtime-supported range.

**Priority: P1 — argument and resource safety.**

### 8. Cross-process type validation does not prove JSON round-tripability

`IsSupportedType` accepts a value type when its public fields and readable public properties recursively use supported types. This does not prove that `System.Text.Json` can reconstruct the type. Read-only/computed properties, constructor-only invariants, custom converters, ignored members, duplicate field/property names, or throwing getters can still make serialization lossy or fail at runtime.

**Fix:** define the supported wire-shape contract independently from CLR reflection shape. Prefer DTO-like structs with public settable fields/properties and validated constructors, or generate/test a serializer contract for every method type at load time. Reject computed/indexed/read-only members unless explicitly supported.

**Priority: P1 — marshaling correctness.**

### 9. Remote exception details expose worker internals by default

The worker returns the concrete exception type name and full remote stack trace for every request failure. These details can contain local filesystem paths, generated source/type names, native library information, and implementation details from code executed inside the sandbox.

**Fix:** separate a safe public error payload from opt-in diagnostic details. Return a stable error code/category and sanitized message by default; expose stack traces only through an explicit trusted-debug option.

**Priority: P1 — information disclosure.**

## Medium-priority findings

### 10. The protocol's 64 MiB line limit still permits expensive single-frame allocation and parsing

Framing is bounded, but it reads character by character into a growing `StringBuilder`, then materializes another JSON object/string graph. One request can therefore consume substantially more than 64 MiB and hold a worker or host thread for a long period.

**Fix:** use length-prefixed binary framing with a much smaller configurable default, validate length before allocation, and stream or chunk large buffers explicitly. Keep per-request and aggregate in-flight byte budgets.

**Priority: P2 — denial-of-service resistance.**

### 11. Protocol DTOs do not carry a protocol version or negotiated capabilities

Host and worker are assumed to run exactly matching message definitions. A stale executable, deployment mismatch, or future rolling upgrade can deserialize with defaults and fail later in misleading ways.

**Fix:** perform an initial handshake containing protocol version, package/assembly version, supported request kinds, serialization options, and maximum frame size. Reject incompatible peers before accepting `Load`.

**Priority: P2 — compatibility and diagnostics.**

### 12. Missing argument entries are silently converted to `null`

The worker accepts an `ArgumentsJson` array shorter than the method parameter list and supplies `null` for missing entries. This shifts a malformed-protocol error into arbitrary deserialization, reflection, or native-call behavior.

**Fix:** require an exact argument-slot count for every call, including explicit slots for `out` parameters. Validate the response's by-ref array length in the host as well.

**Priority: P2 — protocol validation.**

### 13. Duplicate or unsolicited response IDs are silently ignored

The reader loop drops every response whose ID is not currently in `pending`, including duplicates, responses for never-sent IDs, and late responses. Late responses after a documented timeout may be expected, but all other cases indicate protocol corruption or worker bugs.

**Fix:** retain a bounded set/range of timed-out IDs so expected late responses can be distinguished from impossible IDs. Treat duplicate or unsolicited live-protocol responses as a connection fault and include the offending ID in diagnostics.

**Priority: P2 — protocol observability.**

### 14. The proxy's synchronization primitive is never disposed

Every `EmitWorkerProxy` allocates a `ReaderWriterLockSlim`. Calling the proxied `Dispose` releases the worker/handle but does not dispose the lock. The lock can own wait handles after contention.

**Fix:** after acquiring the write lock, mark the proxy disposed and release resources; dispose the lock only when no future method can enter it, or replace it with a lighter lifecycle gate that does not require disposal.

**Priority: P2 — managed-resource lifecycle.**

### 15. Worker creation and calls are synchronous-only despite process and IPC waits

Public mapping and invocation paths block threads during process startup, pipe connection, load, calls, shutdown, and pool disposal. The implementation internally uses asynchronous pipe operations but immediately blocks with `GetAwaiter().GetResult()` or task waits.

**Fix:** provide cancellation-aware asynchronous creation, load, invocation, unload, and disposal APIs. Keep synchronous wrappers only where required and document their blocking/deadlock characteristics.

**Priority: P2 — API scalability.**

## Duplicated intent to consolidate

- Replace raw metadata-token transport with one load-time method-command table.
- Centralize per-handle state, active-call leasing, closing, and disposal.
- Centralize request-frame writing so pending registration and write rollback are atomic from the protocol's perspective.
- Define one worker state machine shared by standalone proxies and pools (`Starting`, `Healthy`, `Retiring`, `Faulted`, `Disposed`).
- Generate serializer contracts once per loaded interface and reuse them for validation and invocation.
- Define one shutdown implementation responsible for request admission, draining, mapping disposal, response status, and forced termination.

## Required tests

- Load an interface inheriting methods from another assembly whose metadata tokens collide with methods in the main module; verify exact method identity.
- Block a native call, concurrently dispose/unload its proxy, and verify disposal waits without invoking freed delegates.
- Keep a call running beyond the shutdown drain deadline and verify the result matches the documented graceful/forced status.
- End the input stream and send shutdown with multiple still-loaded disposable mappings; assert each is disposed exactly once.
- Force request serialization and pipe-write failures; assert `pending` is cleaned immediately and abandoned-call counters do not change.
- Kill or retire a pooled worker, then load a new interface and verify safe replacement behavior.
- Test zero, negative, infinite, maximum-supported, and ordinary timeout values before any worker is spawned.
- Test computed, throwing, read-only, constructor-bound, ignored, renamed, and custom-converter struct members through a complete JSON round trip.
- Verify safe errors omit local paths and stack traces unless trusted diagnostics are enabled.
- Fuzz frame lengths, malformed JSON, missing/extra argument slots, duplicate IDs, unsolicited IDs, and oversized concurrent requests.
- Exercise heavy proxy contention followed by disposal and confirm all synchronization resources are released.

## Recommended order

| Priority | Action |
|---|---|
| P0 | Replace token-only method identity with validated per-method protocol IDs |
| P0 | Add per-handle call leases so unload cannot dispose active native calls |
| P1 | Make shutdown truthful and dispose all loaded mappings deterministically |
| P1 | Roll back pending requests immediately when frame writing fails |
| P1 | Add pool worker replacement and validate timeouts before allocation |
| P1 | Enforce a real JSON round-trip contract and sanitize remote errors |
| P2 | Version the protocol, validate exact message shapes, and tighten frame budgets |
| P2 | Add asynchronous/cancellation-aware lifecycle APIs |

## Deployment warning

Until findings 1 and 2 are fixed, do not treat the worker protocol as a complete native-call safety boundary for inherited interfaces or concurrent unload/call scenarios. Until shutdown and pending-write handling are corrected, timeout and disposal outcomes can misrepresent what actually executed inside the worker. Keep each pool limited to one trust boundary and recycle faulted workers explicitly.