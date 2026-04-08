using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.Versioning;

using Utils.Reflection.ProcessIsolation;

namespace Utils.Parser.VisualStudio.Worker;

/// <summary>
/// Filters plugin DLL paths to those that are either Authenticode-signed or explicitly
/// opted in for unsigned loading via a <c>.allow-unsigned</c> marker file.
/// </summary>
/// <remarks>
/// <para>
/// By default only DLLs with a valid Authenticode signature (PE signature + trusted
/// certificate chain) are forwarded to the worker process. This prevents a malicious or
/// accidentally-dropped DLL from being loaded without the user's knowledge.
/// </para>
/// <para>
/// When a user intentionally wants to load an unsigned plugin they must create an empty
/// marker file next to the DLL named <c>{plugin}.dll.allow-unsigned</c>. The marker is
/// intentional friction: the user must take an explicit action for each unsigned DLL.
/// </para>
/// <para>
/// Results are cached by <c>(dll last-write-time, marker last-write-time)</c> so the
/// Authenticode check (a file-open + crypto operation) is not repeated on every tag request.
/// The cache is invalidated automatically when either file changes.
/// </para>
/// </remarks>
internal static class PluginAssemblyVerifier
{
    /// <summary>
    /// Suffix appended to a DLL path to form the explicit opt-in marker file name.
    /// For example: <c>MyPlugin.dll.allow-unsigned</c>
    /// </summary>
    public const string AllowUnsignedSuffix = ".allow-unsigned";

    private record struct CacheEntry(DateTime DllModified, DateTime MarkerModified, bool Allowed);

    private static readonly ConcurrentDictionary<string, CacheEntry> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the subset of <paramref name="paths"/> that are permitted to be loaded.
    /// A DLL is permitted when it carries a valid Authenticode signature OR when a
    /// <c>{path}.allow-unsigned</c> marker file exists beside it.
    /// </summary>
    /// <remarks>
    /// On non-Windows platforms the filter is a no-op and all paths are returned as-is.
    /// </remarks>
    public static string[] Filter(string[] paths)
    {
        if (!OperatingSystem.IsWindows())
        {
            return paths;
        }

        var allowed = new System.Collections.Generic.List<string>(paths.Length);
        foreach (string path in paths)
        {
            if (IsAllowed(path))
            {
                allowed.Add(path);
            }
        }

        return allowed.ToArray();
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="path"/> is permitted to be
    /// loaded: either it carries a valid Authenticode signature or an explicit
    /// <c>.allow-unsigned</c> marker file exists beside it.
    /// Result is served from cache when the file timestamps have not changed.
    /// </summary>
    /// <param name="path">Full path to the plugin DLL.</param>
    /// <returns><see langword="true"/> if the DLL is allowed; otherwise <see langword="false"/>.</returns>
    [SupportedOSPlatform("windows")]
    private static bool IsAllowed(string path)
    {
        DateTime dllModified;
        try
        {
            dllModified = File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            // Cannot stat the file; skip it.
            return false;
        }

        string markerPath = path + AllowUnsignedSuffix;
        DateTime markerModified = GetLastWriteTimeOrMin(markerPath);

        if (Cache.TryGetValue(path, out CacheEntry cached) &&
            cached.DllModified == dllModified &&
            cached.MarkerModified == markerModified)
        {
            return cached.Allowed;
        }

        bool allowed = HasValidAuthenticode(path) || markerModified != DateTime.MinValue;
        Cache[path] = new CacheEntry(dllModified, markerModified, allowed);
        return allowed;
    }

    /// <summary>
    /// Returns the UTC last-write time of <paramref name="path"/>, or
    /// <see cref="DateTime.MinValue"/> when the file does not exist or cannot be read.
    /// </summary>
    /// <param name="path">Path of the file to query.</param>
    /// <returns>UTC last-write time, or <see cref="DateTime.MinValue"/>.</returns>
    private static DateTime GetLastWriteTimeOrMin(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="filePath"/> carries a valid
    /// Authenticode signature that chains to a trusted root.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool HasValidAuthenticode(string filePath)
    {
        return ProcessIsolationPlatformSecurity.HasValidAuthenticodeSignature(filePath);
    }
}
