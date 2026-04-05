using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// Linux process container backed by <c>bwrap</c> (bubblewrap) when available.
/// </summary>
public sealed class LinuxBubblewrapContainer : IProcessContainer
{
    private const string BubblewrapExecutableName = "bwrap";

    private LinuxBubblewrapContainer()
    {
    }

    /// <summary>
    /// Creates a Linux process container when <c>bwrap</c> is available in the current environment.
    /// </summary>
    /// <returns>An initialized container, or <see langword="null"/> when unavailable.</returns>
    public static LinuxBubblewrapContainer? TryCreate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        return CommandAvailability.Exists(BubblewrapExecutableName)
            ? new LinuxBubblewrapContainer()
            : null;
    }

    /// <summary>
    /// Starts a child process inside an isolated bubblewrap namespace.
    /// </summary>
    /// <param name="executablePath">Absolute path to the executable to run.</param>
    /// <param name="arguments">Ordered command-line arguments for the executable.</param>
    /// <returns>The started process.</returns>
    public Process StartProcess(string executablePath, IEnumerable<string> arguments)
    {
        var wrappedArguments = new List<string>
        {
            "--die-with-parent",
            "--new-session",
            "--unshare-all",
            "--share-net",
            "--ro-bind",
            "/",
            "/",
            "--proc",
            "/proc",
            "--dev",
            "/dev",
            "--",
            executablePath,
        };

        wrappedArguments.AddRange(arguments);

        var psi = new ProcessStartInfo(BubblewrapExecutableName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in wrappedArguments)
        {
            psi.ArgumentList.Add(argument);
        }

        return Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start sandboxed process with bubblewrap.");
    }

    /// <summary>
    /// Grants read access to a directory.
    /// </summary>
    /// <param name="directoryPath">Directory that should remain readable.</param>
    public void GrantDirectoryReadAccess(string directoryPath)
    {
        _ = directoryPath;
    }

    /// <summary>
    /// Disposes the container instance.
    /// </summary>
    public void Dispose()
    {
    }
}
