using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Utils.Reflection.Reflection.Emit;

namespace Utils.Reflection
{
    /// <summary>
    /// A class to dynamically map unmanaged DLL functions to .NET properties or fields.
    /// This class also handles platform differences (Windows vs. Unix-based systems).
    /// </summary>
    public abstract class LibraryMapper : IDisposable
    {
        private IntPtr dllHandle;
        private bool disposed;

        private static readonly Type ExternalAttributeType = typeof(ExternalAttribute);

        /// <summary>Indicates whether this mapper instance has been disposed.</summary>
        public bool IsDisposed => disposed;

        /// <summary>
        /// Diagnostic ID reported by APIs that compile and load caller-controlled interface metadata
        /// directly in the current process. See <see cref="EmitInProcess{TInterface}"/> and
        /// <see cref="Reflection.Emit.EmitDllMappableClass"/> for the associated risk.
        /// </summary>
        internal const string CodeGenerationExperimentalDiagnosticId = "UTILSREFL001";

        /// <summary>
        /// First command-line argument that makes <see cref="RunWorkerIfRequested"/> enter the
        /// isolated Emit worker loop instead of returning control to the caller's normal startup.
        /// </summary>
        internal const string WorkerArgumentMarker = "--utils-reflection-emit-worker";

        /// <summary>
        /// Creates an instance of a derived <see cref="LibraryMapper"/> class and maps the specified DLL
        /// functions to the instance's properties and fields.
        /// </summary>
        /// <typeparam name="T">A class derived from <see cref="LibraryMapper"/>.</typeparam>
        /// <param name="dllPath">The path to the DLL to load.</param>
        /// <returns>An instance of the derived class.</returns>
        public static T Create<T>(string dllPath) where T : LibraryMapper, new()
        {
            var obj = new T();
            MapLibraryToInstance(dllPath, obj);
            return obj;
        }

        /// <summary>
        /// Maps DLL functions to an interface, isolating the (untrusted-input-sensitive) code
        /// generation and native calls in a separate worker process.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the recommended, safe-by-default way to map an interface to a native DLL: the
        /// worker process is a copy of the current executable, re-launched with the strongest
        /// available OS sandbox (see <see cref="ProcessIsolation.ProcessContainerFactory"/>), so that
        /// even a maliciously crafted <typeparamref name="TInterface"/> (see
        /// <see cref="Reflection.Emit.EmitDllMappableClass"/>) can only do as much damage as the
        /// sandbox permissions allow, rather than running with the full trust of the calling process.
        /// </para>
        /// <para>
        /// Only interfaces whose members use types that can be represented as JSON (primitives,
        /// <see cref="string"/>, enums, arrays/structs made of these) can be mapped this way, since
        /// every call is forwarded to the worker and back; pointers and handles (<see cref="IntPtr"/>,
        /// unmanaged pointers) are never supported because they are meaningless outside the process
        /// that produced them. Use <see cref="EmitInProcess{TInterface}"/> for interfaces that need them.
        /// </para>
        /// <para>
        /// Requires the host application to call <see cref="RunWorkerIfRequested"/> at the very start
        /// of its entry point (before any other startup logic), so the re-launched copy of the
        /// process can recognize that it should run as a worker instead of starting normally.
        /// </para>
        /// </remarks>
        /// <typeparam name="TInterface">The interface that defines the functions to map.</typeparam>
        /// <param name="dllPath">The path to the DLL.</param>
        /// <param name="callingConvention">The calling convention of the functions.</param>
        /// <param name="loadTimeout">
        /// Maximum time to wait for the worker to load <paramref name="dllPath"/> and emit the mapping
        /// class. Defaults to 30 seconds when <see langword="null"/>.
        /// </param>
        /// <param name="callTimeout">
        /// Maximum time to wait for the worker's response to each native call forwarded through the
        /// returned proxy. Defaults to 30 seconds when <see langword="null"/>.
        /// </param>
        /// <returns>A proxy implementing <typeparamref name="TInterface"/> that forwards every call to the isolated worker.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="TInterface"/> uses a type that cannot cross a process boundary.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown by this method (initial load) or by the returned proxy's members (subsequent calls)
        /// when the worker does not respond within <paramref name="loadTimeout"/>/<paramref name="callTimeout"/>.
        /// <para>
        /// <b>Important:</b> a per-call timeout abandons only the timed-out request on the host side;
        /// the worker process is <em>not</em> killed and remains usable for subsequent calls. The native
        /// call inside the worker continues to execute on its own thread until it finishes, and the
        /// eventual response is silently discarded. Callers must treat the outcome of a timed-out call
        /// as indeterminate and should not assume any side effect was or was not applied.
        /// </para>
        /// </exception>
        public static TInterface Emit<TInterface>(
            string dllPath, CallingConvention callingConvention,
            TimeSpan? loadTimeout = null, TimeSpan? callTimeout = null)
            where TInterface : class, IDisposable
        {
            EmitWorkerProcess worker = EmitWorkerProcess.Start(typeof(TInterface), dllPath, callingConvention, loadTimeout, callTimeout, out int handle);
            try
            {
                TInterface proxy = DispatchProxy.Create<TInterface, EmitWorkerProxy>();
                ((EmitWorkerProxy)(object)proxy).AttachWorker(worker, handle, ownsWorker: true);
                return proxy;
            }
            catch
            {
                worker.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Like <see cref="Emit{TInterface}"/>, but spreads calls across <paramref name="workerCount"/>
        /// independent isolated worker processes instead of one, picking the next one in round-robin
        /// order for every call.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="Emit{TInterface}"/>'s single worker already accepts several calls in flight at
        /// once (see <see cref="EmitWorkerProcess.InvokeMethod"/>) — but every one of them still ends up
        /// executing inside the very same worker process, so the native library backing
        /// <typeparamref name="TInterface"/> must itself be safe to call concurrently for that to be
        /// safe. Round-robining across <paramref name="workerCount"/> separate processes instead avoids
        /// that requirement entirely: each process has its own independent load of the native DLL, so
        /// concurrent calls can never race inside the same one. The trade-off is cost: this starts
        /// <paramref name="workerCount"/> full sandboxed worker processes up front (each paying the same
        /// per-process startup cost as a single <see cref="Emit{TInterface}"/> call), not one.
        /// </para>
        /// <para>
        /// The set of workers is fixed for the lifetime of the returned proxy — there is no dynamic
        /// scaling, health-checking, or replacement of a worker that dies mid-flight; a dead worker's
        /// share of subsequent round-robin turns simply starts failing with whatever exception
        /// <see cref="EmitWorkerProcess.InvokeMethod"/> raises for it (typically an
        /// <see cref="InvalidOperationException"/> once its connection is detected as broken). Disposing
        /// the returned proxy disposes every worker in the set.
        /// </para>
        /// </remarks>
        /// <typeparam name="TInterface">The interface that defines the functions to map.</typeparam>
        /// <param name="dllPath">The path to the DLL.</param>
        /// <param name="callingConvention">The calling convention of the functions.</param>
        /// <param name="workerCount">Number of independent worker processes to round-robin across. Must be at least 1.</param>
        /// <param name="loadTimeout">Maximum time to wait for each worker's response to its load request. Defaults to 30 seconds when <see langword="null"/>.</param>
        /// <param name="callTimeout">Maximum time to wait for a worker's response to each forwarded call. Defaults to 30 seconds when <see langword="null"/>.</param>
        /// <returns>A proxy implementing <typeparamref name="TInterface"/> that forwards each call to the next worker in round-robin order.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="workerCount"/> is less than 1.</exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="TInterface"/> uses a type that cannot cross a process boundary.
        /// </exception>
        public static TInterface EmitRoundRobin<TInterface>(
            string dllPath, CallingConvention callingConvention, int workerCount,
            TimeSpan? loadTimeout = null, TimeSpan? callTimeout = null)
            where TInterface : class, IDisposable
        {
            if (workerCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(workerCount), workerCount, "At least one round-robin worker is required.");
            }

            var created = new List<(EmitWorkerProcess Worker, int Handle)>(workerCount);
            try
            {
                for (int i = 0; i < workerCount; i++)
                {
                    EmitWorkerProcess worker = EmitWorkerProcess.Start(typeof(TInterface), dllPath, callingConvention, loadTimeout, callTimeout, out int handle);
                    created.Add((worker, handle));
                }
            }
            catch
            {
                foreach ((EmitWorkerProcess worker, _) in created)
                {
                    worker.Dispose();
                }

                throw;
            }

            TInterface proxy = DispatchProxy.Create<TInterface, EmitWorkerRoundRobinProxy>();
            ((EmitWorkerRoundRobinProxy)(object)proxy).AttachWorkers([.. created]);
            return proxy;
        }

        /// <summary>
        /// Dynamically emits a class in the <em>current</em> process and maps DLL functions to its
        /// members based on an interface, without any process isolation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Security warning:</b> the code generator (<see cref="Reflection.Emit.EmitDllMappableClass"/>)
        /// builds C# source by concatenating type, method and parameter names obtained through
        /// reflection on <typeparamref name="TInterface"/>. CLR metadata names are far less constrained
        /// than C# lexical identifiers, so an interface sourced from an untrusted or dynamically
        /// generated assembly could inject arbitrary C# — including a static constructor that runs
        /// with the full trust of this process as soon as the emitted type is loaded. Only call this
        /// method with interfaces you fully trust (typically ones you compiled yourself). Prefer
        /// <see cref="Emit{TInterface}"/>, which runs the same code generation inside an isolated
        /// worker process, for anything else.
        /// </para>
        /// </remarks>
        /// <typeparam name="TInterface">The interface that defines the functions to map.</typeparam>
        /// <param name="dllPath">The path to the DLL.</param>
        /// <param name="callingConvention">The calling convention of the functions.</param>
        /// <returns>An instance of the emitted class implementing the interface <typeparamref name="TInterface"/>.</returns>
        [Experimental(CodeGenerationExperimentalDiagnosticId)]
        public static TInterface EmitInProcess<TInterface>(string dllPath, CallingConvention callingConvention)
            where TInterface : class, IDisposable
        {
            return (TInterface)EmitCore(typeof(TInterface), dllPath, callingConvention);
        }

        /// <summary>
        /// Shared implementation behind <see cref="EmitInProcess{TInterface}"/>, also used internally
        /// by the isolated worker loop (<see cref="Reflection.Emit.EmitWorkerHost"/>) once it is
        /// already running inside a sandboxed process.
        /// </summary>
        /// <param name="interfaceType">The interface that defines the functions to map.</param>
        /// <param name="dllPath">The path to the DLL.</param>
        /// <param name="callingConvention">The calling convention of the functions.</param>
        /// <returns>An instance of the emitted class implementing <paramref name="interfaceType"/>.</returns>
        internal static object EmitCore(Type interfaceType, string dllPath, CallingConvention callingConvention)
        {
#pragma warning disable UTILSREFL001 // Reviewed call site: either the caller opted into EmitInProcess's risk, or this runs inside an isolated worker.
            var obj = EmitDllMappableClass.Emit(interfaceType, callingConvention);
#pragma warning restore UTILSREFL001
            MapLibraryToInstance(dllPath, (LibraryMapper)obj);
            return obj;
        }

        /// <summary>
        /// Entry point for the isolated Emit worker. Host applications that use <see cref="Emit{TInterface}"/>
        /// must call this as the very first statement of their own entry point (before any other
        /// startup logic), passing the raw process arguments through unchanged.
        /// </summary>
        /// <remarks>
        /// When the process was re-launched by <see cref="Emit{TInterface}"/> to act as an isolated worker,
        /// this method never returns until the worker's named-pipe connection ends: it runs the
        /// request/response loop and then returns <see langword="true"/>, at which point the caller
        /// should exit immediately without running its normal startup logic. When the process is
        /// running normally (no worker marker in <paramref name="args"/>), this method returns
        /// <see langword="false"/> immediately and the caller should proceed as usual.
        /// </remarks>
        /// <param name="args">The current process's command-line arguments, unmodified.</param>
        /// <returns><see langword="true"/> when this process ran as an isolated Emit worker; otherwise <see langword="false"/>.</returns>
        public static bool RunWorkerIfRequested(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            if (args.Length < 2 || args[0] != WorkerArgumentMarker)
            {
                return false;
            }

            string pipeName = args[1];
            using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            pipeClient.Connect(10_000);

            using var reader = new System.IO.StreamReader(pipeClient);
            using var writer = new System.IO.StreamWriter(pipeClient) { AutoFlush = true };
            EmitWorkerHost.Run(reader, writer);

            return true;
        }

        /// <summary>
        /// Maps the functions in a DLL to the properties and fields of the provided <see cref="LibraryMapper"/> object.
        /// </summary>
        /// <remarks>
        /// Loading is transactional: all exports are resolved and delegates constructed in a
        /// preparation phase before any member assignment is committed. If any step fails, the DLL
        /// handle is freed immediately so no partial state or leaked native resource is left on the
        /// object. Only fields and auto-properties are accepted as <c>[External]</c> members; custom
        /// setter bodies are rejected during the preparation phase (see <see cref="IsAutoProperty"/>).
        /// </remarks>
        /// <param name="dllPath">The path to the DLL.</param>
        /// <param name="obj">The object whose members are to be mapped to DLL functions.</param>
        private static void MapLibraryToInstance(string dllPath, LibraryMapper obj)
        {
            try
            {
                obj.dllHandle = NativeLibrary.Load(dllPath);
            }
            catch (DllNotFoundException ex)
            {
                throw new DllNotFoundException($"Unable to load the DLL: {dllPath}", ex);
            }

            // Prepare phase: resolve every export and construct every delegate before committing
            // any assignment. On failure, free the DLL immediately.
            var pending = new List<(MemberInfo Member, Delegate Value)>();
            try
            {
                var members = obj.GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var member in members)
                {
                    var attr = (ExternalAttribute)member.GetCustomAttributes(ExternalAttributeType, true).FirstOrDefault();
                    if (attr == null)
                        continue;

                    string functionName = attr.Name ?? member.Name;
                    IntPtr functionPtr;
                    try
                    {
                        functionPtr = NativeLibrary.GetExport(obj.dllHandle, functionName);
                    }
                    catch (EntryPointNotFoundException)
                    {
                        throw new EntryPointNotFoundException(
                            $"Unable to locate the function '{functionName}' in the DLL '{dllPath}'.");
                    }

                    var memberType = (member as PropertyInfo)?.PropertyType ?? (member as FieldInfo)?.FieldType;
                    if (memberType == null)
                        continue; // Method members: only meaningful to EmitDllMappableClass's generated code, not here.

                    if (!typeof(Delegate).IsAssignableFrom(memberType))
                    {
                        throw new InvalidOperationException(
                            $"Member '{member.Name}' on '{obj.GetType().FullName}' is decorated with " +
                            $"[External] but its type '{memberType.FullName}' is not a delegate type. " +
                            $"[External] properties/fields must be typed as a delegate matching the " +
                            $"native function's signature.");
                    }

                    if (member is PropertyInfo prop)
                    {
                        if (!prop.CanWrite)
                            throw new InvalidOperationException(
                                $"Property '{member.Name}' on '{obj.GetType().FullName}' is decorated " +
                                $"with [External] but has no setter.");

                        if (!IsAutoProperty(prop))
                            throw new InvalidOperationException(
                                $"Property '{member.Name}' on '{obj.GetType().FullName}' is decorated " +
                                $"with [External] but has a custom setter body. Only auto-properties " +
                                $"(compiler-generated backing field) and fields are permitted, because " +
                                $"Dispose must be able to null them unconditionally. Use a field or an " +
                                $"auto-property, and enforce any non-nullable contract through a wrapper " +
                                $"method instead.");
                    }

                    Delegate delegateFunction;
                    try
                    {
                        delegateFunction = Marshal.GetDelegateForFunctionPointer(functionPtr, memberType);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new InvalidOperationException(
                            $"Unable to create a delegate of type '{memberType.FullName}' for the native " +
                            $"function '{functionName}' mapped to member '{member.Name}' on " +
                            $"'{obj.GetType().FullName}'.", ex);
                    }

                    pending.Add((member, delegateFunction));
                }
            }
            catch
            {
                // Roll back immediately — do not leave a DLL handle that only the finalizer will free.
                NativeLibrary.Free(obj.dllHandle);
                obj.dllHandle = IntPtr.Zero;
                throw;
            }

            // Commit phase: all members are fields or auto-properties (validated above), so
            // SetValue cannot throw — assignment is unconditionally safe.
            foreach ((MemberInfo member, Delegate value) in pending)
            {
                if (member is PropertyInfo propInfo)
                    propInfo.SetValue(obj, value);
                else if (member is FieldInfo fieldInfo)
                    fieldInfo.SetValue(obj, value);
            }
        }

        #region IDisposable Pattern

        /// <summary>
        /// Releases the resources used by the <see cref="LibraryMapper"/> class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            disposed = true;

            try
            {
                // Clear mapped delegates only during explicit Dispose, not from the finalizer.
                // All [External] members are guaranteed to be fields or auto-properties (validated
                // at Create time), so SetValue(null) is unconditionally safe.
                if (disposing)
                    ClearMappedDelegates();
            }
            finally
            {
                if (dllHandle != IntPtr.Zero)
                {
                    NativeLibrary.Free(dllHandle);
                    dllHandle = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Sets every <see cref="ExternalAttribute"/>-decorated member to <see langword="null"/>
        /// so post-Dispose reads return <see langword="null"/> instead of a stale function pointer
        /// into freed native memory.
        /// </summary>
        private void ClearMappedDelegates()
        {
            foreach (MemberInfo member in GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (member.GetCustomAttributes(ExternalAttributeType, inherit: true).Length == 0) continue;

                if (member is PropertyInfo prop && prop.CanWrite)
                    prop.SetValue(this, null);
                else if (member is FieldInfo field)
                    field.SetValue(this, null);
            }
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="prop"/> is a compiler-generated
        /// auto-property, identified by the presence of a <c>&lt;PropertyName&gt;k__BackingField</c>
        /// field on its declaring type.
        /// </summary>
        private static bool IsAutoProperty(PropertyInfo prop)
        {
            string backingFieldName = $"<{prop.Name}>k__BackingField";
            return (prop.DeclaringType ?? prop.ReflectedType)?
                .GetField(backingFieldName, BindingFlags.NonPublic | BindingFlags.Instance) is not null;
        }

        /// <inheritdoc/>
        ~LibraryMapper() => Dispose(false);

        #endregion
    }

    /// <summary>
    /// Attribute used to mark properties or fields to be mapped to external unmanaged functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method,
                    AllowMultiple = false,
                    Inherited = true)]
    public class ExternalAttribute : Attribute
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ExternalAttribute"/> class using the member's name.
        /// </summary>
        public ExternalAttribute()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ExternalAttribute"/> class with an explicit name override.
        /// </summary>
        /// <param name="name">The name of the unmanaged function to map.</param>
        public ExternalAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// The name of the external function to map. If not provided, the member's name will be used.
        /// </summary>
        public string Name { get; }
    }
}
