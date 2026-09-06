using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Reflection;

/// <summary>
/// Verifies that command resolution retains canonical absolute executable paths.
/// </summary>
[TestClass]
public class CommandAvailabilitySecurityTests
{
    [TestMethod]
    public void TryResolve_ExistingAbsolutePath_ReturnsTrueWithCanonicalPath()
    {
        // Use the current test process executable — guaranteed to exist and be absolute.
        string? processPath = Environment.ProcessPath;
        if (processPath is null)
        {
            Assert.Inconclusive("Environment.ProcessPath is not available in this environment.");
            return;
        }

        bool found = CommandAvailability.TryResolve(processPath, out string? resolved);

        Assert.IsTrue(found, "TryResolve must return true for an existing absolute path.");
        Assert.IsNotNull(resolved);
        Assert.IsTrue(Path.IsPathRooted(resolved), "Resolved path must be rooted (absolute).");
    }

    [TestMethod]
    public void TryResolve_ExistingAbsolutePath_ResultIsFullyQualified()
    {
        string? processPath = Environment.ProcessPath;
        if (processPath is null)
        {
            Assert.Inconclusive("Environment.ProcessPath is not available in this environment.");
            return;
        }

        CommandAvailability.TryResolve(processPath, out string? resolved);

        // Path.GetFullPath normalises . and .. segments; the result must match the input when the
        // input is already a canonical absolute path (no relative segments).
        Assert.AreEqual(Path.GetFullPath(processPath), resolved);
    }

    [TestMethod]
    [DataRow("dotnet")] // available in any .NET SDK / runtime test environment
    public void TryResolve_KnownRuntimeCommand_ReturnsAbsolutePath(string command)
    {
        bool found = CommandAvailability.TryResolve(command, out string? path);

        if (!found)
        {
            Assert.Inconclusive($"'{command}' not found in PATH; test requires a .NET runtime environment.");
            return;
        }

        Assert.IsNotNull(path);
        Assert.IsTrue(Path.IsPathRooted(path), $"Resolved path for '{command}' must be absolute.");
        Assert.IsTrue(File.Exists(path), $"Resolved path '{path}' must point to an existing file.");
    }
}
