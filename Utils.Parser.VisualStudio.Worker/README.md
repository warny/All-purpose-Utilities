# Utils.Parser.VisualStudio.Worker

`Utils.Parser.VisualStudio.Worker` is a worker executable used by the Visual Studio integration for token classification through a named-pipe JSON protocol.

## Purpose

Decouple classification/parsing work from the main Visual Studio process by running parser logic in a dedicated process.

## Examples

### 1) Run the worker with a pipe name

```bash
dotnet run --project Utils.Parser.VisualStudio.Worker/Utils.Parser.VisualStudio.Worker.csproj -- my-pipe-name
```

### 2) Build the worker

```bash
dotnet build Utils.Parser.VisualStudio.Worker/Utils.Parser.VisualStudio.Worker.csproj
```

### 3) Message format (JSON line)

The worker reads JSON lines and responds with JSON (see `WorkerProtocol.cs`):

```json
{"assemblyPath":"...","typeName":"...","tokens":["if","var","customId"]}
```

The entry point and named-pipe handling are in `Program.cs`.

## Related projects

- [`Utils.Parser`](../Utils.Parser/README.md)
- [`Utils.Parser.VisualStudio`](../Utils.Parser.VisualStudio/README.md)
