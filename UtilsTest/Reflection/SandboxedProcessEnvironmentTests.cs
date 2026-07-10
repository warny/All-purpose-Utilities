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
}
