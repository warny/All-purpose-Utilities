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
}
