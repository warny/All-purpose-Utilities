# omy.Utils.Reflection (native interop and process isolation)

`omy.Utils.Reflection` provides helpers for loading unmanaged DLLs at runtime, detecting the current platform, and launching isolated child processes.

## Install
```bash
dotnet add package omy.Utils.Reflection
```

## Supported frameworks
- net8.0

## Features
- Dynamic native DLL binding via `LibraryMapper` and `ExternalAttribute`.
- Runtime IL emission to map an interface directly to DLL exports.
- Platform and CPU-architecture detection via `Platform`.
- OS-level process isolation (Windows AppContainer, Linux bubblewrap, macOS sandbox-exec).
- `CommandAvailability.Exists` to probe executables on `PATH`.

## Quick usage
```csharp
using Utils.Reflection;

if (Platform.IsWindows)
    Console.WriteLine($"Windows x64={Platform.IsX64}");

using var lib = LibraryMapper.Create<MyNativeLib>("path/to/native.dll");
lib.ComputeSum(1, 2); // delegates to native export
```

## Platform detection examples

`Platform` exposes static read-only properties set once at startup.

```csharp
using Utils.Reflection;

// OS
Console.WriteLine(Platform.IsWindows);  // true / false
Console.WriteLine(Platform.IsLinux);
Console.WriteLine(Platform.IsMacOsX);
Console.WriteLine(Platform.IsAndroid);
Console.WriteLine(Platform.IsBrowser);  // WebAssembly

// CPU
Console.WriteLine(Platform.IsX64);      // true on 64-bit AMD/Intel
Console.WriteLine(Platform.IsArm64);    // true on 64-bit ARM
Console.WriteLine(Platform.Uses64BitRuntime); // IntPtr.Size == 8

// Interop sizing (relevant for PKCS#11 / CRYPTOKI structs)
Console.WriteLine(Platform.NativeULongSize);   // 4 on Windows, IntPtr.Size on others
Console.WriteLine(Platform.StructPackingSize); // 1 on Windows, 0 on Unix
```

## LibraryMapper examples

`LibraryMapper` loads a native DLL and wires delegate-typed properties or fields decorated with `[External]` to the corresponding exports.

### Subclass approach

```csharp
using System.Runtime.InteropServices;
using Utils.Reflection;

// 1. Declare delegate types and decorate fields with [External]
public class MathLib : LibraryMapper
{
    [External("add")]
    public Func<int, int, int> Add;

    [External] // member name "Subtract" used as export name
    public Func<int, int, int> Subtract;
}

// 2. Load the DLL — Dispose() unloads it
using var lib = LibraryMapper.Create<MathLib>("math.dll");
int result = lib.Add(3, 4); // 7
```

### Interface emit approach

When you don't want to define a subclass, `Emit<TInterface>` generates the implementation at runtime — in an
**isolated worker process** by default, so a maliciously crafted interface (see the security note
below) can only do as much damage as the OS sandbox permits, never running with the full trust of your
process:

```csharp
using System.Runtime.InteropServices;
using Utils.Reflection;

public interface IMathLib : IDisposable
{
    [External("add")]
    int Add(int a, int b);

    [External("subtract")]
    int Subtract(int a, int b);
}

using IMathLib lib = LibraryMapper.Emit<IMathLib>("math.dll", CallingConvention.Cdecl);
int result = lib.Add(10, 3); // 13
```

`Emit<TInterface>` re-launches the current executable as a sandboxed worker process and forwards every call to
it over a named pipe. **This requires your application's entry point to call
`LibraryMapper.RunWorkerIfRequested(args)` as the very first statement of `Main`**, before any other
startup logic — otherwise the re-launched copy of your process won't recognize that it should run as a
worker, and `Emit<TInterface>` will fail to start it:

```csharp
static void Main(string[] args)
{
    if (LibraryMapper.RunWorkerIfRequested(args))
    {
        return; // this process instance is a worker; it already ran its request loop.
    }

    // ... normal application startup ...
}
```

Only interfaces whose members use JSON-representable types (primitives, `string`, enums, and
arrays/structs made of these) can be mapped this way, since every call round-trips to the worker
process; `IntPtr`/pointers/handles are never supported, because they are meaningless outside the
process that produced them. `Emit<TInterface>` throws `NotSupportedException` immediately (before starting any
process) when the interface doesn't qualify.

> **Performance note.** Isolation is not free: every call is JSON-serialized, sent over a named pipe to
> the worker, executed there, and the result serialized back — a real cost compared to a direct P/Invoke
> or `EmitInProcess<TInterface>` call. Calls from multiple threads on the same worker do run concurrently
> (each is independently correlated to its response, and the worker dispatches every request to the
> thread pool instead of handling them one at a time) — but the native library backing the interface must
> itself be safe to call concurrently for that to be safe; this is entirely the caller's responsibility.
> For a tight, high-frequency call loop where the per-call IPC overhead matters and the interface is
> fully trusted, prefer `EmitInProcess<TInterface>` instead. `loadTimeout`/`callTimeout` parameters on
> `Emit<TInterface>` bound how long a call can block waiting for the worker (30 seconds by default for
> each) before it times out; unlike a full worker restart, a single call timing out does not affect any
> other concurrently in-flight call on the same worker.

#### In-process emit (no isolation)

For interfaces that need pointer/handle parameters, or when you fully trust the interface definition
and want to avoid the cost of a worker process, `LibraryMapper.EmitInProcess<TInterface>` reproduces the
original (pre-isolation) behavior: it compiles and loads the generated mapping class directly in the
current process.

> **Security warning:** the code generator builds C# source by concatenating type/method/parameter
> names obtained through reflection on `TInterface`. CLR metadata names are far less constrained than C#
> identifiers, so an interface sourced from an untrusted or dynamically generated assembly could
> inject arbitrary C# — including a static constructor that runs with the full trust of this process.
> Only call `EmitInProcess<TInterface>` with interfaces you fully trust (typically ones you compiled yourself).

Because of that risk, `EmitInProcess<TInterface>` (and the lower-level `EmitDllMappableClass.Emit` it is built
on) are marked `[Experimental("UTILSREFL001")]`: calling them without acknowledging the risk fails to
compile. Suppress the diagnostic explicitly to opt in:

```csharp
#pragma warning disable UTILSREFL001 // Trusted interface, compiled by us — see the security note above.
using IMathLib lib = LibraryMapper.EmitInProcess<IMathLib>("math.dll", CallingConvention.Cdecl);
#pragma warning restore UTILSREFL001
```

#### Sharing one worker across several interfaces (`EmitWorkerPool`)

`Emit<TInterface>` re-launches a brand new sandboxed worker process every time it is called — a full CLR
startup per mapped interface. When mapping several interfaces that come from a common trust boundary (for
example, several DLLs from the same vendor/build) and the per-call process-spawn cost matters,
`EmitWorkerPool` starts one shared worker on its first `Emit<TInterface>` call and reuses it for every
subsequent call on the same pool instance:

```csharp
using var pool = new EmitWorkerPool();

using IMathLib math = pool.Emit<IMathLib>("math.dll", CallingConvention.Cdecl);
using IStringLib strings = pool.Emit<IStringLib>("strings.dll", CallingConvention.Cdecl);
// Both proxies forward calls to the SAME worker process.

math.Add(1, 2);
strings.ToUpper("hi");
```

This trades away some isolation between the interfaces mapped through the same pool: a crash or a
hostile/misbehaving interface loaded on the shared worker can affect every other interface loaded on it,
where `Emit<TInterface>`'s one-worker-per-interface default keeps failures contained to a single
interface. Disposing a proxy returned by the pool only releases that interface's resources on the shared
worker; dispose the pool itself to shut the worker process down.

#### Spreading calls across several processes (`EmitRoundRobin`)

Calls from multiple threads on a single `Emit<TInterface>` worker already run concurrently (see the
performance note above) — but they all still execute inside the same worker process, so the native
library must itself be safe to call concurrently. `LibraryMapper.EmitRoundRobin<TInterface>` sidesteps
that requirement by starting several independent worker processes up front and picking the next one, in
round-robin order, for every call — each process has its own separate load of the native DLL, so calls
can never race inside the same one:

```csharp
using IMathLib math = LibraryMapper.EmitRoundRobin<IMathLib>("math.dll", CallingConvention.Cdecl, workerCount: 4);

// Each call goes to the next of the 4 worker processes, in turn.
int a = math.Add(1, 2);
int b = math.Add(3, 4);
```

This costs `workerCount` full sandboxed process startups up front, rather than one — pick a worker count
that matches the parallelism you actually need. The set of workers is fixed for the proxy's lifetime;
disposing it disposes every worker in the set.

## Process isolation examples

`ProcessContainerFactory` wraps OS sandboxing APIs to constrain child processes. It returns `null` gracefully when no sandbox is available.

### Default (read-only) container

```csharp
using Utils.Reflection.ProcessIsolation;

IProcessContainer? container = ProcessContainerFactory.TryCreate(
    windowsContainerName:  "MyAppSandbox",
    windowsDisplayName:    "My App Sandbox",
    windowsDescription:    "Isolated worker for untrusted plugins");

if (container is not null)
{
    using (container)
    {
        // Grant read access to the plugin directory
        container.GrantDirectoryReadAccess(@"C:\plugins");

        System.Diagnostics.Process p = container.StartProcess(
            @"C:\plugins\worker.exe",
            ["--mode", "safe"]);
        p.WaitForExit();
    }
}
else
{
    // Fallback: run without extra isolation
}
```

### Custom permissions

```csharp
var permissions = new ProcessContainerPermissions
{
    AllowDiskRead    = true,  // required for .NET assemblies to load
    AllowDiskWrite   = false,
    AllowNetwork     = false,
    AllowDeviceAccess = false,
};

IProcessContainer? container = ProcessContainerFactory.TryCreate(
    "WorkerSandbox", "Worker Sandbox", "Plugin worker",
    permissions);
```

> **Note:** On Windows, any permission beyond `AllowDiskRead` disables AppContainer isolation and `TryCreate` returns `null`. On Linux, `bwrap` (bubblewrap) is used; on macOS, `sandbox-exec`.

> **Note — file-read scope differs by platform.** Windows (`AppContainerSandbox`) denies file reads
> by default and only grants them per-directory via `GrantDirectoryReadAccess`. Linux and macOS
> currently grant read access to the entire host filesystem to every sandboxed process (subject to
> normal OS user permissions) regardless of what's granted through `GrantDirectoryReadAccess`, which
> is a no-op on those platforms. Don't assume identical read scoping across platforms when writing
> code against `IProcessContainer` generically.

### IPC ACL hardening (Windows)

```csharp
if (container is not null &&
    container.TryGetSecurityIdentifier(out var sid) && sid is not null)
{
    // Use sid to set ACLs on named pipes or shared-memory objects
    // so only the sandboxed process can access them.
}
```

## CommandAvailability examples

```csharp
using Utils.Reflection.ProcessIsolation;

if (CommandAvailability.Exists("bwrap"))
    Console.WriteLine("bubblewrap is available");

if (CommandAvailability.Exists(@"C:\tools\ffmpeg.exe"))
    Console.WriteLine("ffmpeg found");
```

## Related packages
- `omy.Utils` – base utilities used by reflection helpers.
- `omy.Utils.IO` – binary serialization that uses `PropertyOrFieldInfo` from this package.
