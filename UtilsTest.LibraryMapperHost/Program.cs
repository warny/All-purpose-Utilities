using System.Runtime.InteropServices;

using Utils.Reflection;

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
    /// <param name="args">The unmodified process arguments, with the native library path in normal mode.</param>
    /// <returns>Zero when worker dispatch or the end-to-end native call succeeds; otherwise a nonzero value.</returns>
    public static int Main(string[] args)
    {
        if (LibraryMapper.RunWorkerIfRequested(args))
            return 0;

        if (args.Length != 1)
        {
            Console.Error.WriteLine("Expected one native library path argument.");
            return 2;
        }

        using INativeMath mapper = LibraryMapper.Emit<INativeMath>(args[0], CallingConvention.Cdecl);
        int result = mapper.Abs(-42);
        Console.WriteLine(result);
        return result == 42 ? 0 : 1;
    }
}
