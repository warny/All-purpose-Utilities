using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Parser.VisualStudio.Worker;
using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Parser;

/// <summary>
/// Validates worker process containment behavior and argument escaping helpers.
/// </summary>
[TestClass]
public class PluginWorkerProcessContainairisationTests
{
    /// <summary>
    /// Ensures the worker launcher degrades to a direct process when the container launch fails.
    /// </summary>
    [TestMethod]
    public void StartWorkerProcess_WhenContainerLaunchFails_FallsBackToDirectProcess()
    {
        string executablePath = Environment.ProcessPath ?? throw new AssertFailedException("Process path is unavailable.");

        ConstructorInfo constructor = typeof(PluginWorkerProcess).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(string), typeof(IProcessContainer) },
            modifiers: null) ?? throw new AssertFailedException("PluginWorkerProcess constructor not found.");

        object instance = constructor.Invoke(new object[] { executablePath, new ThrowingContainer() });

        MethodInfo startWorkerProcess = typeof(PluginWorkerProcess).GetMethod(
            "StartWorkerProcess",
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new AssertFailedException("StartWorkerProcess method not found.");

        Process process = (Process)(startWorkerProcess.Invoke(instance, new object[] { "--info" })
            ?? throw new AssertFailedException("StartWorkerProcess returned null."));

        process.WaitForExit(5000);
        process.Dispose();

        FieldInfo sandboxField = typeof(PluginWorkerProcess).GetField(
            "sandbox",
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new AssertFailedException("sandbox field not found.");

        object? sandboxValue = sandboxField.GetValue(instance);
        Assert.IsNull(sandboxValue);
    }

    /// <summary>
    /// Ensures command-line quoting preserves backslashes in Windows-style paths containing spaces.
    /// </summary>
    [TestMethod]
    public void QuoteArgument_PreservesPathBackslashesWithSpaces()
    {
        MethodInfo quoteArgument = typeof(AppContainerSandbox).GetMethod(
            "QuoteArgument",
            BindingFlags.Static | BindingFlags.NonPublic) ?? throw new AssertFailedException("QuoteArgument method not found.");

        const string argument = "C:\\Program Files\\MyTool\\worker.exe";
        string escaped = (string)(quoteArgument.Invoke(null, new object[] { argument })
            ?? throw new AssertFailedException("QuoteArgument returned null."));

        Assert.AreEqual("\"C:\\Program Files\\MyTool\\worker.exe\"", escaped);
    }

    /// <summary>
    /// Ensures worker permissions can be controlled through environment variables.
    /// </summary>
    [TestMethod]
    public void LoadPermissionsFromEnvironment_ReadsPermissionFlags()
    {
        const string diskWriteVar = "PROCESS_WORKER_ALLOW_DISK_WRITE";
        const string networkVar = "PROCESS_WORKER_ALLOW_NETWORK";
        const string deviceVar = "PROCESS_WORKER_ALLOW_DEVICE_ACCESS";

        try
        {
            Environment.SetEnvironmentVariable(diskWriteVar, "true");
            Environment.SetEnvironmentVariable(networkVar, "true");
            Environment.SetEnvironmentVariable(deviceVar, "false");

            MethodInfo loadPermissions = typeof(PluginWorkerProcess).GetMethod(
                "LoadPermissionsFromEnvironment",
                BindingFlags.Static | BindingFlags.NonPublic) ?? throw new AssertFailedException("LoadPermissionsFromEnvironment not found.");

            ProcessContainerPermissions permissions =
                (ProcessContainerPermissions)(loadPermissions.Invoke(null, null)
                ?? throw new AssertFailedException("LoadPermissionsFromEnvironment returned null."));

            Assert.IsTrue(permissions.AllowDiskWrite);
            Assert.IsTrue(permissions.AllowNetwork);
            Assert.IsFalse(permissions.AllowDeviceAccess);
        }
        finally
        {
            Environment.SetEnvironmentVariable(diskWriteVar, null);
            Environment.SetEnvironmentVariable(networkVar, null);
            Environment.SetEnvironmentVariable(deviceVar, null);
        }
    }

    /// <summary>
    /// Test double that always fails process start requests.
    /// </summary>
    private sealed class ThrowingContainer : IProcessContainer
    {
        /// <summary>
        /// Always throws to simulate container startup failure.
        /// </summary>
        /// <param name="executablePath">Executable path (unused).</param>
        /// <param name="arguments">Arguments (unused).</param>
        /// <returns>Never returns.</returns>
        public Process StartProcess(string executablePath, IEnumerable<string> arguments)
        {
            _ = executablePath;
            _ = arguments;
            throw new InvalidOperationException("Simulated container startup failure.");
        }

        /// <summary>
        /// No-op for the test double.
        /// </summary>
        /// <param name="directoryPath">Directory path (unused).</param>
        public void GrantDirectoryReadAccess(string directoryPath)
        {
            _ = directoryPath;
        }

        /// <summary>
        /// No-op for the test double.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Test double does not expose a security identifier.
        /// </summary>
        /// <param name="securityIdentifier">Always <see langword="null"/>.</param>
        /// <returns>Always <see langword="false"/>.</returns>
        public bool TryGetSecurityIdentifier(out SecurityIdentifier? securityIdentifier)
        {
            securityIdentifier = null;
            return false;
        }
    }
}
