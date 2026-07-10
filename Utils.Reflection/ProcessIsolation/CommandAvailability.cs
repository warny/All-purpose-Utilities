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
    public static bool Exists(string commandName)
    {
        if (Path.IsPathRooted(commandName))
        {
            return ExistsWithOptionalExtensions(commandName);
        }

        string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] directories = pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (string directory in directories)
        {
            string candidate = Path.Combine(directory, commandName);
            if (ExistsWithOptionalExtensions(candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks <paramref name="candidatePath"/> as-is, then (on Windows, when it has no extension
    /// already) with each <c>PATHEXT</c> extension appended.
    /// </summary>
    /// <param name="candidatePath">Full path to probe, with or without an extension.</param>
    /// <returns><see langword="true"/> when a matching file exists.</returns>
    private static bool ExistsWithOptionalExtensions(string candidatePath)
    {
        if (File.Exists(candidatePath))
        {
            return true;
        }

        if (!OperatingSystem.IsWindows() || Path.HasExtension(candidatePath))
        {
            return false;
        }

        string pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? string.Empty;
        foreach (string extension in pathExt.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (File.Exists(candidatePath + extension))
            {
                return true;
            }
        }

        return false;
    }
}
