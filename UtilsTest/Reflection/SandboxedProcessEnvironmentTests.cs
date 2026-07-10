using System;
using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates that <see cref="SandboxedProcessEnvironment.ApplyMinimalEnvironment"/> strips the
/// environment down to an allowlist, so a sandboxed worker does not inherit arbitrary host secrets.
/// </summary>
[TestClass]
public class SandboxedProcessEnvironmentTests
{
    private const string SecretVariableName = "UTILS_REFLECTION_TEST_SECRET";

    [TestMethod]
    public void ApplyMinimalEnvironment_RemovesArbitraryVariables()
    {
        Environment.SetEnvironmentVariable(SecretVariableName, "super-secret-token");
        try
        {
            var psi = new ProcessStartInfo("dummy");
            // Force EnvironmentVariables to materialize as a full copy of the current environment
            // before stripping it, matching what actually happens right before Process.Start.
            _ = psi.EnvironmentVariables[SecretVariableName];

            SandboxedProcessEnvironment.ApplyMinimalEnvironment(psi);

            Assert.IsFalse(psi.EnvironmentVariables.ContainsKey(SecretVariableName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretVariableName, null);
        }
    }

    [TestMethod]
    public void ApplyMinimalEnvironment_KeepsPath()
    {
        var psi = new ProcessStartInfo("dummy");

        SandboxedProcessEnvironment.ApplyMinimalEnvironment(psi);

        Assert.IsTrue(psi.EnvironmentVariables.ContainsKey("PATH"));
    }

    [TestMethod]
    public void ApplyMinimalEnvironment_KeepsDotNetPrefixedVariables()
    {
        const string dotnetVariableName = "DOTNET_TEST_VARIABLE";
        Environment.SetEnvironmentVariable(dotnetVariableName, "1");
        try
        {
            var psi = new ProcessStartInfo("dummy");

            SandboxedProcessEnvironment.ApplyMinimalEnvironment(psi);

            Assert.IsTrue(psi.EnvironmentVariables.ContainsKey(dotnetVariableName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(dotnetVariableName, null);
        }
    }

    [TestMethod]
    public void BuildWindowsEnvironmentBlock_ExcludesArbitraryVariables()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("CreateProcess environment blocks are Windows-only.");
            return;
        }

        Environment.SetEnvironmentVariable(SecretVariableName, "super-secret-token");
        try
        {
            string block = SandboxedProcessEnvironment.BuildWindowsEnvironmentBlock();

            StringAssert.DoesNotMatch(block, new System.Text.RegularExpressions.Regex(
                System.Text.RegularExpressions.Regex.Escape(SecretVariableName)));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretVariableName, null);
        }
    }

    [TestMethod]
    public void BuildWindowsEnvironmentBlock_ContainsPathAndIsDoubleNullTerminated()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("CreateProcess environment blocks are Windows-only.");
            return;
        }

        string block = SandboxedProcessEnvironment.BuildWindowsEnvironmentBlock();

        // PATH's actual OS-native casing varies by machine (commonly "Path" on a plain Windows
        // install, but case-preserving — could be "PATH" depending on how it was originally set), so
        // this must match case-insensitively rather than assuming the all-uppercase Unix spelling.
        Assert.IsTrue(block.Contains("PATH=", StringComparison.OrdinalIgnoreCase),
            "Expected a PATH entry (any casing) in the environment block.");
        Assert.IsTrue(block.EndsWith("\0\0", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildWindowsEnvironmentBlock_KeepsWindowsRequiredVariables()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("CreateProcess environment blocks are Windows-only.");
            return;
        }

        // SystemRoot/windir/TEMP/ComSpec are effectively required for a Windows process (and the .NET
        // runtime within it) to start up correctly; a sandboxed worker missing them can fail before it
        // even reaches the point of connecting back to the host's named pipe.
        string block = SandboxedProcessEnvironment.BuildWindowsEnvironmentBlock();

        foreach (string requiredName in new[] { "SystemRoot", "TEMP" })
        {
            if (Environment.GetEnvironmentVariable(requiredName) is not null)
            {
                Assert.IsTrue(block.Contains(requiredName + "=", StringComparison.OrdinalIgnoreCase),
                    $"Expected '{requiredName}' (present in the host environment) to survive into the block.");
            }
        }
    }

    [TestMethod]
    public void IsAllowed_MatchesAllowlistedNamesRegardlessOfCase()
    {
        // Windows environment variable names are case-insensitive; the allowlist match must not
        // silently drop a variable just because the OS happens to have cased it differently than the
        // hardcoded list (e.g. "Path" instead of "PATH"). Uses a name that does not already exist on a
        // typical machine (TMPDIR is a Unix convention Windows doesn't set) so this is deterministic
        // regardless of platform, rather than depending on how a pre-existing real variable happens to
        // be cased.
        const string lowerCaseName = "tmpdir";
        Environment.SetEnvironmentVariable(lowerCaseName, "/tmp/example");
        try
        {
            var psi = new ProcessStartInfo("dummy");
            SandboxedProcessEnvironment.ApplyMinimalEnvironment(psi);

            Assert.IsTrue(psi.EnvironmentVariables.ContainsKey(lowerCaseName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(lowerCaseName, null);
        }
    }

    [TestMethod]
    public void ApplyMinimalEnvironment_KeepsDotNetPrefixedVariables_CaseInsensitively()
    {
        const string lowerCasePrefixedName = "dotnet_test_lowercase_prefix";
        Environment.SetEnvironmentVariable(lowerCasePrefixedName, "1");
        try
        {
            var psi = new ProcessStartInfo("dummy");
            SandboxedProcessEnvironment.ApplyMinimalEnvironment(psi);

            Assert.IsTrue(psi.EnvironmentVariables.ContainsKey(lowerCasePrefixedName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(lowerCasePrefixedName, null);
        }
    }

    [TestMethod]
    public void BuildWindowsEnvironmentBlock_EntriesAreSortedByName()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("CreateProcess environment blocks are Windows-only.");
            return;
        }

        string block = SandboxedProcessEnvironment.BuildWindowsEnvironmentBlock();
        string[] entries = block[..^1].Split('\0', StringSplitOptions.RemoveEmptyEntries);
        string[] names = System.Array.ConvertAll(entries, entry => entry[..entry.IndexOf('=')]);

        string[] sortedNames = (string[])names.Clone();
        System.Array.Sort(sortedNames, StringComparer.OrdinalIgnoreCase);

        CollectionAssert.AreEqual(sortedNames, names);
    }
}
