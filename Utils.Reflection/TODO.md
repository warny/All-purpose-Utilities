# Utils.Reflection — Historical improvement log

This file records the first two quality and security reviews of `Utils.Reflection` (`Reflection/`, `Reflection/Emit/`, and `ProcessIsolation/`). Items 1–36 have been implemented, documented, or explicitly declined. Active findings are tracked in the dated TODO files.

## Security and isolation

### 1. ~~Mutable `ProcessContainerPermissions.Default` singleton~~ — **implemented**
`Default` now returns a fresh immutable, init-only permission object on every access.

### 2. ~~Generated C# built from unvalidated reflection names~~ — **implemented**
`LibraryMapper.Emit<TInterface>` now performs generation and native calls in a worker process. The former in-process behavior remains available through the explicitly experimental `EmitInProcess<TInterface>` API. Cross-process types are validated by `CrossProcessMarshaling`.

### 3. ~~macOS sandbox always allows `process*` and `file-read*`~~ — **documented**
The broader read/process posture is documented, including the limited effect of `GrantDirectoryReadAccess` and `AllowProcessDebugging` on macOS.

### 4. ~~Linux bubblewrap mounts the whole filesystem read-only~~ — **documented**
The cross-platform difference from Windows AppContainer is documented. `BuildArguments` was extracted for tests.

### 5. ~~Sandboxed children inherit the complete environment~~ — **implemented**
`SandboxedProcessEnvironment` now applies an allowlisted environment on Linux and macOS. Windows support was added in item 26.

### 6. ~~Authenticode verification reports success on non-Windows systems~~ — **implemented**
`HasValidAuthenticodeSignature` now throws `PlatformNotSupportedException` outside Windows.

### 7. ~~Named-pipe client identity is assumed valid on Unix~~ — **documented**
The absence of `SO_PEERCRED`/`getpeereid` verification is explicitly documented.

### 8. ~~AppContainer creation ignores Job Object creation failure~~ — **implemented**
The AppContainer SID is released and creation fails when the Job Object cannot be created.

### 9. ~~`AssignProcessToJobObject` result is ignored~~ — **implemented**
Assignment failure terminates the newly created process and raises a diagnostic exception.

## Code generation and native mapping

### 10. ~~`EmitDllMappableClass` cache is not thread-safe~~ — **implemented**
The cache now uses `ConcurrentDictionary<..., Lazy<Type>>` with `ExecutionAndPublication`, preventing duplicate compilation and assembly-load races.

### 11. ~~Missing emitted type can be cached as `null`~~ — **implemented**
Compilation now throws a deterministic `InvalidOperationException` when the generated type cannot be found.

### 12. ~~Fragile compiler reference resolution~~ — **implemented**
Compilation prefers `TRUSTED_PLATFORM_ASSEMBLIES`, deduplicates paths, and retains a fallback for non-standard hosts.

### 13. ~~ACL grant failures are silently swallowed~~ — **implemented**
Expected filesystem/security failures are logged through `Trace.TraceWarning`.

### 14. ~~Unmanaged leak in Authenticode verification~~ — **implemented**
`WINTRUST_FILE_INFO` is destroyed before its unmanaged outer allocation is freed.

### 15. ~~`AppContainerSandbox` lacks a finalizer~~ — **implemented**
A standard `Dispose(bool)`/finalizer pattern was added.

### 16. ~~`Platform.NativeULongSize` documentation does not match the API~~ — **implemented**
The setter was made public to match the documented contract.

### 17. ~~Native mapping failures lack member/export context~~ — **implemented**
Invalid delegate members, missing setters, and delegate-construction failures now produce contextual exceptions.

## API and maintainability

### 18. ~~`CommandAvailability.Exists` ignores `PATHEXT` on Windows~~ — **implemented**
Bare executable names are resolved using the current Windows `PATHEXT` list.

### 19. ~~Inconsistent generic parameter names~~ — **implemented**
Public and internal mapping APIs consistently use `TInterface`.

### 20. Different error philosophies inside the package — **declined**
No additional `TryCreate`/`TryEmit` surface was introduced without a concrete requirement.

### 21. ~~`Platform.IsMacOsX` naming differs from the rest of the API~~ — **implemented**
`Platform.IsMacOS` was added as a compatibility-preserving alias.

### 22. ~~Apple deprecates `sandbox-exec`~~ — **documented**
The platform limitation and likely future migration requirement are documented.

### 23. ~~Unbounded static emitted-type cache~~ — **documented**
The cache lifetime and suitability only for a bounded set of known interfaces are documented.

## Test coverage

### 24. ~~Process-isolation argument/profile generation is barely tested~~ — **implemented**
Pure builders were extracted and covered for bubblewrap, sandbox-exec, Windows command-line quoting, environment filtering, and command lookup.

### 25. ~~Generated mapping lacks robustness tests~~ — **implemented**
Coverage now includes non-interface rejection, `ref`/`out`, explicit `[Out]`, concurrent emission, valid C# attribute ordering, and duplicate-compilation prevention.

## Post-review corrections

The automated review of PR #428 found and fixed three additional defects:

- Unix named pipes were unreachable from the sandbox because the socket directory was hidden.
- Public-field structs were serialized as `{}` without shared `IncludeFields` JSON options.
- Framework-dependent applications relaunched `dotnet` without the managed entry assembly path.

## Second review

### 26. ~~Windows AppContainer worker inherits the full host environment~~ — **implemented**
A filtered Unicode environment block is now passed to `CreateProcess`.

### 27. ~~Process-wide mutable `NativeULongSize` and `StructPackingSize` settings~~ — **documented**
Their global, unsynchronized nature and startup-only usage requirement are documented.

### 28. ~~No timeout for worker Load/Call/Shutdown exchanges~~ — **implemented**
Load, call, connection, and shutdown operations now have bounded waits. Later concurrent-protocol work changed timeout semantics from killing the worker to abandoning only the timed-out request.

### 29. ~~Generic interfaces and generic methods are not rejected explicitly~~ — **implemented**
Unsupported generic metadata shapes now fail before source generation with clear `NotSupportedException` messages.

### 30. ~~Breaking behavior change shipped without a major version/changelog entry~~ — **implemented**
The package version was raised to 2.0.0 and the breaking isolation behavior was recorded in `CHANGELOG.md`.

### 31. ~~Remote worker exceptions omit the remote stack trace~~ — **implemented**
`WorkerResponse` and `EmitWorkerInvocationException` now carry and display the remote stack trace.

### 32. ~~One complete worker process is started per mapped interface~~ — **implemented**
`EmitWorkerPool` provides opt-in worker sharing with per-interface handles and unload support.

### 33. ~~Per-call JSON/IPC cost is undocumented~~ — **documented**
The README explains serialization, named-pipe overhead, concurrency, and when in-process mapping is more appropriate.

### 34. ~~A worker cannot execute calls concurrently~~ — **implemented**
Requests and responses are correlated by ID; the host supports several requests in flight and the worker dispatches calls concurrently. This also fixed generated `void` signatures.

### 35. ~~No multi-process round-robin API~~ — **implemented**
`EmitRoundRobin<TInterface>` distributes calls across a fixed set of worker processes.

### 36. ~~No detailed process/thread architecture documentation~~ — **implemented**
`docs/reflection/ProcessAndThreadModel.md` documents exclusive workers, pools, concurrent calls, native loading, process containers, and round-robin operation.

## Active work

See:

- `TODO-2026-07-11.md` for protocol, lifecycle, and native-mapping findings.
- `TODO-2026-07-11-pass3.md` for platform isolation findings and later review passes.
