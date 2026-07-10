using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

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
    /// secret. Mixes Unix names (<c>PATH</c>, <c>HOME</c>, <c>USER</c>, ...) and their Windows
    /// counterparts/Windows-only requirements (<c>TEMP</c>, <c>USERNAME</c>, <c>SystemRoot</c>, ...):
    /// matching is case-insensitive (see <see cref="IsAllowed"/>), and a name irrelevant to the current
    /// platform simply never appears in <see cref="Environment.GetEnvironmentVariables"/> there, so
    /// listing both costs nothing per platform.
    /// </summary>
    /// <remarks>
    /// <c>SystemRoot</c>/<c>windir</c>/<c>TEMP</c>/<c>ComSpec</c> are effectively required for a Windows
    /// process (and the .NET runtime within it) to start up correctly — without them, a sandboxed Emit
    /// worker can fail before it even reaches the point of connecting back to the host's named pipe.
    /// Environment variable names are case-insensitive on Windows, and
    /// <see cref="Environment.GetEnvironmentVariables"/> returns them in their OS-native casing (for
    /// example <c>"Path"</c>, not <c>"PATH"</c>) — comparing case-sensitively against an all-uppercase,
    /// Unix-styled list silently dropped every one of these on Windows.
    /// </remarks>
    private static readonly HashSet<string> AllowedExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PATH", "HOME", "TMPDIR", "LANG", "LC_ALL", "USER", "LOGNAME", "TERM",
        "TEMP", "TMP", "USERNAME", "SystemRoot", "windir", "ComSpec",
    };

    /// <summary>
    /// Replaces <paramref name="startInfo"/>'s inherited environment (a full copy of the current
    /// process's environment) with only the variables needed to start the process and locate the
    /// .NET runtime.
    /// </summary>
    /// <param name="startInfo">Start info for a sandboxed child process, before <see cref="Process.Start(ProcessStartInfo)"/>.</param>
    internal static void ApplyMinimalEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.EnvironmentVariables.Clear();

        foreach ((string name, string value) in GetAllowedVariables())
        {
            startInfo.EnvironmentVariables[name] = value;
        }
    }

    /// <summary>
    /// Builds a native Windows environment block for <c>CreateProcess</c>'s <c>lpEnvironment</c>
    /// parameter, containing only the same allowlisted variables as
    /// <see cref="ApplyMinimalEnvironment"/>.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="AppContainerSandbox"/>, which calls <c>CreateProcess</c> directly instead of
    /// going through <see cref="ProcessStartInfo"/>: without an explicit environment block,
    /// <c>CreateProcess</c> makes the child inherit the calling process's <em>entire</em> environment
    /// (passing <see langword="null"/>/<see cref="IntPtr.Zero"/> for <c>lpEnvironment</c>), which would
    /// silently defeat the point of stripping it down for
    /// <see cref="LinuxBubblewrapContainer"/>/<see cref="MacOsSandboxExecContainer"/> above. The
    /// returned block is a sequence of <c>"NAME=VALUE\0"</c> entries terminated by an extra
    /// <c>'\0'</c>, sorted by name (ordinal, case-insensitive) as recommended for
    /// <c>CREATE_UNICODE_ENVIRONMENT</c> blocks.
    /// </remarks>
    /// <returns>The environment block string, ready to be marshaled with <see cref="System.Runtime.InteropServices.Marshal.StringToHGlobalUni(string?)"/>.</returns>
    [SupportedOSPlatform("windows")]
    internal static string BuildWindowsEnvironmentBlock()
    {
        var sortedVariables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in GetAllowedVariables())
        {
            sortedVariables[name] = value;
        }

        var block = new StringBuilder();
        foreach (KeyValuePair<string, string> variable in sortedVariables)
        {
            block.Append(variable.Key).Append('=').Append(variable.Value).Append('\0');
        }

        block.Append('\0');
        return block.ToString();
    }

    private static IEnumerable<(string Name, string Value)> GetAllowedVariables()
    {
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var name = (string)entry.Key;
            if (IsAllowed(name) && entry.Value is string value)
            {
                yield return (name, value);
            }
        }
    }

    private static bool IsAllowed(string name) =>
        AllowedExactNames.Contains(name) ||
        name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("CORECLR_", StringComparison.OrdinalIgnoreCase);
}
