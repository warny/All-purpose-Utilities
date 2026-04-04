using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;

using Utils.Parser.VisualStudio.Worker;

if (args.Length < 1)
{
    await Console.Error.WriteLineAsync("Usage: Utils.Parser.VisualStudio.Worker <pipe-name>");
    return 1;
}

string pipeName = args[0];
using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

try
{
    await pipe.ConnectAsync(10_000);
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Failed to connect to pipe '{pipeName}': {ex.Message}");
    return 1;
}

var host = new WorkerPluginHost();
using var reader = new StreamReader(pipe, leaveOpen: true);
using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

while (true)
{
    string? line;
    try
    {
        line = await reader.ReadLineAsync();
    }
    catch
    {
        break;
    }

    if (line is null)
    {
        break;
    }

    ClassifyRequest? request;
    try
    {
        request = JsonSerializer.Deserialize<ClassifyRequest>(line);
    }
    catch
    {
        continue;
    }

    if (request is null)
    {
        continue;
    }

    ClassifyResponse response = host.Classify(request);

    try
    {
        await writer.WriteLineAsync(JsonSerializer.Serialize(response));
    }
    catch
    {
        break;
    }
}

return 0;
