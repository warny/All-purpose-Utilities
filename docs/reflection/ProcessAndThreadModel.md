# Utils.Reflection — Process and Thread Model

Deep dive into how `Utils.Reflection` actually loads native DLLs, launches child processes, and
distributes calls across threads/processes. The [package README](../../Utils.Reflection/README.md)
covers day-to-day usage; this document covers the mechanics underneath, with runnable examples.

## 1. Loading a native DLL in a process

Two independent code paths load a native DLL and wire it to managed code. Both end at the same place
(`NativeLibrary.Load` + `NativeLibrary.GetExport` + `Marshal.GetDelegateForFunctionPointer`), but differ
in how the managed side is produced.

### 1a. Hand-written subclass — `LibraryMapper.Create<T>`

You write the class; `[External]` fields/properties are wired to native exports at `Create<T>` time.

```csharp
public class MathLib : LibraryMapper
{
    [External("add")]
    public Func<int, int, int> Add;
}

using var lib = LibraryMapper.Create<MathLib>("math.dll");
int result = lib.Add(3, 4); // 7
```

`LibraryMapper.MapLibraryToInstance` (`Reflection/LibraryMapper.cs`) reflects over `obj`'s members,
finds every `[External]`-decorated field/property, resolves the export by name
(`NativeLibrary.GetExport`), and assigns a delegate built with
`Marshal.GetDelegateForFunctionPointer`. No code generation involved — the class already exists.

### 1b. Generated class from an interface — `EmitDllMappableClass`/`Emit<TInterface>`

You write an interface; a mapping class is generated at runtime from it.

```csharp
public interface IMathLib : IDisposable
{
    [External("add")]
    int Add(int a, int b);
}
```

`EmitDllMappableClass.CompileMappingType` (`Reflection/Emit/EmitDllMappableClass.cs`) builds C# source
by walking the interface's methods with reflection — one `private delegate` per method (carrying
`[UnmanagedFunctionPointer(callingConvention)]`), one `[External]`-decorated field to hold it, and one
implementing method that forwards to the delegate. The source is compiled with Roslyn
(`CSharpCompilation`) and loaded via `AssemblyLoadContext.Default.LoadFromStream`. The resulting type
still derives from `LibraryMapper`, so the *exact same* `MapLibraryToInstance` step from 1a wires its
delegate fields to the native exports.

This is the path with a real security cost: the generated source concatenates reflection-derived
names (type/method/parameter names), which are CLR metadata identifiers, not C# identifiers — an
interface sourced from an untrusted or dynamically generated assembly could inject C# through a crafted
name. See the README's "In-process emit" section for the full warning. This is why `Emit<TInterface>`
(as opposed to `EmitInProcess<TInterface>`) runs this whole step *inside a sandboxed worker process*
instead of the calling process — see §2 and §3.

## 2. Emitting several interfaces in the same process — `EmitWorkerPool`

`Emit<TInterface>` starts one dedicated worker process per call. `EmitWorkerPool` starts one shared
worker and maps several interfaces onto it, at the cost of losing isolation *between* those interfaces
(a crash in one can affect the others — they now share a process, same as any two native libraries
loaded into the same process normally would).

```csharp
using var pool = new EmitWorkerPool();

using IMathLib math = pool.Emit<IMathLib>("math.dll", CallingConvention.Cdecl);
using IStringLib strings = pool.Emit<IStringLib>("strings.dll", CallingConvention.Cdecl);
// Both proxies forward calls to the SAME worker process.
```

### What actually happens on the wire

Each `pool.Emit<TInterface>()` call sends one `Load` request and gets back a `Handle` (an `int`,
allocated by `EmitWorkerHost.Run` — see `Reflection/Emit/EmitWorkerHost.cs`) identifying that interface's
loaded instance *on that worker*. Every later request (`Call`, `Unload`) carries that handle so the
worker knows which loaded instance to target:

```text
Host (EmitWorkerPool)                    Worker (EmitWorkerHost.Run)
──────────────────────                   ───────────────────────────
pool.Emit<IMathLib>("math.dll")
  → {Id:1, Kind:Load, Interface:IMathLib, DllPath:"math.dll"}
                                          Assembly.LoadFrom + EmitCore
                                          loaded[1] = (mathInstance, typeof(IMathLib))
  ← {Id:1, Success:true, Handle:1}

pool.Emit<IStringLib>("strings.dll")
  → {Id:2, Kind:Load, Interface:IStringLib, DllPath:"strings.dll"}
                                          loaded[2] = (stringsInstance, typeof(IStringLib))
  ← {Id:2, Success:true, Handle:2}

math.Add(1, 2)
  → {Id:3, Kind:Call, Handle:1, Method:Add, Args:["1","2"]}
                                          loaded[1] resolved → mathInstance.Add(1, 2)
  ← {Id:3, Success:true, Return:"3"}

math.Dispose()
  → {Id:4, Kind:Unload, Handle:1}
                                          loaded.Remove(1); mathInstance.Dispose()
  ← {Id:4, Success:true}
                                          // loaded[2] (stringsInstance) is untouched
```

`EmitWorkerHost.Run` keeps `loaded` as a `ConcurrentDictionary<int, LoadedInterface>`, so `Unload`-ing
one handle never disturbs another — see [§4](#4-running-a-call-on-a-thread-inside-a-worker) for why it
needs to be concurrent at all. `Handle`/`WorkerRequest`/`WorkerResponse` are defined in
`Reflection/Emit/EmitWorkerMessages.cs`.

## 3. Launching a command in a process

Two distinct mechanisms exist for starting a child process — they solve different problems and are not
interchangeable.

### 3a. Generic OS sandbox — `IProcessContainer.StartProcess`

For running *any* executable under OS-level isolation (Windows AppContainer, Linux bubblewrap, macOS
sandbox-exec):

```csharp
using Utils.Reflection.ProcessIsolation;

IProcessContainer? container = ProcessContainerFactory.TryCreate(
    windowsContainerName: "MyAppSandbox",
    windowsDisplayName: "My App Sandbox",
    windowsDescription: "Isolated worker for untrusted plugins");

if (container is not null)
{
    using (container)
    {
        container.GrantDirectoryReadAccess(@"C:\plugins");
        System.Diagnostics.Process p = container.StartProcess(@"C:\plugins\worker.exe", ["--mode", "safe"]);
        p.WaitForExit();
    }
}
```

`ProcessContainerFactory.TryCreate` picks the platform-specific implementation
(`AppContainerSandbox`/`LinuxBubblewrapContainer`/`MacOsSandboxExecContainer`, all in
`ProcessIsolation/`) based on `OperatingSystem.IsWindows()`/`IsLinux()`/`IsMacOS()`, and returns `null`
when no sandbox is available so the caller can fall back to running unsandboxed. This mechanism has no
idea what the launched executable *is* — it just constrains what OS resources it can touch (network,
disk, devices) once running.

### 3b. The Emit worker's own launch path — `EmitWorkerProcess.StartWorkerProcess`

For launching *this library's own* isolated worker specifically — a re-execution of the *current*
process with a marker argument (`Reflection/Emit/EmitWorkerProcess.cs`):

```csharp
// Simplified from EmitWorkerProcess.Start(TimeSpan? callTimeout):
IProcessContainer? sandbox = ProcessContainerFactory.TryCreate(...);   // reuses §3a under the hood
string[] arguments = BuildWorkerArguments(exePath, pipeName);          // [marker, pipeName] or
                                                                        // [assemblyPath, marker, pipeName]
Process process = sandbox is not null
    ? sandbox.StartProcess(exePath, arguments)                        // sandboxed (§3a again)
    : Process.Start(new ProcessStartInfo(exePath) { ... });           // unsandboxed fallback
```

Two things make this launch path different from a generic "run some other exe" case:

- **It relaunches the *same* executable, not a different one.** `Environment.ProcessPath` is used as
  `exePath` — the worker is a fresh instance of the calling application itself, re-entering through
  `LibraryMapper.RunWorkerIfRequested(args)` at the top of its own `Main` instead of running normally.
  `BuildWorkerArguments` also has to detect and work around being re-launched through the `dotnet` muxer
  (`dotnet MyApp.dll`) — see its XML doc for the exact logic.
- **It layers `IProcessContainer` underneath, it doesn't replace it.** `StartWorkerProcess` calls
  `sandbox.StartProcess` from §3a for the actual OS-level launch; what's specific to the Emit worker is
  everything *around* that call — computing the right arguments, choosing the worker's permission set
  (`CreateWorkerPermissions`, see the XML doc for why `AllowDiskWrite` is `true` on Linux/macOS but must
  stay `false` on Windows), and the named-pipe handshake that follows (`WaitForConnectionAsync`, then
  `RunReaderLoop` — see §4).

## 4. Running a call on a thread inside a worker

A single worker process handles multiple requests **concurrently**, not one at a time. This applies
uniformly — a plain `Emit<TInterface>` worker, an `EmitWorkerPool` shared worker, and each member of an
`EmitRoundRobin` set all behave the same way internally.

```csharp
using IMathLib math = LibraryMapper.Emit<IMathLib>("math.dll", CallingConvention.Cdecl);

// These two calls, from two threads, genuinely execute in parallel inside the worker —
// as long as math.dll itself tolerates concurrent calls. Utils.Reflection cannot verify that;
// it is the caller's responsibility.
var t1 = Task.Run(() => math.Add(1, 2));
var t2 = Task.Run(() => math.Add(3, 4));
await Task.WhenAll(t1, t2);
```

### How correlation makes this safe

Every `WorkerRequest`/`WorkerResponse` carries an `Id`. On the host side
(`EmitWorkerProcess.SendAndReceive`), each call registers a `TaskCompletionSource<WorkerResponse>` in a
`ConcurrentDictionary<int, TaskCompletionSource<WorkerResponse>>` keyed by that `Id`, then writes its
request (under a short lock that only protects the write itself, never the wait). A single background
task (`RunReaderLoop`, started once in the constructor) continuously reads response lines and completes
whichever `TaskCompletionSource` matches the response's `Id`:

```text
Thread A: InvokeMethod(Add(1,2))          Thread B: InvokeMethod(Add(3,4))         RunReaderLoop (background)
──────────────────────────────            ──────────────────────────────          ─────────────────────────
pending[10] = tcsA
write {Id:10, Call, Add, [1,2]}
                                           pending[11] = tcsB
                                           write {Id:11, Call, Add, [3,4]}
await tcsA.Task  (blocks)                 await tcsB.Task  (blocks)
                                                                                    read {Id:11, Return:"7"}
                                                                                    pending.Remove(11) → tcsB
                                                                                    tcsB.TrySetResult(...)
                                           ← returns 7
                                                                                    read {Id:10, Return:"3"}
                                                                                    pending.Remove(10) → tcsA
                                                                                    tcsA.TrySetResult(...)
← returns 3
```

Responses can arrive in *either* order — nothing assumes request order equals response order. On the
worker side, `EmitWorkerHost.Run` dispatches each request to `Task.Run` instead of handling it inline,
so the two `Add` calls above genuinely run on separate thread-pool threads inside the worker, not
queued behind each other.

### What a per-call timeout does *not* do anymore

Because responses are correlated by `Id`, a single request timing out (`Emit<TInterface>`'s
`callTimeout`, 30s by default) only fails *that* request — `TaskCompletionSource.TrySetCanceled`, entry
removed from `pending`. It does not kill the worker or affect any other in-flight call. A response that
arrives late for an abandoned request simply finds no matching `pending` entry and is silently dropped.
Only a genuinely broken connection (the background reader loop itself ending — EOF or an I/O fault) fails
every pending request and stops the worker, via `RunReaderLoop`'s `FailAllPending` + `KillSilently`.

## 5. Building — and calling into — a round-robin of processes

`EmitWorkerPool` (§2) shares one process across several *different* interfaces. `EmitRoundRobin`
(`LibraryMapper.EmitRoundRobin<TInterface>`) does the opposite: it spreads calls to the *same* interface
across several independent processes, so that a native library that cannot safely be called
concurrently in-process (§4's caveat) can still be called in parallel — each process has its own private
load of the DLL.

```csharp
using IMathLib math = LibraryMapper.EmitRoundRobin<IMathLib>(
    "math.dll", CallingConvention.Cdecl, workerCount: 4);

// Call 1 → worker 0, call 2 → worker 1, call 3 → worker 2, call 4 → worker 3, call 5 → worker 0, ...
int a = math.Add(1, 2);
int b = math.Add(3, 4);
```

### Building the set

`LibraryMapper.EmitRoundRobin<TInterface>` loops `workerCount` times, calling
`EmitWorkerProcess.Start(typeof(TInterface), dllPath, callingConvention, ...)` — the exact same
single-worker startup path `Emit<TInterface>` uses (§1b/§3b) — once per worker, collecting
`(EmitWorkerProcess Worker, int Handle)` pairs. If any iteration throws, every worker already started is
disposed before the exception propagates — no leaked processes on a partial failure.

```text
EmitRoundRobin<IMathLib>("math.dll", Cdecl, workerCount: 3)
  Start() → worker0, Load(IMathLib) → handle 1     [independent process #1, own math.dll load]
  Start() → worker1, Load(IMathLib) → handle 1     [independent process #2, own math.dll load]
  Start() → worker2, Load(IMathLib) → handle 1     [independent process #3, own math.dll load]

  members = [(worker0,1), (worker1,1), (worker2,1)]
  → DispatchProxy.Create<IMathLib, EmitWorkerRoundRobinProxy>().AttachWorkers(members)
```

### Dispatching calls into the set

`EmitWorkerRoundRobinProxy.Invoke` (`Reflection/Emit/EmitWorkerRoundRobinProxy.cs`) picks the next
member with a single atomic counter:

```csharp
int index = (int)((uint)Interlocked.Increment(ref nextIndex) % current.Length);
(EmitWorkerProcess worker, int handle) = current[index];
return worker.InvokeMethod(handle, targetMethod, args);
```

Casting to `uint` before the modulo keeps the index in range even after `nextIndex` overflows `int` back
to negative (~2^31 calls in) — no special-cased counter reset needed. Each selected `worker` then goes
through the exact same `InvokeMethod`/`SendAndReceive`/`RunReaderLoop` machinery described in §4 — a
round-robin set is not a different call mechanism, just several independent instances of the same one,
each one also individually capable of handling concurrent calls internally (a `workerCount: 4` round-robin
set where each worker also happens to receive 2 concurrent calls behaves exactly as §4 describes, 4 times
over). Disposing the proxy disposes every member; there is no dynamic replacement of a worker that dies
mid-flight — its future round-robin turns simply fail with whatever `InvokeMethod` raises for a broken
connection until the whole proxy is disposed and recreated.

## Related documents

- [`Utils.Reflection/README.md`](../../Utils.Reflection/README.md) — day-to-day usage and API reference.
- [`Utils.Reflection/TODO.md`](../../Utils.Reflection/TODO.md) — audit history; items 2/26/28/32/34/35
  cover the design decisions summarized here in more depth, including trade-offs considered and
  discarded.
