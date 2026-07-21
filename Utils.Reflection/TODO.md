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

## Implementation guides for the complex changes

The following guides are intentionally more prescriptive than the findings above. They describe one coherent implementation path, the main files to change, the invariants to preserve, and the tests that should be written before refactoring production code.

### Guide A — Replace metadata tokens with worker-private method IDs (item 1)

**Recommended design:** create the method-command table during `Load` and never resolve a caller-provided metadata token during `Call`.

**Files likely involved:**

- `Reflection/Emit/EmitWorkerHost.cs`
- `Reflection/Emit/EmitWorkerMessages.cs`
- `Reflection/Emit/EmitWorkerProcess.cs`
- `Reflection/Emit/EmitWorkerProxy.cs`
- protocol and functional tests under `UtilsTest` and `UtilsTest.Functional`

**Steps:**

1. Replace `LoadedInterface.AllowedMethodTokens` with an immutable table such as `FrozenDictionary<int, MethodInfo> MethodsById`.
2. At load time, enumerate `interfaceType.GetMethods()`, validate every method once, and assign contiguous worker-private IDs. Do not derive these IDs from metadata tokens.
3. Return a method descriptor table in the successful `Load` response. Each descriptor should contain the private ID and a host-verifiable signature key, for example declaring type identity, method name, generic arity, parameter type identities and return type identity.
4. On the host, build the inverse mapping from local `MethodInfo` to remote method ID when the proxy is attached. The proxy should send only the private ID on each call.
5. In `HandleCall`, perform a direct lookup in `MethodsById`; reject unknown IDs before deserializing arguments.
6. Keep metadata tokens only as an internal optimization or diagnostic field. Never use them as a cross-module identity.

**Important invariants:**

- The host cannot select a method that was not included in the validated load-time table.
- Two inherited methods with equal metadata-token values remain distinct.
- Overloads and methods with equivalent names in different declaring interfaces remain distinct.
- The mapping is immutable for the lifetime of one loaded handle.

**Migration note:** adding the descriptor table changes the wire protocol. Implement it together with the version handshake from Guide F rather than adding another unversioned DTO shape.

**Tests:** create two interface assemblies with colliding numeric tokens, inherited methods and overloads. Assert that each proxy method reaches exactly the expected `MethodInfo`, and that an unknown private ID fails before invocation.

### Guide B — Introduce a per-handle lease/state object (items 2 and 4)

**Recommended design:** replace the value-type `LoadedInterface` record with one reference-type owner that controls admission, active-call counting, closing and one-time disposal.

A possible state model is:

```text
Open -> Closing -> Disposed
```

with an integer active-call count. A call lease may be represented by a small `IDisposable` token returned by `TryAcquireCall`.

**Steps:**

1. Create a `LoadedInterfaceState` class containing the mapping instance, method table, a private lock, the lifecycle state and the active-call count.
2. `TryAcquireCall` must atomically reject `Closing`/`Disposed`, increment the count while still under the lock, and return a lease.
3. Lease disposal decrements the active count and signals a waiter when the count reaches zero.
4. `Unload` first removes or marks the handle as closing so no new call can acquire a lease. It then waits for the active count to reach zero and disposes the mapping exactly once.
5. `HandleCall` must hold the lease across method lookup, argument deserialization, native invocation and response-value extraction. Releasing it before the native call returns would reintroduce the race.
6. Worker shutdown must reuse the same `CloseAndDispose` operation for every remaining handle; do not maintain a separate disposal path.

**Avoid:** holding a global dictionary lock during native calls. That would serialize unrelated handles and make one blocked DLL call stop all unloads and calls.

**Cancellation/deadline policy:** decide whether unload waits indefinitely or accepts a deadline. If a deadline is supported, a timed-out unload must not dispose the mapping while leases remain; it should return a non-success status and leave final disposal to worker shutdown.

**Tests:** block one native call with synchronization primitives, issue unload concurrently, verify new calls are rejected, verify disposal has not started while the first call is active, then release the call and assert one-time disposal.

### Guide C — Build one truthful shutdown and cleanup pipeline (items 3 and 4)

**Recommended design:** make `Run` own a worker-lifecycle controller and perform cleanup from one `finally` block for both explicit shutdown and end-of-stream.

**Steps:**

1. Introduce an admission state (`Accepting`, `Draining`, `Stopped`). Check it before dispatching each non-shutdown request.
2. On a shutdown request, atomically switch to `Draining`; no request read or dispatched after that transition may be accepted.
3. Snapshot and await all already accepted request tasks. Do not silently ignore task faults; collect them for diagnostics.
4. Close every loaded handle through the lease-aware operation from Guide B.
5. Send `Shutdown` success only after task drain and mapping disposal both completed.
6. If the configured graceful deadline expires, return an explicit status such as `GracefulShutdownTimedOut` if the connection is still writable. The host may then terminate the worker process. Do not send the ordinary success response.
7. In a `finally` block, perform best-effort cleanup for malformed input, pipe closure and unexpected exceptions. This path should be idempotent and safe after partial graceful cleanup.

**Suggested response data:** add a stable error/status code rather than encoding forced shutdown only in a message string.

**Tests:** cover normal shutdown, end-of-stream, a task fault, a call exceeding the drain deadline, multiple loaded handles, duplicate cleanup attempts, and a response writer that fails during shutdown acknowledgement.

### Guide D — Make request registration and frame writing transactional (item 5)

**Recommended design:** centralize all outgoing requests in one method that distinguishes `Registered`, `FrameWritten`, `TimedOut` and `Completed` states.

**Steps:**

1. Allocate the request ID and completion source.
2. Register the exact pending entry with `TryAdd`.
3. Serialize before taking the writer lock where possible, so serialization failure cannot leave a partially written frame.
4. Under the writer lock, write and flush one complete frame. Set a `frameWritten` flag only after the operation succeeds.
5. On serialization/write/flush failure, remove the same `(id, completion source)` entry with a key/value-aware removal. Fault that completion source immediately and transition the worker to `Faulted` if the stream integrity is uncertain.
6. Start abandonment accounting only after `frameWritten` is true. A local serialization failure is not an abandoned remote call.
7. Keep late-response tracking separate from the live `pending` dictionary.

**Concurrency warning:** removal by ID alone can accidentally remove a newer entry if IDs are ever reused. Either never reuse IDs during a process lifetime or remove using both ID and expected completion-source identity.

**Tests:** inject failures from serialization, `Write`, `Flush` and connection closure. Assert immediate pending cleanup, no abandoned-call increment before a successful write, and worker faulting after a potentially partial frame.

### Guide E — Replace unhealthy pooled workers without retrying calls (items 6 and 7)

**Recommended design:** expose an immutable or atomically readable health state from `EmitWorkerProcess`, and let the pool replace the worker only before a new `Load` operation.

**Steps:**

1. Define worker states such as `Starting`, `Healthy`, `Retiring`, `Faulted` and `Disposed`, plus an optional terminal exception.
2. Transition to `Faulted` on reader-loop termination, malformed/unsolicited protocol responses, pipe write failure or unexpected process exit.
3. Transition to `Retiring` when the abandoned-call threshold is reached; reject new loads but allow deterministic cleanup of existing proxies where possible.
4. In `EmitWorkerPool.GetOrStartWorker`, while holding the pool gate, reuse only a `Healthy` worker. Detach and dispose any terminal worker, then start and publish a replacement.
5. Do not migrate existing handles to the replacement. Proxies bound to the old worker must fail deterministically with a worker-unavailable exception.
6. Never automatically retry a native `Call`: after timeout or connection loss, its side effects are indeterminate. Automatic retry is safe only for a `Load` request known not to have reached the worker, using the transactional write state from Guide D.
7. Validate timeout values in the pool constructor before any worker is created. Prefer one helper shared by pool and standalone entry points.

**Suggested timeout contract:** either support `Timeout.InfiniteTimeSpan` explicitly or require `TimeSpan.Zero < timeout <= TimeSpan.FromMilliseconds(int.MaxValue)`. Document the selected rule and apply it consistently.

**Tests:** kill the worker and then load a new interface through the same pool; retire it through abandoned calls; verify old proxies remain invalid; verify zero/negative/too-large values fail before process creation.

### Guide F — Version and bound the protocol before changing framing (items 10, 11, 12 and 13)

**Recommended design:** introduce a small fixed handshake first, then replace JSON lines with a length-prefixed envelope. Treat this as one protocol revision rather than several independent DTO edits.

**Handshake fields should include:**

- protocol major/minor version;
- package and worker assembly versions;
- maximum accepted frame size;
- serializer options/profile identifier;
- supported request kinds and optional capabilities;
- diagnostic-detail policy.

**Length-prefixed framing:**

1. Use a fixed-width unsigned length in a documented byte order.
2. Reject zero/oversized lengths before allocating the payload buffer.
3. Read exactly the announced number of bytes; premature EOF is a connection fault.
4. Use UTF-8 JSON initially if binary DTO migration is not desired. The main gain comes from validating the byte length before materializing text.
5. Set a smaller default frame limit and provide explicit chunked/blob messages for genuinely large buffers.
6. Track aggregate in-flight request bytes in addition to the per-frame limit.

**Message validation:** require exact argument-slot counts, validate by-ref response counts, reject unknown enum values, and distinguish expected late response IDs from duplicates or impossible IDs.

**Compatibility rule:** major-version mismatch must fail during handshake. Minor versions may interoperate only when both sides advertise the required capabilities.

**Tests:** fuzz truncated prefixes, oversized lengths, malformed UTF-8/JSON, extra and missing arguments, duplicate IDs, late timed-out IDs, impossible IDs and mismatched handshake versions.

### Guide G — Define and validate a serializer contract per interface (item 8)

**Recommended design:** build a wire contract for every parameter, return value and by-ref slot during `Load`, and reject the interface before emitting or loading the native library.

**Steps:**

1. Centralize one `JsonSerializerOptions` instance/profile used identically by host and worker.
2. For each participating type, inspect `JsonTypeInfo` from the configured resolver rather than inferring serializability only from fields and properties.
3. Restrict the default contract to explicit DTO shapes: supported primitives/enums/arrays plus classes or structs whose serialized members are readable and deserializable under the selected constructor policy.
4. Reject indexers, members with incompatible duplicate JSON names, unsupported polymorphism, open generic types, pointer/byref-like types and members requiring an unregistered converter.
5. Perform a contract-level validation without invoking arbitrary property getters. Do not test serializability by serializing an untrusted instance at load time.
6. Record the validated `JsonTypeInfo`/contract in the method-command table and reuse it during every call instead of repeating reflection.
7. If custom converters are allowed, require explicit registration on both sides and include a serializer-profile identifier in the handshake.

**Tests:** cover immutable constructor-bound DTOs according to the chosen policy, read-only/computed properties, throwing getters, ignored/renamed members, duplicate JSON names, custom converters, nested collections and by-ref values.

### Guide H — Add asynchronous lifecycle APIs without duplicating the protocol core (item 15)

**Recommended design:** make the worker/process implementation asynchronous internally, then implement synchronous wrappers at the public edge.

**Steps:**

1. Add `StartAsync`, `LoadInterfaceAsync`, `CallAsync`, `UnloadInterfaceAsync` and `DisposeAsync`, all accepting `CancellationToken` where cancellation has clear semantics.
2. Use `SemaphoreSlim` or an async-compatible writer gate for frame writes; do not hold a monitor across `await`.
3. Keep one pending-response mechanism based on `TaskCompletionSource` for both sync and async callers.
4. The synchronous methods should call the asynchronous core only if blocking is an accepted API contract. Use `ConfigureAwait(false)` throughout the library implementation and document that cancellation of a waiting caller does not necessarily cancel a native operation already executing remotely.
5. Distinguish caller cancellation, configured timeout, worker fault and remote failure with separate exception/status categories.
6. Implement `IAsyncDisposable` on pool/process owners. Do not make proxy finalization depend on asynchronous cleanup; finalization remains best-effort process/resource containment only.

**Tests:** cancellation before write, cancellation after write with a late response, concurrent async calls, async disposal during active calls, and synchronous wrappers invoked under a custom synchronization context.

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
