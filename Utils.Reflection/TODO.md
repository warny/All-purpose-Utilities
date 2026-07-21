# Utils.Reflection — Fresh audit (2026-07-19)

Fresh review of the current `Utils.Reflection` code after the previous audit items were moved to `DONE-*` files. This document intentionally focuses on defects and contract gaps still visible in the current implementation. No production code is changed by this commit.

## Critical findings

### ~~1. Method identity is not preserved across interface modules~~ ✅ DONE

~~Worker calls identify a method only by `MethodInfo.MetadataToken`. At load time, tokens from `interfaceType.GetMethods()` are placed in one numeric allowlist. At call time, the token is resolved through `interfaceType.Module.ResolveMethod(token)`.~~

~~Metadata tokens are unique only inside one module. An inherited interface method declared in another assembly/module can therefore have the same numeric token as an unrelated method in the main interface module. The numeric allowlist can accept the token, after which `ResolveMethod` resolves a different method.~~

**Fix applied:** `CrossProcessMarshaling.BuildCommandTable(interfaceType)` builds a deterministic `MethodInfo[]` sorted by `(DeclaringType.FullName, MethodName, ParameterTypes)` — stable across all runtimes, independent of metadata token values. Both host (`EmitWorkerProcess.LoadInterface`) and worker (`EmitWorkerHost.HandleLoad`) independently build the same table at load time and store a per-handle copy. `WorkerRequest.MethodCommandId` now carries the zero-based index into this table instead of a raw metadata token. `HandleCall` validates the index with a bounds check and looks up the `MethodInfo` directly — no `Module.ResolveMethod` call at all. `EmitWorkerProcess` stores a `FrozenDictionary<MethodInfo, int>` reverse table per handle so `InvokeMethod` can convert a `MethodInfo` to its command ID in O(1). Unit tests verify the ordering contract (`BuildCommandTable_MultipleMethodsSameDeclaringType_SortsByName`, `BuildCommandTable_IsDeterministic_SameOutputOnRepeatedCalls`); functional test `Run_CallWithOutOfRangeCommandId_ReturnsFailureResponse` verifies that an out-of-range command ID is rejected with a descriptive error.

~~**Priority: P0 — remote-dispatch integrity.**~~

### ~~2. `Unload` can dispose a native mapping while a call is executing~~ ✅ DONE

~~`HandleCall` retrieves a `LoadedInterface` value from the concurrent dictionary and then invokes it. A concurrent `Unload` can remove that dictionary entry and immediately call `Dispose` on the same instance after `HandleCall` has already retrieved it.~~

**Fix applied:** `LoadedInterface` (a struct) replaced by `LoadedInterfaceSlot` (a class with ref-counting). `TryBeginCall()` atomically checks `_closing` and increments the active-call count; `EndCall()` decrements and signals; `Dispose()` marks `_closing = true` then `Monitor.Wait`s for count to reach 0 before disposing. `HandleCall` calls `TryBeginCall` in a try/finally with `EndCall`; `HandleUnload` calls `slot.Dispose()`. Functional test `Run_UnloadWhileCallInProgress_WaitsForCallBeforeDisposing` verifies the race is resolved.

~~**Priority: P0 — native-resource safety.**~~

## High-priority findings

### ~~3. Shutdown acknowledges success without guaranteeing that active calls stopped~~ ✅ DONE

~~On `Shutdown`, the worker waits at most five seconds for dispatched tasks, ignores timeout/fault information, writes a successful shutdown response, and returns.~~

**Fix applied:** `DrainDispatched` now returns `bool` (whether all tasks completed before the 5-second deadline). Shutdown writes `Success = true` only if all tasks drained; otherwise `Success = false` with an explanatory `ErrorMessage`. Tests added in `EmitWorkerHostLoopTests`.

~~**Priority: P1 — lifecycle correctness.**~~

### ~~4. Loaded mappings are not explicitly disposed when the worker loop ends~~ ✅ DONE

~~The worker-local `loaded` dictionary owns native mapping instances. `Run` returns on shutdown or end-of-stream without a `finally` block that removes and disposes every remaining instance.~~

**Fix applied:** `EmitWorkerHost.Run` now wraps the full request loop in `try/finally`; on exit (whether graceful Shutdown, end-of-stream, or exception), every remaining `IDisposable` mapping is disposed exactly once (errors are swallowed per-entry to ensure all entries are attempted). Functional test `Run_LoadedInterfaceNotUnloaded_IsDisposedOnShutdown` verifies the behaviour.

~~**Priority: P1 — deterministic cleanup.**~~

### ~~5. A host-side write failure leaves a request pending until its timeout~~ ✅ DONE

~~`SendAndReceive` adds the request's completion source to `pending` before serializing/writing the request. If serialization or `writer.WriteLine` throws, the method exits without removing that pending entry.~~

**Fix applied:** `SendAndReceive` now wraps the lock/write block in try/catch; removes and the pending entry immediately on failure, so an unsent request can never be misclassified as an abandoned call. `PendingCount` and `AbandonedCallCount` are now `internal` for unit testing. A `CreateForTesting` factory enables testing with injected streams without spawning a real worker process.

~~**Priority: P1 — protocol state integrity.**~~

### ~~6. Pool workers are never replaced after a connection fault or retirement~~ ✅ DONE

~~`EmitWorkerPool` caches one worker for its complete lifetime. Once that worker closes, faults, or retires after abandoned calls, later `Emit` operations keep using the same poisoned object and fail permanently.~~

**Fix applied:** Added `EmitWorkerProcess.IsHealthy` (`!disposed && connectionFault is null`). `EmitWorkerPool.GetOrStartWorker` now checks `IsHealthy` before returning the cached worker: if unhealthy, the old worker is disposed, the field is cleared, and a fresh worker is started for the next call. Existing proxies backed by the old worker continue to fail — we deliberately do not retry indeterminate calls. `WorkerFactory` (internal) and `GetCurrentWorker()` (internal) enable unit testing without real process spawning. Tests: `IsHealthy_NewWorker_IsTrue`, `IsHealthy_AfterConnectionFault_IsFalse`, `GetOrStartWorker_FaultedWorker_IsDisposedAndReplacedOnNextCall`.

~~**Priority: P1 — availability and lifecycle contract.**~~

### ~~7. Timeout values are stored without an explicit public validation contract~~ ✅ DONE

~~`EmitWorkerPool` accepts arbitrary nullable `TimeSpan` values and forwards them later to `CancellationTokenSource`. Zero, negative, excessively large, or infinite values therefore fail late and inconsistently, potentially after spawning a process or loading an interface.~~

**Fix applied:** `EmitWorkerProcess.ValidateTimeout` rejects zero, negative, and >int.MaxValue ms durations. Called from `EmitWorkerPool` constructor, `EmitWorkerProcess.Start(TimeSpan?)`, and `EmitWorkerProcess.LoadInterface`. Tests added in `EmitWorkerProcessTests` and `EmitWorkerPoolTests`.

~~**Priority: P1 — argument and resource safety.**~~

### ~~8. Cross-process type validation does not prove JSON round-tripability~~ ✅ DONE

~~`IsSupportedType` accepts a value type when its public fields and readable public properties recursively use supported types. This does not prove that `System.Text.Json` can reconstruct the type. Read-only/computed properties, constructor-only invariants, custom converters, ignored members, duplicate field/property names, or throwing getters can still make serialization lossy or fail at runtime.~~

**Fix applied:** `IsSupportedType` now enforces the JSON wire contract for value-type properties: (1) indexers (parameterized properties) are skipped — not serialized by `System.Text.Json`; (2) properties marked `[JsonIgnore(Condition = Always)]` are skipped — explicitly excluded from the wire; (3) properties with a public getter but NO public setter are now REJECTED because `System.Text.Json` can serialize them but not deserialize them back, causing silent data loss on the round-trip. `init` setters and `get+set` setters are accepted. Users must add `[JsonIgnore]` to explicitly exclude computed/read-only properties. Tests cover all four cases: `IsSupportedType_ReturnsFalse_ForStructWithReadOnlyPropertyAndNoSetter`, `..._ReturnsTrue_ForStructWithJsonIgnoredReadOnlyProperty`, `..._ReturnsTrue_ForStructWithInitProperty`, `..._ReturnsTrue_ForStructWithIndexer`.

~~**Priority: P1 — marshaling correctness.**~~

### ~~9. Remote exception details expose worker internals by default~~ ✅ DONE

~~The worker returns the concrete exception type name and full remote stack trace for every request failure. These details can contain local filesystem paths, generated source/type names, native library information, and implementation details from code executed inside the sandbox.~~

**Fix applied:** `EmitWorkerProcess`, `EmitWorkerProcess.CreateForTesting`, and `EmitWorkerPool` now accept an `includeDiagnostics` flag (default `false`). `ThrowIfFailed` (now an instance method) passes `ErrorTypeName` and `ErrorStackTrace` to `EmitWorkerInvocationException` only when `includeDiagnostics=true`; otherwise those fields are `null`. Unit tests `LoadInterface_ByDefault_OmitsRemoteDiagnosticsFromException` and `LoadInterface_WithDiagnosticsEnabled_IncludesRemoteDiagnostics` verify both paths via injected streams (`EnqueueableStream`).

~~**Priority: P1 — information disclosure.**~~

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

### ~~14. The proxy's synchronization primitive is never disposed~~ ✅ DONE

~~Every `EmitWorkerProxy` allocates a `ReaderWriterLockSlim`. Calling the proxied `Dispose` releases the worker/handle but does not dispose the lock. The lock can own wait handles after contention.~~

**Fix applied:** Added `disposeGuard` field (0=alive, 1=disposed) using `Interlocked.CompareExchange` to make `Dispose` idempotent. After releasing the write lock, `invocationLock.Dispose()` is called — safe because the write lock serialises against all readers so no thread can hold the lock at that point. The read path checks `disposeGuard` first (fast path) and catches `ObjectDisposedException` from `EnterReadLock` for the narrow concurrent-Dispose window, re-throwing as a proxy-scoped ODE. Unit test `Dispose_DisposesReaderWriterLockSlim_LockCannotBeAcquiredAfterwards` verifies the lock is disposed via reflection.

~~**Priority: P2 — managed-resource lifecycle.**~~

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