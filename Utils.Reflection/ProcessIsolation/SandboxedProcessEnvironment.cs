using System;
using System.Collections;
using System.Diagnostics;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// Strips the environment variables passed to a sandboxed child process down to a small allowlist,
/// so a process meant to run untrusted code does not inherit secrets (tokens, connection strings,
/// internal paths) that happen to be set in the host process's environment.
/// </summary>
internal static class SandboxedProcessEnvironment
{
    /// <summary>
    /// Environment variable names copied through verbatim, beyond the <c>DOTNET_</c>/<c>CORECLR_</c>
    /// prefixes handled separately. These are needed for the child process (and, for a self-hosted
    /// Emit worker, the .NET runtime within it) to start up and behave correctly, not to access any
    /// secret.
    /// </summary>
    private static readonly string[] AllowedExactNames =
    [
        "PATH", "HOME", "TMPDIR", "LANG", "LC_ALL", "USER", "LOGNAME", "TERM",
    ];

    /// <summary>
    /// Replaces <paramref name="startInfo"/>'s inherited environment (a full copy of the current
    /// process's environment) with only the variables needed to start the process and locate the
    /// .NET runtime.
    /// </summary>
    /// <param name="startInfo">Start info for a sandboxed child process, before <see cref="Process.Start(ProcessStartInfo)"/>.</param>
    internal static void ApplyMinimalEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.EnvironmentVariables.Clear();

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var name = (string)entry.Key;
            if (IsAllowed(name))
            {
                startInfo.EnvironmentVariables[name] = (string?)entry.Value;
            }
        }
    }

    private static bool IsAllowed(string name) =>
        Array.IndexOf(AllowedExactNames, name) >= 0 ||
        name.StartsWith("DOTNET_", StringComparison.Ordinal) ||
        name.StartsWith("CORECLR_", StringComparison.Ordinal);
}
