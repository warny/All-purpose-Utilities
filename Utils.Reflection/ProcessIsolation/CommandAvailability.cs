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
    /// <param name="commandName">File name of the executable to probe.</param>
    /// <returns><see langword="true"/> if the command can be located; otherwise <see langword="false"/>.</returns>
    public static bool Exists(string commandName)
    {
        if (Path.IsPathRooted(commandName))
        {
            return File.Exists(commandName);
        }

        string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] directories = pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (string directory in directories)
        {
            string candidate = Path.Combine(directory, commandName);
            if (File.Exists(candidate))
            {
                return true;
            }
        }

        return false;
    }
}
