using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates <see cref="CommandAvailability.Exists"/>, including the Windows <c>PATHEXT</c>
/// resolution added for bare command names without an extension.
/// </summary>
[TestClass]
public class CommandAvailabilityTests
{
    [TestMethod]
    public void Exists_RootedPathWithExtension_MatchesRealFile()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"utils-reflection-{Guid.NewGuid():N}.exe");
        File.WriteAllBytes(tempFile, []);
        try
        {
            Assert.IsTrue(CommandAvailability.Exists(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void Exists_RootedPathWithoutExtension_ReturnsFalse_WhenFileDoesNotExist()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"utils-reflection-missing-{Guid.NewGuid():N}");
        Assert.IsFalse(CommandAvailability.Exists(missing));
    }

    [TestMethod]
    public void Exists_RootedPathWithoutExtension_ResolvesViaPathExtOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("PATHEXT resolution is Windows-specific.");
            return;
        }

        string baseNameWithoutExtension = Path.Combine(Path.GetTempPath(), $"utils-reflection-{Guid.NewGuid():N}");
        string actualFile = baseNameWithoutExtension + ".exe";
        File.WriteAllBytes(actualFile, []);
        try
        {
            Assert.IsTrue(CommandAvailability.Exists(baseNameWithoutExtension));
        }
        finally
        {
            File.Delete(actualFile);
        }
    }

    [TestMethod]
    public void Exists_RootedPathWithExplicitExtension_DoesNotTryPathExtAgain()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("PATHEXT resolution is Windows-specific.");
            return;
        }

        // The literal ".exe" file does not exist, and CommandAvailability must not try appending
        // further PATHEXT extensions on top of an already-present extension (e.g. ".exe.exe").
        string alreadyHasExtension = Path.Combine(Path.GetTempPath(), $"utils-reflection-missing-{Guid.NewGuid():N}.exe");
        Assert.IsFalse(CommandAvailability.Exists(alreadyHasExtension));
    }

    [TestMethod]
    public void Exists_BareCommandName_ResolvesThroughPathAndPathExt()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("PATHEXT resolution is Windows-specific.");
            return;
        }

        string tempDirectory = Path.Combine(Path.GetTempPath(), $"utils-reflection-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        string commandName = $"utils-reflection-tool-{Guid.NewGuid():N}";
        File.WriteAllBytes(Path.Combine(tempDirectory, commandName + ".exe"), []);

        string? originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", tempDirectory + Path.PathSeparator + originalPath);

            Assert.IsTrue(CommandAvailability.Exists(commandName));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

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
