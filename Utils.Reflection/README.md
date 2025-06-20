# Utils.Reflection Library

**Utils.Reflection** extends the .NET reflection APIs with helpers for dynamic loading and runtime code generation.
It provides abstractions that help split platform-specific logic from high level processing.

## Features

- Dynamic DLL mapping and loading utilities
- Support for compiling and emitting C# code at runtime
- Platform detection helpers and `PropertyOrFieldInfo` wrapper
- Designed to work together with `Utils.VirtualMachine` for dynamic instruction handling
