using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;

using Utils.Reflection.ProcessIsolation;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Launches and communicates with an isolated Emit worker: a copy of the current process re-executed
/// with a marker argument that makes it enter <see cref="LibraryMapper.RunWorkerIfRequested"/> instead
/// of its normal <c>Main</c> logic. The worker performs the native DLL loading and the (untrusted)
/// mapping-class generation described in <see cref="EmitDllMappableClass"/>, contained by whatever
/// process-container sandbox is available on the current platform.
/// </summary>
internal sealed class EmitWorkerProcess : IDisposable
{
    private IProcessContainer? sandbox;
    private readonly Process workerProcess;
    private readonly NamedPipeServerStream pipe;
    private readonly System.IO.StreamReader reader;
    private readonly System.IO.StreamWriter writer;
    private readonly SemaphoreSlim callLock = new(1, 1);
    private int nextId;
    private bool disposed;

    private EmitWorkerProcess(
        IProcessContainer? sandbox,
        Process workerProcess,
        NamedPipeServerStream pipe,
        System.IO.StreamReader reader,
        System.IO.StreamWriter writer)
    {
        this.sandbox = sandbox;
        this.workerProcess = workerProcess;
        this.pipe = pipe;
        this.reader = reader;
        this.writer = writer;
    }

    /// <summary>
    /// Validates that <paramref name="interfaceType"/> can be marshaled across a process boundary,
    /// starts the isolated worker, and requests that it load <paramref name="dllPath"/> and emit the
    /// mapping class for the interface.
    /// </summary>
    /// <param name="interfaceType">Interface whose members will be mapped to native exports.</param>
    /// <param name="dllPath">Path to the native DLL to load inside the worker.</param>
    /// <param name="callingConvention">Calling convention used for the generated delegates.</param>
    /// <returns>A connected, loaded worker ready to receive <see cref="WorkerRequestKind.Call"/> requests.</returns>
    internal static EmitWorkerProcess Start(Type interfaceType, string dllPath, CallingConvention callingConvention)
    {
        CrossProcessMarshaling.EnsureInterfaceIsSupported(interfaceType);

        if (string.IsNullOrEmpty(interfaceType.Assembly.Location))
        {
            throw new NotSupportedException(
                $"The assembly declaring '{interfaceType.FullName}' has no on-disk location (for example, " +
                $"a single-file publish or an in-memory assembly), so it cannot be loaded by an isolated " +
                $"Emit worker. Use LibraryMapper.EmitInProcess<T> instead.");
        }

        string exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Unable to determine the current process executable path, required to launch an isolated Emit worker.");

        ProcessContainerPermissions permissions = CreateWorkerPermissions();
        IProcessContainer? sandbox = ProcessContainerFactory.TryCreate(
            windowsContainerName: "Utils.Reflection.EmitWorker.v1",
            windowsDisplayName: "Utils.Reflection Emit Worker",
            windowsDescription: "Isolated process that compiles and executes dynamically emitted native DLL bindings.",
            permissions: permissions);

        string pipeName = $"Utils.Reflection.EmitWorker.{Guid.NewGuid():N}";
        NamedPipeServerStream serverPipe = CreateServerPipe(pipeName, sandbox);

        Process process;
        try
        {
            process = StartWorkerProcess(exePath, pipeName, ref sandbox);
        }
        catch
        {
            serverPipe.Dispose();
            sandbox?.Dispose();
            throw;
        }

        try
        {
            using CancellationTokenSource connectTimeout = new(TimeSpan.FromSeconds(10));
            serverPipe.WaitForConnectionAsync(connectTimeout.Token).GetAwaiter().GetResult();

            if (OperatingSystem.IsWindows() && !ProcessIsolationPlatformSecurity.IsExpectedNamedPipeClient(serverPipe, process.Id))
            {
                throw new InvalidOperationException("Security violation: unexpected process connected to the isolated Emit worker pipe.");
            }
        }
        catch
        {
            serverPipe.Dispose();
            KillSilently(process);
            sandbox?.Dispose();
            throw;
        }

        var reader = new System.IO.StreamReader(serverPipe, leaveOpen: true);
        var writer = new System.IO.StreamWriter(serverPipe, leaveOpen: true) { AutoFlush = true };

        var worker = new EmitWorkerProcess(sandbox, process, serverPipe, reader, writer);
        try
        {
            worker.Load(interfaceType, dllPath, callingConvention);
        }
        catch
        {
            worker.Dispose();
            throw;
        }

        return worker;
    }

    private void Load(Type interfaceType, string dllPath, CallingConvention callingConvention)
    {
        var request = new WorkerRequest
        {
            Id = Interlocked.Increment(ref nextId),
            Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = interfaceType.Assembly.Location,
            InterfaceTypeFullName = interfaceType.FullName,
            DllPath = dllPath,
            CallingConvention = callingConvention,
        };

        WorkerResponse response = SendAndReceive(request);
        ThrowIfFailed(response);
    }

    /// <summary>
    /// Serializes <paramref name="args"/>, forwards the call to the worker, applies any by-ref/out
    /// results back into <paramref name="args"/>, and returns the deserialized return value.
    /// </summary>
    /// <param name="method">Interface method being invoked, resolved by the caller's <see cref="System.Reflection.DispatchProxy"/>.</param>
    /// <param name="args">Argument values, in parameter order; by-ref/out slots are updated in place.</param>
    /// <returns>The method's return value, or <see langword="null"/> for <see langword="void"/> methods.</returns>
    internal object? InvokeMethod(MethodInfo method, object?[] args)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        callLock.Wait();
        try
        {
            ParameterInfo[] parameters = method.GetParameters();
            string?[] argumentsJson = new string?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                Type effectiveType = parameterType.IsByRef ? parameterType.GetElementType()! : parameterType;
                argumentsJson[i] = JsonSerializer.Serialize(args[i], effectiveType, CrossProcessMarshaling.JsonOptions);
            }

            var request = new WorkerRequest
            {
                Id = Interlocked.Increment(ref nextId),
                Kind = WorkerRequestKind.Call,
                MethodMetadataToken = method.MetadataToken,
                ArgumentsJson = argumentsJson,
            };

            WorkerResponse response = SendAndReceive(request);
            ThrowIfFailed(response, method.Name);

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType.IsByRef)
                {
                    Type effectiveType = parameters[i].ParameterType.GetElementType()!;
                    string? valueJson = response.ByRefValuesJson is { } values && i < values.Length ? values[i] : null;
                    args[i] = valueJson is null ? null : JsonSerializer.Deserialize(valueJson, effectiveType, CrossProcessMarshaling.JsonOptions);
                }
            }

            if (method.ReturnType == typeof(void) || response.ReturnValueJson is null)
            {
                return null;
            }

            return JsonSerializer.Deserialize(response.ReturnValueJson, method.ReturnType, CrossProcessMarshaling.JsonOptions);
        }
        finally
        {
            callLock.Release();
        }
    }

    private static void ThrowIfFailed(WorkerResponse response, string? methodName = null)
    {
        if (!response.Success)
        {
            string context = methodName is null ? "the Load request" : $"'{methodName}'";
            throw new EmitWorkerInvocationException(
                $"The isolated Emit worker reported an error while handling {context}: {response.ErrorMessage}",
                response.ErrorTypeName);
        }
    }

    private WorkerResponse SendAndReceive(WorkerRequest request)
    {
        writer.WriteLine(JsonSerializer.Serialize(request));

        string? line = reader.ReadLine()
            ?? throw new InvalidOperationException("The isolated Emit worker closed the connection unexpectedly.");

        return JsonSerializer.Deserialize<WorkerResponse>(line)
            ?? throw new InvalidOperationException("The isolated Emit worker returned an invalid response.");
    }

    /// <summary>
    /// Creates the named pipe server. When a sandbox exposing a security identifier is available, a
    /// <see cref="PipeSecurity"/> ACL restricts connections to that identifier plus the current user.
    /// </summary>
    private static NamedPipeServerStream CreateServerPipe(string pipeName, IProcessContainer? sandbox)
    {
        if (!OperatingSystem.IsWindows() ||
            sandbox is null ||
            !sandbox.TryGetSecurityIdentifier(out SecurityIdentifier? securityIdentifier) ||
            securityIdentifier is null)
        {
            return new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        return CreateSecuredServerPipe(pipeName, securityIdentifier);
    }

    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreateSecuredServerPipe(string pipeName, SecurityIdentifier securityIdentifier)
    {
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            securityIdentifier, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
            inBufferSize: 0, outBufferSize: 0, pipeSecurity);
    }

    /// <summary>
    /// Starts the worker process inside the sandbox when available, falling back to a plain child
    /// process (and disabling the sandbox for the caller) if the container fails to launch it.
    /// </summary>
    private static Process StartWorkerProcess(string exePath, string pipeName, ref IProcessContainer? sandbox)
    {
        string[] arguments = BuildWorkerArguments(exePath, pipeName);

        if (sandbox is not null)
        {
            try
            {
                return sandbox.StartProcess(exePath, arguments);
            }
            catch
            {
                sandbox.Dispose();
                sandbox = null;
            }
        }

        var psi = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start the isolated Emit worker process.");
    }

    /// <summary>
    /// Builds the argument list passed to <paramref name="exePath"/> to make it enter
    /// <see cref="LibraryMapper.RunWorkerIfRequested"/>.
    /// </summary>
    /// <remarks>
    /// When the host process is running under a generic launcher — most commonly the <c>dotnet</c>
    /// muxer (<c>dotnet MyApp.dll</c>, or an <c>UseAppHost=false</c> deployment) —
    /// <see cref="Environment.ProcessPath"/> resolves to the launcher executable, not the managed
    /// entry assembly. Re-launching that path with just <c>[marker, pipeName]</c> makes the launcher
    /// try to parse the marker as its own first argument (e.g. <c>dotnet
    /// --utils-reflection-emit-worker</c>) instead of forwarding it to the managed <c>Main</c>, so the
    /// worker never starts. Detected by comparing the launcher's file name against
    /// <see cref="Assembly.GetEntryAssembly"/>'s location: when they differ, the managed assembly path
    /// is inserted before the marker (<c>dotnet MyApp.dll --utils-reflection-emit-worker &lt;pipe&gt;</c>),
    /// matching how <c>dotnet</c> itself is invoked to run a framework-dependent deployment.
    /// </remarks>
    /// <param name="exePath">Executable that will be relaunched (<see cref="Environment.ProcessPath"/>).</param>
    /// <param name="pipeName">Name of the named pipe the worker should connect back to.</param>
    /// <returns>The complete, ordered argument list for the relaunched process.</returns>
    internal static string[] BuildWorkerArguments(string exePath, string pipeName)
    {
        string? entryAssemblyLocation = Assembly.GetEntryAssembly()?.Location;

        if (!string.IsNullOrEmpty(entryAssemblyLocation) &&
            !string.Equals(
                System.IO.Path.GetFileNameWithoutExtension(exePath),
                System.IO.Path.GetFileNameWithoutExtension(entryAssemblyLocation),
                StringComparison.OrdinalIgnoreCase))
        {
            return [entryAssemblyLocation, LibraryMapper.WorkerArgumentMarker, pipeName];
        }

        return [LibraryMapper.WorkerArgumentMarker, pipeName];
    }

    /// <summary>
    /// Builds the permission set requested for the isolated Emit worker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ProcessContainerPermissions.AllowDiskWrite"/> is granted on Linux/macOS only, even
    /// though the worker itself has no legitimate need to write files: on those platforms the
    /// host/worker named pipe is backed by a Unix domain socket file under the OS temp directory
    /// (<see cref="System.IO.Pipes.NamedPipeServerStream"/>'s Unix implementation). Without this flag,
    /// <see cref="ProcessIsolation.LinuxBubblewrapContainer"/> mounts a fresh, empty <c>tmpfs</c> over
    /// <c>/tmp</c> and <see cref="ProcessIsolation.MacOsSandboxExecContainer"/> denies
    /// <c>file-write*</c>, so the sandboxed worker can never see or connect to the socket and
    /// <see cref="Start"/> always fails with a connection timeout. Broader than a single-socket bind
    /// would be, but scoping the sandbox to that one path would require extending
    /// <see cref="IProcessContainer"/> beyond what can be validated without a real Linux/macOS
    /// environment — see the equivalent trade-off already documented for
    /// <see cref="ProcessIsolation.LinuxBubblewrapContainer"/>'s and
    /// <see cref="ProcessIsolation.MacOsSandboxExecContainer"/>'s read posture.
    /// </para>
    /// <para>
    /// <b>Must stay <see langword="false"/> on Windows.</b> Windows named pipes are kernel objects
    /// outside the filesystem, so the flag brings no benefit there — and
    /// <see cref="ProcessIsolation.ProcessContainerFactory.TryCreate"/> treats
    /// <see cref="ProcessContainerPermissions.AllowDiskWrite"/> as a request for broader-than-restrictive
    /// permissions and skips AppContainer creation entirely when it is set, which would silently
    /// disable sandboxing for the worker instead of merely being a no-op.
    /// </para>
    /// </remarks>
    /// <returns>The permission set to request for the isolated Emit worker.</returns>
    internal static ProcessContainerPermissions CreateWorkerPermissions() =>
        new() { AllowDiskRead = true, AllowDiskWrite = !OperatingSystem.IsWindows() };

    private static void KillSilently(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // Best-effort; the Job Object's KillOnJobClose (Windows) covers the rest.
        }
        finally
        {
            process.Dispose();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            if (!workerProcess.HasExited)
            {
                var shutdown = new WorkerRequest { Id = Interlocked.Increment(ref nextId), Kind = WorkerRequestKind.Shutdown };
                try
                {
                    SendAndReceive(shutdown);
                }
                catch
                {
                    // Best-effort graceful shutdown; the process is killed below regardless.
                }
            }
        }
        catch
        {
            // Ignore — workerProcess.HasExited itself can throw if the process handle is unusable.
        }

        reader.Dispose();
        writer.Dispose();
        pipe.Dispose();
        KillSilently(workerProcess);

        sandbox?.Dispose();
        callLock.Dispose();
    }
}
