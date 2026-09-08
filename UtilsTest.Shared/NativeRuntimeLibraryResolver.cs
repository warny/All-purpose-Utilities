using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace UtilsTest.NativeInterop;

/// <summary>
/// Resolves a platform C runtime that exports a requested native function.
/// </summary>
internal static class NativeRuntimeLibraryResolver
{
    /// <summary>
    /// Finds the first platform C runtime candidate that can be loaded and contains
    /// <paramref name="exportName"/>.
    /// </summary>
    /// <param name="exportName">The native export that the selected library must contain.</param>
    /// <returns>The loadable native-library name or path.</returns>
    /// <exception cref="ArgumentException"><paramref name="exportName"/> is empty or whitespace.</exception>
    /// <exception cref="PlatformNotSupportedException">No candidate list is known for the current operating system.</exception>
    /// <exception cref="DllNotFoundException">None of the platform candidates can be loaded with the requested export.</exception>
    internal static string ResolveWithExport(string exportName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);
        IReadOnlyList<string> candidates = GetCandidates();

        foreach (string candidate in candidates)
        {
            if (!NativeLibrary.TryLoad(candidate, out IntPtr handle))
                continue;

            try
            {
                if (NativeLibrary.TryGetExport(handle, exportName, out _))
                    return candidate;
            }
            finally
            {
                NativeLibrary.Free(handle);
            }
        }

        throw new DllNotFoundException(
            $"No platform C runtime exporting '{exportName}' was found. Tried: {string.Join(", ", candidates)}.");
    }

    /// <summary>
    /// Gets ordered C runtime candidates for the current operating system and architecture.
    /// </summary>
    private static IReadOnlyList<string> GetCandidates()
    {
        if (OperatingSystem.IsWindows())
            return ["ucrtbase.dll", "msvcrt.dll"];

        if (OperatingSystem.IsMacOS())
            return ["/usr/lib/libSystem.B.dylib", "libSystem.B.dylib"];

        if (OperatingSystem.IsLinux())
            return GetLinuxCandidates(RuntimeInformation.ProcessArchitecture);

        throw new PlatformNotSupportedException(
            $"No C runtime candidates are configured for {RuntimeInformation.OSDescription}.");
    }

    /// <summary>
    /// Gets ordered glibc, architecture-specific musl, and generic C runtime candidates for Linux.
    /// </summary>
    /// <param name="architecture">The Linux process architecture.</param>
    /// <returns>Candidate library names ordered from most specific to most general.</returns>
    internal static IReadOnlyList<string> GetLinuxCandidates(Architecture architecture)
    {
        var candidates = new List<string> { "libc.so.6" };
        switch (architecture)
        {
            case Architecture.X64:
                candidates.Add("libc.musl-x86_64.so.1");
                break;
            case Architecture.X86:
                candidates.Add("libc.musl-x86.so.1");
                candidates.Add("libc.musl-i386.so.1");
                break;
            case Architecture.Arm64:
                candidates.Add("libc.musl-aarch64.so.1");
                break;
            case Architecture.Arm:
                candidates.Add("libc.musl-armv7.so.1");
                candidates.Add("libc.musl-armhf.so.1");
                break;
        }

        candidates.Add("libc.so");
        return candidates;
    }
}
