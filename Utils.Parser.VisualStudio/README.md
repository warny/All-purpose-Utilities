# Utils.Parser.VisualStudio

`Utils.Parser.VisualStudio` is a Visual Studio extension based on **VisualStudio.Extensibility (out-of-process)** that provides syntax colorization driven by descriptor files and user plugins.

## What it does

- Loads `*.syntaxcolor` descriptor files from the edited file's folder and parent folders.
- Resolves matching profiles for the current file extension.
- Produces editor `ClassificationTag` tags through an out-of-process `TextViewTagger`.
- Forwards classification to user-supplied plugin assemblies running in an isolated worker process.

## Descriptor files

Descriptor files (`.syntaxcolor`) define keyword lists for a given file extension. They are discovered by walking from the edited file's directory up to the filesystem root.

```text
@FileExtension : ".demo"

Keyword :
    SELECT | FROM | WHERE

Number :
    NUMBER

String :
    STRING_LITERAL
```

### Directives

| Directive | Description |
|---|---|
| `@FileExtension` | File extension this profile applies to (e.g. `".sql"`). Multiple directives can appear in a single file. |
| `@StringSyntaxExtension` | Associates a C# `[StringSyntax]` attribute name with this profile. |

### Syntax rules

- Comments: `#` or `//` (ignored inside quoted values)
- Multiple tokens per classification line, separated by `|`
- Classification name ends with `:` (can be quoted)
- Blank lines and comment-only lines are ignored

### Size limit

Files larger than **1 MB** are rejected before reading to prevent out-of-memory conditions from crafted descriptors.

---

## Plugin system

Users can extend syntax colorization by dropping `ISyntaxColorisation` assemblies into:

```
%LOCALAPPDATA%\Utils.Parser.VisualStudio\Plugins\
```

Plugin assemblies are **never loaded into the Visual Studio process**. They run in a dedicated worker process (`Utils.Parser.VisualStudio.Worker.exe`) that is isolated from the extension. A crash, hang, or malicious plugin affects only the worker — Visual Studio continues running normally.

### Writing a plugin

Implement `ISyntaxColorisation` from `Utils.Parser` and place the compiled DLL (with all its dependencies) in the plugin directory:

```csharp
public sealed class MyColorisation : ISyntaxColorisation
{
    public static readonly MyColorisation Instance = new();

    public IReadOnlyList<string> FileExtensions => [".myext"];

    public string? GetClassification(string token) => token switch
    {
        "SELECT" or "FROM" or "WHERE" => VisualStudioClassificationNames.Keyword,
        _ => null,
    };
}
```

The worker discovers `ISyntaxColorisation` implementations by:
1. Looking for a public static `Instance` property of the right type, or
2. Calling the public parameterless constructor.

Plugin results take priority over built-in descriptor-file profiles.

### Hot-reload

The worker caches each plugin by its last-write timestamp. Replacing the DLL on disk is picked up automatically on the next classification request without restarting Visual Studio.

---

## Security architecture

The plugin system was designed so that users do not need to trust third-party NuGet packages loaded by the extension. Several independent defence layers are stacked:

### 1 — Out-of-process isolation

Plugin assemblies run in `Utils.Parser.VisualStudio.Worker.exe`, a separate process. The extension communicates with it over a named pipe using newline-delimited JSON. No plugin code ever executes inside the Visual Studio process.

Each plugin assembly is loaded in its own collectible `AssemblyLoadContext`, allowing it to be unloaded when the file changes on disk (hot-reload) or when the worker is reset after a failure.

### 2 — AppContainer sandbox (Windows)

The worker is launched inside a **Windows AppContainer** — a lightweight isolation boundary that requires no elevation. The AppContainer restricts:

| Restriction | Effect |
|---|---|
| **Network** | `socket()` calls return `WSAEACCES`. The plugin cannot make outbound connections or listen on a port. |
| **File-system writes** | The plugin can only write to the container's own data folder (`%LOCALAPPDATA%\Packages\Utils.Parser.VisualStudio.PluginWorker.v1`). User files and the VS installation are read-only from the plugin's perspective. |
| **Registry writes** | Restricted to the container's own registry hive. |

The AppContainer profile (`Utils.Parser.VisualStudio.PluginWorker.v1`) is stored in `HKCU` and is created without elevation. The extension pre-grants the plugin directory read+execute access to the AppContainer SID so DLLs can be loaded.

If AppContainer setup fails for any reason, the extension degrades gracefully to an unsandboxed worker rather than blocking startup.

### 3 — Job Object lifecycle management

A **Win32 Job Object** with `KillOnJobClose` is attached to the worker process immediately after creation (before releasing the process handle). This guarantees that:

- The worker is automatically killed when the extension process exits, regardless of how that exit happens (normal shutdown, crash, task kill).
- There is no window where the worker could outlive the extension and continue executing.

The Job Object also applies full **UI restrictions** (`Handles`, `ReadClipboard`, `WriteClipboard`, `SystemParameters`, `DisplaySettings`, `GlobalAtoms`, `Desktop`, `ExitWindows`), preventing a malicious plugin from interacting with the desktop or clipboard.

### 4 — Named pipe security

The IPC pipe is hardened at two levels:

**ACL (access control list):** When a sandbox is active, the pipe is created with `NamedPipeServerStreamAcl.Create` and a `PipeSecurity` that grants `ReadWrite` only to the specific AppContainer SID. No other process — including other AppContainer instances or the current user's other processes — can connect.

**PID verification:** After `WaitForConnectionAsync` completes, `GetNamedPipeClientProcessId` (kernel32) is called to retrieve the PID of the connected client. If it does not match the PID of the worker process that was just started, the connection is rejected with an exception and the worker is reset. This closes the TOCTOU window between pipe creation and the worker connecting.

### 5 — Authenticode signature verification

Before any DLL path is forwarded to the worker, `PluginAssemblyVerifier` checks each file:

- **Signed DLL:** `WinVerifyTrust` (wintrust.dll) verifies the Authenticode PE signature and that the certificate chains to a trusted root. The DLL is loaded normally.
- **Unsigned DLL (default):** The DLL is silently skipped. The worker never sees it.
- **Unsigned DLL with explicit opt-in:** If the user creates an empty marker file named `{plugin}.dll.allow-unsigned` next to the DLL, it is allowed through. The marker is intentional friction — the user must take a deliberate per-file action.

Verification results are cached by `(dll last-write-time, marker last-write-time)`. The crypto check runs at most once per DLL version.

To opt in to an unsigned plugin:

```
%LOCALAPPDATA%\Utils.Parser.VisualStudio\Plugins\
    MyPlugin.dll
    MyPlugin.dll.allow-unsigned   ← create this empty file
```

Deleting the marker immediately re-blocks the DLL on the next classification request.

### 6 — IPC hardening

**Bounded reads:** The pipe reader never uses `ReadLineAsync` directly. `ReadBoundedLineAsync` reads in 4 KB chunks and throws `InvalidDataException` if the accumulated response exceeds **10 million characters** (~20 MB). This prevents a compromised worker from flooding the extension process with a huge JSON payload.

**Per-request timeout:** Every classify call is wrapped in a linked `CancellationTokenSource` with a **5-second** `CancelAfter`. A hung or slow plugin cannot stall the Visual Studio tagging pipeline indefinitely. The worker is reset after a timeout.

**Startup timeout:** `WaitForConnectionAsync` is given **10 seconds** to connect. If the worker process fails to start or connect within that window, the operation is cancelled and the worker is reset.

### 7 — Descriptor profile cache

Descriptor files (`.syntaxcolor`) are read and parsed on demand. The results are cached per tagger instance (one tagger per open document) and keyed by the **sorted list of descriptor file paths and their last-write timestamps**. As long as no file is added, removed, or modified on disk, subsequent tag requests reuse the cached profiles without any file I/O or parsing work.

The cache is invalidated automatically when:
- A new `.syntaxcolor` file appears in the directory tree of the open file.
- An existing descriptor file is modified or deleted.

### Summary table

| Layer | Mechanism | What it prevents |
|---|---|---|
| Process isolation | Separate `Worker.exe` process | Plugin crash/corruption reaching Visual Studio |
| Code isolation | Collectible `AssemblyLoadContext` | DLL lock, inability to hot-reload |
| Sandbox | Windows AppContainer | Network access, file-system writes by plugins |
| Lifecycle | Job Object `KillOnJobClose` | Orphaned worker processes |
| Desktop isolation | Job Object UI restrictions | Clipboard theft, desktop interaction |
| Pipe auth (ACL) | `PipeSecurity` with AppContainer SID | Unauthorized processes connecting to the pipe |
| Pipe auth (PID) | `GetNamedPipeClientProcessId` | TOCTOU pipe hijacking |
| Plugin vetting | `WinVerifyTrust` Authenticode check | Accidental or malicious unsigned DLL loading |
| Explicit opt-in | `.allow-unsigned` marker file | Silent loading of unsigned plugins |
| Payload size | `ReadBoundedLineAsync` 10 M char limit | Memory exhaustion from malicious IPC responses |
| Hung plugin | 5-second per-request timeout | VS tagging pipeline stall |
| Descriptor cache | (paths + timestamps) cache key | Repeated disk reads and parsing on every keystroke |

---

## Build and debug

1. Build the solution (the worker project is built and copied automatically via MSBuild targets).
2. Start debugging the extension from Visual Studio.
3. Open a file matching one of your descriptor extensions.

The worker executable is deployed to `$(OutputPath)worker\Utils.Parser.VisualStudio.Worker.exe`. If the file is absent at runtime, the extension falls back to in-process-only classification (built-in profiles still work; user plugins are silently unavailable).
