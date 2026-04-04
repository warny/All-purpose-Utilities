using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Utils.Parser.VisualStudio.Sandbox;

namespace Utils.Parser.VisualStudio.Worker;

/// <summary>
/// Manages the lifecycle of the plugin worker process and communicates with it via a named pipe.
/// When running on Windows, the worker is launched inside an <see cref="AppContainerSandbox"/>
/// which restricts network access and file-system writes without requiring elevation.
/// </summary>
internal sealed class PluginWorkerProcess : IAsyncDisposable
{
    private readonly string workerExePath;
    private readonly AppContainerSandbox? sandbox;
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private int nextId;
    private Process? workerProcess;
    private NamedPipeServerStream? serverPipe;
    private StreamReader? pipeReader;
    private StreamWriter? pipeWriter;

    private PluginWorkerProcess(string workerExePath, AppContainerSandbox? sandbox)
    {
        this.workerExePath = workerExePath;
        this.sandbox = sandbox;

        if (sandbox is not null && OperatingSystem.IsWindows())
        {
            // Pre-grant the AppContainer read access to the plugin directory so the worker
            // can load DLLs placed there by the user.
            sandbox.GrantDirectoryReadAccess(PluginDirectoryLocator.PluginDirectory);
        }
    }

    /// <summary>
    /// Locates the worker executable and creates the sandbox.
    /// Returns <see langword="null"/> when the worker executable is not deployed.
    /// </summary>
    public static PluginWorkerProcess? TryCreate()
    {
        string extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string workerExe = Path.Combine(extensionDir, "worker", "Utils.Parser.VisualStudio.Worker.exe");
        if (!File.Exists(workerExe))
        {
            return null;
        }

        // Best-effort: create the sandbox. Falls back to an unsandboxed worker if setup fails.
        AppContainerSandbox? sandbox = OperatingSystem.IsWindows()
            ? AppContainerSandbox.TryCreate()
            : null;

        return new PluginWorkerProcess(workerExe, sandbox);
    }

    /// <summary>
    /// Sends a batch of tokens to the worker process for out-of-process classification.
    /// Returns an empty dictionary when the worker is unavailable or encounters an error.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string?>> ClassifyAsync(
        string[] assemblyPaths,
        string fileExtension,
        string[] tokens,
        CancellationToken cancellationToken)
    {
        if (tokens.Length == 0 || assemblyPaths.Length == 0)
        {
            return new Dictionary<string, string?>();
        }

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            await EnsureWorkerAsync(cancellationToken);

            int id = Interlocked.Increment(ref nextId);
            var request = new ClassifyRequest(id, assemblyPaths, fileExtension, tokens);
            await pipeWriter!.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), cancellationToken);

            string? responseLine = await pipeReader!.ReadLineAsync(cancellationToken);
            if (responseLine is null)
            {
                return new Dictionary<string, string?>();
            }

            ClassifyResponse? response = JsonSerializer.Deserialize<ClassifyResponse>(responseLine);
            return response?.TokenClassifications ?? new Dictionary<string, string?>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Worker died or pipe broke — reset so the next call can restart it.
            await ResetWorkerAsync();
            return new Dictionary<string, string?>();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task EnsureWorkerAsync(CancellationToken cancellationToken)
    {
        if (workerProcess is { HasExited: false })
        {
            return;
        }

        await ResetWorkerAsync();

        string pipeName = $"Utils.Parser.VS.Worker.{Guid.NewGuid():N}";
        serverPipe = CreateServerPipe(pipeName);

        workerProcess = StartWorkerProcess(pipeName);

        using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        await serverPipe.WaitForConnectionAsync(startupTimeout.Token);

        pipeReader = new StreamReader(serverPipe, leaveOpen: true);
        pipeWriter = new StreamWriter(serverPipe, leaveOpen: true) { AutoFlush = true };
    }

    /// <summary>
    /// Creates the named pipe server. When a sandbox is active, a <see cref="PipeSecurity"/>
    /// ACL is applied so the AppContainer SID can connect.
    /// </summary>
    private NamedPipeServerStream CreateServerPipe(string pipeName)
    {
        if (sandbox is null || !OperatingSystem.IsWindows())
        {
            return new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        return CreateSecuredServerPipe(pipeName);
    }

    [SupportedOSPlatform("windows")]
    private NamedPipeServerStream CreateSecuredServerPipe(string pipeName)
    {
        // Grant the AppContainer SID read+write access to this pipe so the sandboxed
        // worker can connect. The pipe is randomly named (GUID) so there is no
        // security concern in allowing the container to connect to it.
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            sandbox!.GetContainerSid(),
            PipeAccessRights.ReadWrite,
            System.Security.AccessControl.AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            System.Security.Principal.WindowsIdentity.GetCurrent().User!,
            PipeAccessRights.FullControl,
            System.Security.AccessControl.AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
            inBufferSize: 0, outBufferSize: 0, pipeSecurity);
    }

    /// <summary>
    /// Starts the worker process — inside the AppContainer sandbox when available,
    /// or as a plain child process otherwise.
    /// </summary>
    private Process StartWorkerProcess(string pipeName)
    {
        if (sandbox is not null && OperatingSystem.IsWindows())
        {
            return sandbox.StartProcess(workerExePath, pipeName);
        }

        var psi = new ProcessStartInfo(workerExePath, pipeName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start plugin worker process.");
    }

    private async Task ResetWorkerAsync()
    {
        pipeReader?.Dispose();
        pipeWriter?.Dispose();
        serverPipe?.Dispose();
        pipeReader = null;
        pipeWriter = null;
        serverPipe = null;

        if (workerProcess is not null)
        {
            try
            {
                if (!workerProcess.HasExited)
                {
                    workerProcess.Kill(entireProcessTree: true);
                    await workerProcess.WaitForExitAsync();
                }
            }
            catch
            {
                // Best-effort termination; the Job Object's KillOnJobClose covers the rest.
            }
            finally
            {
                workerProcess.Dispose();
                workerProcess = null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ResetWorkerAsync();
        sandbox?.Dispose();
        semaphore.Dispose();
    }
}
