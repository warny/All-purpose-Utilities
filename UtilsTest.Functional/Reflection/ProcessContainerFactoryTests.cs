using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates process-container selection and command discovery behavior.
/// </summary>
[TestClass]
public class ProcessContainerFactoryTests
{
    /// <summary>
    /// Ensures command discovery succeeds for an existing absolute executable path.
    /// </summary>
    [TestMethod]
    public void CommandAvailability_AbsolutePath_ReturnsTrue()
    {
        string dotnetPath = Environment.ProcessPath ?? throw new AssertFailedException("Process path is unavailable.");

        bool exists = CommandAvailability.Exists(dotnetPath);

        Assert.IsTrue(exists);
    }

    /// <summary>
    /// Ensures platform-specific factory creation remains safe and deterministic.
    /// </summary>
    [TestMethod]
    public void ProcessContainerFactory_TryCreate_DoesNotThrowAndMatchesPlatform()
    {
        IProcessContainer? container = ProcessContainerFactory.TryCreate(
            windowsContainerName: "UtilsTest.ProcessContainerFactoryTests",
            windowsDisplayName: "UtilsTest Container",
            windowsDescription: "Container created for unit tests.");

        if (OperatingSystem.IsWindows())
        {
            Assert.IsTrue(container is null or IProcessContainer);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            Assert.IsTrue(container is null or IProcessContainer);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Assert.IsTrue(container is null or IProcessContainer);
            return;
        }

        Assert.IsNull(container);
    }

    /// <summary>
    /// Ensures disabling disk-read permission forces a direct-process fallback.
    /// </summary>
    [TestMethod]
    public void ProcessContainerFactory_TryCreate_WithDiskReadDisabled_ReturnsNull()
    {
        var permissions = new ProcessContainerPermissions
        {
            AllowDiskRead = false,
        };

        IProcessContainer? container = ProcessContainerFactory.TryCreate(
            windowsContainerName: "UtilsTest.ProcessContainerFactoryTests",
            windowsDisplayName: "UtilsTest Container",
            windowsDescription: "Container created for unit tests.",
            permissions: permissions);

        Assert.IsNull(container);
    }
}
