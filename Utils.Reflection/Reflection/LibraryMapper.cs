using System;
using System.ComponentModel;
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
        /// Dynamically emits a class and maps DLL functions to its members based on an interface.
        /// </summary>
        /// <typeparam name="I">The interface that defines the functions to map.</typeparam>
        /// <param name="dllPath">The path to the DLL.</param>
        /// <param name="callingConvention">The calling convention of the functions.</param>
        /// <returns>An instance of the emitted class implementing the interface <typeparamref name="I"/>.</returns>
        public static I Emit<I>(string dllPath, CallingConvention callingConvention)
            where I : class, IDisposable
        {
            var obj = EmitDllMappableClass.Emit<I>(callingConvention);
            MapLibraryToInstance(dllPath, (LibraryMapper)(object)obj);
            return obj;
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
                    continue; // Or throw an exception, if you expect only property/field

                var delegateFunction = Marshal.GetDelegateForFunctionPointer(functionPtr, memberType);

                if (member is PropertyInfo propInfo)
                {
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
