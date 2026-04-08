using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// Defines a reusable process container capable of launching child processes
/// with additional isolation constraints.
/// </summary>
public interface IProcessContainer : IDisposable
{
    /// <summary>
    /// Starts a process inside the current container implementation.
    /// </summary>
    /// <param name="executablePath">Absolute path of the executable to run.</param>
    /// <param name="arguments">Ordered list of command-line arguments.</param>
    /// <returns>The started process instance.</returns>
    Process StartProcess(string executablePath, IEnumerable<string> arguments);

    /// <summary>
    /// Grants read access to a directory when the underlying container supports ACL tuning.
    /// On unsupported platforms this method is a no-op.
    /// </summary>
    /// <param name="directoryPath">Directory that should become readable for the child process.</param>
    void GrantDirectoryReadAccess(string directoryPath);

    /// <summary>
    /// Returns the security identifier used for IPC ACL hardening when available.
    /// </summary>
    /// <param name="securityIdentifier">Resolved security identifier.</param>
    /// <returns><see langword="true"/> when a security identifier is available.</returns>
    bool TryGetSecurityIdentifier(out SecurityIdentifier? securityIdentifier);
}
