# All-purpose Utilities

A collection of production-ready .NET utility libraries published as the **omy.Utils** package family. Each package focuses on a specific area (networking, I/O, data, imaging, etc.) and is built to be consumed directly from NuGet without preview SDKs.

## Packages at a glance

- **omy.Utils** – foundational helpers used across the other packages (arrays, collections, expressions, strings, streams, security).
- **omy.Utils.Net** – DNS, ICMP, network discovery, Wake-on-LAN, and URI builder helpers.
- **omy.Utils.IO** – stream utilities, base16/base32/base64 converters, and binary serialization helpers.
- **omy.Utils.Data** – attributes and mappers for `IDataRecord`/`IDataReader` to typed objects.
- **omy.Utils.Imaging** – bitmap accessors, vector drawing primitives, and color helpers.
- **omy.Utils.Fonts** – TrueType/PostScript parsing, encoding tables, and glyph metrics utilities.
- **omy.Utils.Geography** – coordinate models, map projections, and tile helpers.
- **omy.Utils.Mathematics** – advanced math helpers (FFT, derivation/integration, SI conversions, linear algebra).
- **omy.Utils.Reflection** – reflection extensions such as `PropertyOrFieldInfo` and dynamic delegate invocation.
- **omy.Utils.Xml** – attribute-driven XML processing and `XmlDataProcessor` helpers.
- **omy.Utils.VirtualMachine** – minimal VM framework with attribute-defined instructions and configurable endianness.
- **omy.Utils.OData** – OData client helpers and metadata utilities.
- **omy.Utils.OData.Generators** – Roslyn source generator for OData models from EDMX metadata.
- **omy.Utils.DependencyInjection** – attribute-based dependency injection helpers.
- **omy.Utils.DependencyInjection.Generators** – Roslyn source generator that emits DI registrations.
- **omy.Utils.IO.Serialization.Generators** – Roslyn source generator for stream serializers.
- **omy.Utils.Parser** – self-describing universal parser: load any ANTLR4 `.g4` grammar at runtime and tokenize/parse source text without code generation.

> Additional project-level READMEs provide deeper details for specialized packages like serialization or generators.

## Install from NuGet

Use the package that matches your scenario. Examples:

```bash
dotnet add package omy.Utils
# or
dotnet add package omy.Utils.Net
```

> All packages target stable TFMs (primarily `net8.0` and `net9.0`). Only building the repository may require the latest SDK.

## Quick usage

```csharp
using Utils.Net;

var builder = new UriBuilderEx("http://example.com");
builder.QueryString["key"].Add("value");
Console.WriteLine(builder.ToString());
```

More examples are available inside each package README (see `Utils/README.md` for the base library).

## Documentation

- [Getting started](docs/getting-started.md)
- [GitHub about summary](docs/github-about.md)
- [Releasing guide](docs/releasing.md)
- [Changelog](CHANGELOG.md)

## External worker process permissions (Plugin isolation)

Some components (notably parser plugin isolation) execute user-provided DLL logic in an **external worker process**. This is expected and intentional: the worker process is where sandboxing is applied.

Runtime permissions can be configured with environment variables:

| Permission | Environment variable | Default | Windows (AppContainer mode) | Linux (`bwrap`) | macOS (`sandbox-exec`) |
|---|---|---:|---|---|---|
| Disk read | `PROCESS_WORKER_ALLOW_DISK_READ` | `true` | Required. If `false`, container creation is skipped. | Required. If `false`, container creation is skipped. | Required. If `false`, container creation is skipped. |
| Disk write | `PROCESS_WORKER_ALLOW_DISK_WRITE` | `false` | Not exposed in restrictive AppContainer mode. Requesting it falls back to direct process mode. | Controls writable `/tmp` bind vs read-only tmpfs behavior. | Adds/removes `file-write*` rule in profile. |
| Network | `PROCESS_WORKER_ALLOW_NETWORK` | `false` | Not exposed in restrictive AppContainer mode. Requesting it falls back to direct process mode. | Controls `--share-net`. | Adds/removes `network*` rule in profile. |
| Device access | `PROCESS_WORKER_ALLOW_DEVICE_ACCESS` | `false` | Not exposed in restrictive AppContainer mode. Requesting it falls back to direct process mode. | Controls host `/dev` bind vs minimal device namespace. | Adds/removes `iokit-open` rule in profile. |
| Process debugging | `PROCESS_WORKER_ALLOW_DEBUGGING` | `false` | Not exposed in restrictive AppContainer mode. Requesting it falls back to direct process mode. | Reserved for future hardening controls. | Adds/removes `process-info*` rule in profile. |

> If no supported container backend is available (or if requested permissions are incompatible with the selected restrictive backend), the runtime falls back to launching a regular child process.

### Additional permissions worth adding

The current model is intentionally small. Useful next additions could be:

- **CPU quota / priority** (throttling untrusted plugins).
- **Memory limit** (hard cap to prevent worker OOM impact on host).
- **Execution time budget** per request (separate from IPC timeout).
- **Allowed path allowlist** (restrict reads to plugin directory + explicit folders).
- **Network destination allowlist** (host/IP/port filtering, not only on/off).
- **Process creation control** (forbid spawning child processes from worker).
- **Environment variable allowlist** (avoid leaking host secrets).
- **System call profile selection** (Linux seccomp policy presets).

## Build from source

The solution targets **.NET 9** for development. To build locally:

```bash
dotnet build
```

Unit tests live in the `UtilsTest` project and can be run with `dotnet test`.

## License

This project is distributed under the Apache 2.0 license (see `LICENSE-apache-2.0.txt`).
