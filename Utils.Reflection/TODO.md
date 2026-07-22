# Utils.Reflection — Fresh audit (2026-07-19)

Fresh review of the current `Utils.Reflection` code after the previous audit items were moved to `DONE-*` files. This document intentionally focuses on defects and contract gaps still visible in the current implementation. No production code is changed by this commit.

## Critical findings

### ~~1. Method identity is not preserved across interface modules~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~Worker calls identify a method only by `MethodInfo.MetadataToken`. At load time, tokens from `interfaceType.GetMethods()` are placed in one numeric allowlist. At call time, the token is resolved through `interfaceType.Module.ResolveMethod(token)`.~~

~~Metadata tokens are unique only inside one module. An inherited interface method declared in another assembly/module can therefore have the same numeric token as an unrelated method in the main interface module. The numeric allowlist can accept the token, after which `ResolveMethod` resolves a different method.~~

**Fix applied:** `HandleLoad` now assigns contiguous worker-private method IDs and returns them in `WorkerResponse.MethodDescriptors`. `WorkerRequest.MethodId` replaces `MethodMetadataToken`. The host builds a `MethodInfo→int` table from the descriptors; the worker resolves method IDs from a `FrozenDictionary<int, MethodInfo>` in `LoadedInterfaceState`.

### ~~2. `Unload` can dispose a native mapping while a call is executing~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~`HandleCall` retrieves a `LoadedInterface` value from the concurrent dictionary and then invokes it. A concurrent `Unload` can remove that dictionary entry and immediately call `Dispose` on the same instance after `HandleCall` has already retrieved it.~~

**Fix applied:** The `LoadedInterface` record struct was replaced by `LoadedInterfaceState`, a reference-type owner with per-handle call leasing (`TryAcquireCallLease`/`CallLease`). `HandleCall` acquires a lease before any native invocation; `HandleUnload` calls `CloseAndDispose(force: false)` which waits for all leases to be released before disposing.

## High-priority findings

### ~~3. Shutdown acknowledges success without guaranteeing that active calls stopped~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~On `Shutdown`, the worker waits at most five seconds for dispatched tasks, ignores timeout/fault information, writes a successful shutdown response, and returns.~~

**Fix applied:** `Run` now delegates to `DrainAndCloseAll`, which awaits tasks up to `GracefulShutdownDrainTimeout` and returns a grace flag. The shutdown response carries `ShutdownWasGraceful` so the host can observe whether the deadline was met.

### ~~4. Loaded mappings are not explicitly disposed when the worker loop ends~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~The worker-local `loaded` dictionary owns native mapping instances. `Run` returns on shutdown or end-of-stream without a `finally` block.~~

**Fix applied:** `Run` wraps the loop in `try/finally` that force-closes all remaining handles. The `DrainAndCloseAll` path on both explicit shutdown and end-of-stream also closes every remaining handle.

### ~~5. A host-side write failure leaves a request pending until its timeout~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~`SendAndReceive` adds the request's completion source to `pending` before serializing/writing the request. If serialization or `writer.WriteLine` throws, the method exits without removing that pending entry.~~

**Fix applied:** `SendAndReceive` catches serialization/write exceptions, removes and faults the exact pending entry immediately, and only increments the abandoned-call counter when `frameWritten` is true.

### ~~6. Pool workers are never replaced after a connection fault or retirement~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~`EmitWorkerPool` caches one worker for its complete lifetime. Once that worker closes, faults, or retires, later `Emit` operations keep using the same poisoned object.~~

**Fix applied:** `EmitWorkerProcess.IsHealthy` exposes the worker's usability. `GetOrStartWorker` now detects an unhealthy worker, disposes it, and starts a fresh replacement before loading a new interface.

### ~~7. Timeout values are stored without an explicit public validation contract~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~`EmitWorkerPool` accepts arbitrary nullable `TimeSpan` values and forwards them later to `CancellationTokenSource`. Zero, negative, excessively large, or infinite values therefore fail late.~~

**Fix applied:** `EmitWorkerProcess.ValidateTimeout` (internal, testable) validates that a timeout is a positive finite duration not exceeding `int.MaxValue` milliseconds. `EmitWorkerPool`'s constructor validates provided timeouts eagerly. `Timeout.InfiniteTimeSpan` is explicitly rejected.

### 8. Cross-process type validation does not prove JSON round-tripability

`IsSupportedType` accepts a value type when its public fields and readable public properties recursively use supported types. This does not prove that `System.Text.Json` can reconstruct the type. Read-only/computed properties, constructor-only invariants, custom converters, ignored members, duplicate field/property names, or throwing getters can still make serialization lossy or fail at runtime.

**Fix:** define the supported wire-shape contract independently from CLR reflection shape. Prefer DTO-like structs with public settable fields/properties and validated constructors, or generate/test a serializer contract for every method type at load time. Reject computed/indexed/read-only members unless explicitly supported.

**Priority: P1 — marshaling correctness.**

### ~~9. Remote exception details expose worker internals by default~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~The worker returns the concrete exception type name and full remote stack trace for every request failure.~~

**Fix applied:** `ProcessRequest` now omits `ErrorStackTrace` from error responses by default. `ErrorTypeName` is short (not assembly-qualified). `ThrowIfFailed` on the host passes `remoteStackTrace: null` to `EmitWorkerInvocationException`.

## Medium-priority findings

### 10. The protocol's 64 MiB line limit still permits expensive single-frame allocation and parsing

Framing is bounded, but it reads character by character into a growing `StringBuilder`, then materializes another JSON object/string graph. One request can therefore consume substantially more than 64 MiB and hold a worker or host thread for a long period.

**Fix:** use length-prefixed binary framing with a much smaller configurable default, validate length before allocation, and stream or chunk large buffers explicitly. Keep per-request and aggregate in-flight byte budgets.

**Priority: P2 — denial-of-service resistance.**

### 11. Protocol DTOs do not carry a protocol version or negotiated capabilities

Host and worker are assumed to run exactly matching message definitions. A stale executable, deployment mismatch, or future rolling upgrade can deserialize with defaults and fail later in misleading ways.

**Fix:** perform an initial handshake containing protocol version, package/assembly version, supported request kinds, serialization options, and maximum frame size. Reject incompatible peers before accepting `Load`.

**Priority: P2 — compatibility and diagnostics.**

### ~~12. Missing argument entries are silently converted to `null`~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~The worker accepts an `ArgumentsJson` array shorter than the method parameter list and supplies `null` for missing entries.~~

**Fix applied:** `HandleCall` now validates that `argumentsJson.Length == parameters.Length` and throws `InvalidOperationException` with a descriptive message if the counts differ.

### ~~13. Duplicate or unsolicited response IDs are silently ignored~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~The reader loop drops every response whose ID is not currently in `pending`, including duplicates, responses for never-sent IDs, and late responses.~~

**Fix applied:** `EmitWorkerProcess` maintains `recentlyTimedOutIds` (bounded at 1024 entries). Timed-out request IDs are tracked so late responses can be dropped silently. Responses for IDs that are neither pending nor recently timed out are treated as protocol violations and fault the connection.

### ~~14. The proxy's synchronization primitive is never disposed~~ ✅ FIXED (PR #fix/utils-reflection-audit-2026-07-22)

~~Every `EmitWorkerProxy` allocates a `ReaderWriterLockSlim`. Calling the proxied `Dispose` releases the worker/handle but does not dispose the lock.~~

**Fix applied:** `ReaderWriterLockSlim` was replaced by a lightweight gate using `volatile int disposeState` (Interlocked) and an `int activeCallCount` (Interlocked) with a `Monitor`-based wait in the dispose path. No OS-level wait handles are allocated.

### 15. Worker creation and calls are synchronous-only despite process and IPC waits

Public mapping and invocation paths block threads during process startup, pipe connection, load, calls, shutdown, and pool disposal. The implementation internally uses asynchronous pipe operations but immediately blocks with `GetAwaiter().GetResult()` or task waits.

**Fix:** provide cancellation-aware asynchronous creation, load, invocation, unload, and disposal APIs. Keep synchronous wrappers only where required and document their blocking/deadlock characteristics.

**Priority: P2 — API scalability.**

## Implementation guides for the complex changes

The following guides are intentionally more prescriptive than the findings above. They describe one coherent implementation path, the main files to change, the invariants to preserve, and the tests that should be written before refactoring production code.

### Guide A — Replace metadata tokens with worker-private method IDs (item 1) ✅ DONE

### Guide B — Introduce a per-handle lease/state object (items 2 and 4) ✅ DONE

### Guide C — Build one truthful shutdown and cleanup pipeline (items 3 and 4) ✅ DONE

### Guide D — Make request registration and frame writing transactional (item 5) ✅ DONE

### Guide E — Replace unhealthy pooled workers without retrying calls (items 6 and 7) ✅ DONE

### Guide F — Version and bound the protocol before changing framing (items 10, 11, 12 and 13)

Items 12 and 13 are fixed. Items 10 and 11 (protocol versioning and framing) remain.

### Guide G — Define and validate a serializer contract per interface (item 8)

Still pending. See item 8 above.

### Guide H — Add asynchronous lifecycle APIs without duplicating the protocol core (item 15)

Still pending. See item 15 above.

## Remaining items to fix

| Priority | Item | Status |
|---|---|---|
| P1 | 8. JSON round-trip contract | **Pending** |
| P2 | 10. 64 MiB frame size | **Pending** |
| P2 | 11. Protocol versioning | **Pending** |
| P2 | 15. Async APIs | **Pending** |

## Deployment warning

~~Until findings 1 and 2 are fixed~~ (now fixed), do not treat the worker protocol as a complete native-call safety boundary for inherited interfaces or concurrent unload/call scenarios. Until item 8 (JSON contract) is corrected, type shapes that look supported but are not JSON round-trippable may fail at runtime with misleading errors.
