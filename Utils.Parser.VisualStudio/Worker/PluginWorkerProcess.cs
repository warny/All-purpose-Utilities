using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Utils.Reflection.ProcessIsolation;

namespace Utils.Parser.VisualStudio.Worker;

/// <summary>
/// Manages the lifecycle of the plugin worker process and communicates with it via a named pipe.
/// Depending on the current OS, the worker is launched in the strongest available
/// process container (Windows AppContainer, Linux bubblewrap, macOS sandbox-exec).
/// </summary>
internal sealed class PluginWorkerProcess : IAsyncDisposable
{
    /// <summary>
    /// Maximum number of characters accepted from a single worker response.
    /// Guards against a malicious plugin flooding the extension process with a huge JSON payload.
    /// At ~2 bytes/char on average this is roughly 20 MB of raw JSON.
    /// </summary>
    private const int MaxResponseCharCount = 10 * 1024 * 1024;

    /// <summary>Per-request timeout. Prevents a hung plugin from blocking the tagger indefinitely.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly string workerExePath;
    private IProcessContainer? sandbox;
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private int nextId;
    private Process? workerProcess;
    private NamedPipeServerStream? serverPipe;
    private StreamReader? pipeReader;
    private StreamWriter? pipeWriter;

    private PluginWorkerProcess(string workerExePath, IProcessContainer? sandbox)
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
        ProcessContainerPermissions permissions = LoadPermissionsFromEnvironment();
        IProcessContainer? sandbox = ProcessContainerFactory.TryCreate(
            windowsContainerName: "Utils.Parser.VisualStudio.PluginWorker.v1",
            windowsDisplayName: "Utils.Parser.VisualStudio Plugin Worker",
            windowsDescription: "Isolated process that loads and evaluates user-provided ISyntaxColorisation plugins.",
            permissions: permissions);

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

            // Apply a per-request timeout independent of the VS cancellation token so a
            // hung or slow plugin cannot stall the tagger indefinitely.
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(RequestTimeout);

            string? responseLine = await ReadBoundedLineAsync(pipeReader!, MaxResponseCharCount, requestTimeout.Token);
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
            // Worker died, pipe broke, or response was oversized — reset for the next call.
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

        // Verify that the process that connected is the worker we just started, not
        // an opportunistic third-party process that observed the pipe name in the
        // worker's command-line arguments.
        if (OperatingSystem.IsWindows())
        {
            VerifyPipeClient(serverPipe, workerProcess);
        }

        pipeReader = new StreamReader(serverPipe, leaveOpen: true);
        pipeWriter = new StreamWriter(serverPipe, leaveOpen: true) { AutoFlush = true };
    }

    /// <summary>
    /// Checks that the process connected to <paramref name="pipe"/> matches
    /// <paramref name="expectedWorker"/>. Throws if the identity cannot be confirmed.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void VerifyPipeClient(NamedPipeServerStream pipe, Process expectedWorker)
    {
        IntPtr pipeHandle = pipe.SafePipeHandle.DangerousGetHandle();

        if (!WindowsNativeMethods.GetNamedPipeClientProcessId(pipeHandle, out uint clientPid))
        {
            throw new InvalidOperationException(
                "GetNamedPipeClientProcessId failed: could not verify the identity of the " +
                $"process connected to the plugin pipe (error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
        }

        if ((int)clientPid != expectedWorker.Id)
        {
            throw new InvalidOperationException(
                $"Security violation: PID {clientPid} connected to the plugin pipe but " +
                $"the expected worker PID is {expectedWorker.Id}. The connection was rejected.");
        }
    }

    /// <summary>
    /// Reads one line from <paramref name="reader"/>, rejecting responses longer than
    /// <paramref name="maxCharCount"/> to prevent a malicious worker from allocating
    /// unbounded memory in the extension process.
    /// </summary>
    private static async Task<string?> ReadBoundedLineAsync(
        StreamReader reader, int maxCharCount, CancellationToken cancellationToken)
    {
        var accumulator = new StringBuilder();
        char[] buf = new char[4096];

        while (true)
        {
            int read = await reader.ReadAsync(buf.AsMemory(0, buf.Length), cancellationToken);
            if (read == 0)
            {
                return accumulator.Length > 0 ? accumulator.ToString() : null;
            }

            int nlIndex = Array.IndexOf(buf, '\n', 0, read);
            if (nlIndex >= 0)
            {
                // Found the line terminator — check accumulated + pre-newline length before appending.
                if (accumulator.Length + nlIndex > maxCharCount)
                {
                    throw new InvalidDataException(
                        $"IPC response from plugin worker exceeded the {maxCharCount:N0}-character limit.");
                }

                accumulator.Append(buf, 0, nlIndex);

                // Strip trailing \r for CRLF line endings.
                if (accumulator.Length > 0 && accumulator[accumulator.Length - 1] == '\r')
                {
                    accumulator.Length--;
                }

                return accumulator.ToString();
            }

            // No newline yet — check total before accumulating.
            if (accumulator.Length + read > maxCharCount)
            {
                throw new InvalidDataException(
                    $"IPC response from plugin worker exceeded the {maxCharCount:N0}-character limit.");
            }

            accumulator.Append(buf, 0, read);
        }
    }

    /// <summary>
    /// Creates the named pipe server. When a sandbox is active, a <see cref="PipeSecurity"/>
    /// ACL is applied so only the AppContainer SID can connect.
    /// </summary>
    private NamedPipeServerStream CreateServerPipe(string pipeName)
    {
        if (!OperatingSystem.IsWindows() || sandbox is not AppContainerSandbox appContainerSandbox)
        {
            return new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        return CreateSecuredServerPipe(pipeName, appContainerSandbox);
    }

    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreateSecuredServerPipe(
        string pipeName,
        AppContainerSandbox appContainerSandbox)
    {
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            appContainerSandbox.GetContainerSid(),
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
        if (sandbox is not null)
        {
            try
            {
                return sandbox.StartProcess(workerExePath, new[] { pipeName });
            }
            catch
            {
                // If a platform container is present but not runnable in the current host
                // policy (for example bubblewrap disabled by kernel constraints), disable it
                // for subsequent attempts and fall back to the direct child-process path.
                sandbox.Dispose();
                sandbox = null;
            }
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

    /// <summary>
    /// Loads process-permission flags from environment variables.
    /// </summary>
    /// <remarks>
    /// Supported variables:
    /// <list type="bullet">
    ///   <item><c>UTILS_PARSER_WORKER_ALLOW_DISK_READ</c></item>
    ///   <item><c>UTILS_PARSER_WORKER_ALLOW_DISK_WRITE</c></item>
    ///   <item><c>UTILS_PARSER_WORKER_ALLOW_NETWORK</c></item>
    ///   <item><c>UTILS_PARSER_WORKER_ALLOW_DEVICE_ACCESS</c></item>
    ///   <item><c>UTILS_PARSER_WORKER_ALLOW_PROCESS_DEBUGGING</c></item>
    /// </list>
    /// Missing or invalid values keep defaults.
    /// </remarks>
    /// <returns>Permission settings consumed by <see cref="ProcessContainerFactory"/>.</returns>
    private static ProcessContainerPermissions LoadPermissionsFromEnvironment()
    {
        var permissions = new ProcessContainerPermissions();
        permissions.AllowDiskRead = ReadBool("UTILS_PARSER_WORKER_ALLOW_DISK_READ", permissions.AllowDiskRead);
        permissions.AllowDiskWrite = ReadBool("UTILS_PARSER_WORKER_ALLOW_DISK_WRITE", permissions.AllowDiskWrite);
        permissions.AllowNetwork = ReadBool("UTILS_PARSER_WORKER_ALLOW_NETWORK", permissions.AllowNetwork);
        permissions.AllowDeviceAccess = ReadBool("UTILS_PARSER_WORKER_ALLOW_DEVICE_ACCESS", permissions.AllowDeviceAccess);
        permissions.AllowProcessDebugging = ReadBool("UTILS_PARSER_WORKER_ALLOW_PROCESS_DEBUGGING", permissions.AllowProcessDebugging);
        return permissions;
    }

    /// <summary>
    /// Reads a boolean environment variable with fallback.
    /// </summary>
    /// <param name="name">Environment variable name.</param>
    /// <param name="defaultValue">Value used when parsing fails.</param>
    /// <returns>Parsed boolean value or <paramref name="defaultValue"/>.</returns>
    private static bool ReadBool(string name, bool defaultValue)
    {
        string? rawValue = Environment.GetEnvironmentVariable(name);
        return bool.TryParse(rawValue, out bool parsed) ? parsed : defaultValue;
    }
}
