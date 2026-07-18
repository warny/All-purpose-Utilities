using System;
using System.IO;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// Provides helpers to locate command-line executables in the current process environment.
/// </summary>
public static class CommandAvailability
{
    /// <summary>
    /// Returns <see langword="true"/> when an executable is present in <c>PATH</c>.
    /// </summary>
    /// <remarks>
    /// On Windows, when <paramref name="commandName"/> has no extension, each extension listed in
    /// <c>PATHEXT</c> is also tried (matching how <c>cmd.exe</c>/<c>CreateProcess</c> resolve a bare
    /// command name), so <c>Exists("ffmpeg")</c> finds <c>ffmpeg.exe</c> without the caller having to
    /// know the platform-specific executable extension.
    /// </remarks>
    /// <param name="commandName">File name of the executable to probe.</param>
    /// <returns><see langword="true"/> if the command can be located; otherwise <see langword="false"/>.</returns>
    public static bool Exists(string commandName) => TryResolve(commandName, out _);

    /// <summary>
    /// Locates an executable in <c>PATH</c> and returns its canonical absolute path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Callers should retain the resolved path and pass it directly to process launches rather than
    /// re-resolving the bare command name each time. Re-resolving introduces a TOCTOU window: the
    /// <c>PATH</c> value or the file at each candidate directory can change between the check and the
    /// launch, allowing a different (possibly hostile) binary to be executed.
    /// </para>
    /// <para>
    /// On Windows, when <paramref name="commandName"/> has no extension, each extension listed in
    /// <c>PATHEXT</c> is also tried; see <see cref="Exists"/> for details.
    /// </para>
    /// </remarks>
    /// <param name="commandName">File name or rooted path of the executable to locate.</param>
    /// <param name="absolutePath">
    /// When this method returns <see langword="true"/>, the canonical absolute path to the
    /// executable; otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if the executable was found; otherwise <see langword="false"/>.</returns>
    public static bool TryResolve(string commandName, out string? absolutePath)
    {
        if (Path.IsPathRooted(commandName))
        {
            if (TryResolveWithExtensions(commandName, out absolutePath))
                return true;

            absolutePath = null;
            return false;
        }

        string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryResolveWithExtensions(Path.Combine(directory, commandName), out absolutePath))
                return true;
        }

        absolutePath = null;
        return false;
    }

    /// <summary>
    /// Tries <paramref name="candidatePath"/> as-is (returning the canonical absolute path via
    /// <see cref="Path.GetFullPath"/> on success), then — on Windows when no extension is present —
    /// with each <c>PATHEXT</c> extension appended.
    /// </summary>
    private static bool TryResolveWithExtensions(string candidatePath, out string? absolutePath)
    {
        if (File.Exists(candidatePath))
        {
            absolutePath = Path.GetFullPath(candidatePath);
            return true;
        }

        if (!OperatingSystem.IsWindows() || Path.HasExtension(candidatePath))
        {
            absolutePath = null;
            return false;
        }

        string pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? string.Empty;
        foreach (string extension in pathExt.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = candidatePath + extension;
            if (File.Exists(candidate))
            {
                absolutePath = Path.GetFullPath(candidate);
                return true;
            }
        }

        absolutePath = null;
        return false;
    }
}
