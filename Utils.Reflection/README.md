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

When you don't want to define a subclass, `Emit<I>` generates the implementation at runtime:

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
