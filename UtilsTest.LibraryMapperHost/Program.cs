using System.Runtime.InteropServices;

using Utils.Reflection;
using UtilsTest.NativeInterop;

namespace UtilsTest.LibraryMapperHost;

/// <summary>
/// Defines the native scalar operation exercised through the isolated mapper worker.
/// </summary>
public interface INativeMath : IDisposable
{
    /// <summary>
    /// Returns the absolute value of <paramref name="value"/>.
    /// </summary>
    [External("abs")]
    int Abs(int value);
}

/// <summary>
/// Provides a controlled executable entry point for isolated <see cref="LibraryMapper.Emit{TInterface}"/> tests.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the mapper worker when requested, or performs one end-to-end native call in normal mode.
    /// </summary>
    /// <param name="args">The unmodified process arguments.</param>
    /// <returns>Zero when worker dispatch or the end-to-end native call succeeds; otherwise a nonzero value.</returns>
    public static int Main(string[] args)
    {
        if (LibraryMapper.RunWorkerIfRequested(args))
            return 0;

        if (args.Length != 0)
        {
            Console.Error.WriteLine("No arguments are expected in normal host mode.");
            return 2;
        }

        string nativeLibrary = NativeRuntimeLibraryResolver.ResolveWithExport("abs");
        using INativeMath mapper = LibraryMapper.Emit<INativeMath>(nativeLibrary, CallingConvention.Cdecl);
        int result = mapper.Abs(-42);
        Console.WriteLine(result);
        return result == 42 ? 0 : 1;
    }
}
