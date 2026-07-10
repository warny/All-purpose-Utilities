using System;
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
        private IntPtr dllHandle; // Handle to the loaded DLL

        private static readonly Type ExternalAttributeType = typeof(ExternalAttribute);

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
        /// class before giving up and killing it. Defaults to 30 seconds when <see langword="null"/>.
        /// </param>
        /// <param name="callTimeout">
        /// Maximum time to wait for the worker's response to each native call forwarded through the
        /// returned proxy before giving up and killing it. Defaults to 30 seconds when
        /// <see langword="null"/>. A hung native call inside the worker would otherwise block the
        /// calling thread indefinitely, with no way to recover.
        /// </param>
        /// <returns>A proxy implementing <typeparamref name="TInterface"/> that forwards every call to the isolated worker.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="TInterface"/> uses a type that cannot cross a process boundary.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown by this method (initial load) or by the returned proxy's members (subsequent calls)
        /// when the worker does not respond within <paramref name="loadTimeout"/>/<paramref name="callTimeout"/>.
        /// The worker is killed and the proxy becomes unusable when this happens.
        /// </exception>
        public static TInterface Emit<TInterface>(
            string dllPath, CallingConvention callingConvention,
            TimeSpan? loadTimeout = null, TimeSpan? callTimeout = null)
            where TInterface : class, IDisposable
        {
            EmitWorkerProcess worker = EmitWorkerProcess.Start(typeof(TInterface), dllPath, callingConvention, loadTimeout, callTimeout);
            try
            {
                TInterface proxy = DispatchProxy.Create<TInterface, EmitWorkerProxy>();
                ((EmitWorkerProxy)(object)proxy).AttachWorker(worker);
                return proxy;
            }
            catch
            {
                worker.Dispose();
                throw;
            }
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

            // Get all instance members that might have the External attribute
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
                        $"Unable to locate the function '{functionName}' in the DLL '{dllPath}'."
                    );
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

                if (member is PropertyInfo propInfo)
                {
                    if (!propInfo.CanWrite)
                    {
                        throw new InvalidOperationException(
                            $"Property '{member.Name}' on '{obj.GetType().FullName}' is decorated " +
                            $"with [External] but has no setter.");
                    }

                    propInfo.SetValue(obj, delegateFunction);
                }
                else if (member is FieldInfo fieldInfo)
                {
                    fieldInfo.SetValue(obj, delegateFunction);
                }
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
            if (dllHandle != IntPtr.Zero)
            {
                NativeLibrary.Free(dllHandle);
                dllHandle = IntPtr.Zero;
            }
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
