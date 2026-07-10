using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates that <see cref="ProcessContainerPermissions"/> cannot be mutated after construction,
/// so a caller can never weaken the shared <see cref="ProcessContainerPermissions.Default"/> baseline
/// for every other consumer in the process.
/// </summary>
[TestClass]
public class ProcessContainerPermissionsTests
{
    [TestMethod]
    public void Default_ReturnsIndependentInstanceEachTime()
    {
        ProcessContainerPermissions first = ProcessContainerPermissions.Default;
        ProcessContainerPermissions second = ProcessContainerPermissions.Default;

        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public void Default_HasTheMostRestrictiveBaseline()
    {
        ProcessContainerPermissions permissions = ProcessContainerPermissions.Default;

        Assert.IsTrue(permissions.AllowDiskRead);
        Assert.IsFalse(permissions.AllowDiskWrite);
        Assert.IsFalse(permissions.AllowNetwork);
        Assert.IsFalse(permissions.AllowDeviceAccess);
        Assert.IsFalse(permissions.AllowProcessDebugging);
    }

    [TestMethod]
    public void ObjectInitializer_CanCustomizePermissions()
    {
        var permissions = new ProcessContainerPermissions
        {
            AllowDiskWrite = true,
            AllowNetwork = true,
        };

        Assert.IsTrue(permissions.AllowDiskRead);
        Assert.IsTrue(permissions.AllowDiskWrite);
        Assert.IsTrue(permissions.AllowNetwork);
        Assert.IsFalse(permissions.AllowDeviceAccess);
    }
}
