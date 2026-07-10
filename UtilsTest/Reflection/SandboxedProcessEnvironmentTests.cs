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

        StringAssert.Contains(block, "PATH=");
        Assert.IsTrue(block.EndsWith("\0\0", StringComparison.Ordinal));
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
