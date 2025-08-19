# Utils.Reflection Library

**Utils.Reflection** extends the .NET reflection APIs with helpers for dynamic loading and runtime code generation.
It targets **.NET 9** and provides abstractions that help split platform-specific logic from high level processing.

## Features

- Dynamic DLL mapping and loading utilities
- Support for compiling and emitting C# code at runtime
- Platform detection helpers and `PropertyOrFieldInfo` wrapper
- Designed to work together with `Utils.VirtualMachine` for dynamic instruction handling

## Usage example

```csharp
// Detect the current OS
if (Utils.Reflection.Platform.IsWindows)
    Console.WriteLine("Windows detected");

// Map a native method from a DLL
class KernelApi : Utils.Reflection.LibraryMapper
{
    [Utils.Reflection.LibraryMapper.External("GetTickCount")]
    public Func<uint> GetTickCount = null!;
}
using var kernel = Utils.Reflection.LibraryMapper.Create<KernelApi>("kernel32.dll");
uint ticks = kernel.GetTickCount();
```
