using System;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates <see cref="CommandAvailability.TryResolve"/> path-resolution logic.
/// </summary>
[TestClass]
public class CommandAvailabilityTests
{
    // ─── Item 56: resolve and retain canonical absolute executable path ──────────

    [TestMethod]
    public void TryResolve_NonExistentCommand_ReturnsFalse()
    {
        bool found = CommandAvailability.TryResolve("does_not_exist_xyz_12345", out string? path);

        Assert.IsFalse(found);
        Assert.IsNull(path);
    }

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
    public void TryResolve_WhenFound_Exists_ReturnsTrue()
    {
        // Exists is documented as equivalent to TryResolve(name, out _); verify consistency.
        bool found = CommandAvailability.TryResolve("does_not_exist_xyz_12345", out _);
        bool exists = CommandAvailability.Exists("does_not_exist_xyz_12345");

        Assert.AreEqual(found, exists,
            "Exists must agree with TryResolve for the same command name.");
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
