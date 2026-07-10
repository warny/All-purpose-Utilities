using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates the mapping from <see cref="ProcessContainerPermissions"/> to sandbox-exec profile
/// clauses, without requiring macOS or a real <c>sandbox-exec</c> binary (pure string construction).
/// </summary>
[TestClass]
public class MacOsSandboxExecContainerTests
{
    [TestMethod]
    public void BuildProfile_AlwaysDeniesByDefaultAndAllowsFileReadAndProcess()
    {
        string profile = MacOsSandboxExecContainer.BuildProfile(ProcessContainerPermissions.Default);

        StringAssert.Contains(profile, "(deny default)");
        StringAssert.Contains(profile, "(allow file-read*)");
        StringAssert.Contains(profile, "(allow process*)");
    }

    [TestMethod]
    public void BuildProfile_DefaultPermissions_DoesNotAllowWriteNetworkOrDevices()
    {
        string profile = MacOsSandboxExecContainer.BuildProfile(ProcessContainerPermissions.Default);

        Assert.IsFalse(profile.Contains("(allow file-write*)"));
        Assert.IsFalse(profile.Contains("(allow network*)"));
        Assert.IsFalse(profile.Contains("(allow iokit-open)"));
        Assert.IsFalse(profile.Contains("(allow process-info*)"));
    }

    [TestMethod]
    public void BuildProfile_AllowDiskWrite_AddsFileWriteClause()
    {
        var permissions = new ProcessContainerPermissions { AllowDiskWrite = true };

        string profile = MacOsSandboxExecContainer.BuildProfile(permissions);

        StringAssert.Contains(profile, "(allow file-write*)");
    }

    [TestMethod]
    public void BuildProfile_AllowNetwork_AddsNetworkClause()
    {
        var permissions = new ProcessContainerPermissions { AllowNetwork = true };

        string profile = MacOsSandboxExecContainer.BuildProfile(permissions);

        StringAssert.Contains(profile, "(allow network*)");
    }

    [TestMethod]
    public void BuildProfile_AllowDeviceAccess_AddsIoKitClause()
    {
        var permissions = new ProcessContainerPermissions { AllowDeviceAccess = true };

        string profile = MacOsSandboxExecContainer.BuildProfile(permissions);

        StringAssert.Contains(profile, "(allow iokit-open)");
    }

    [TestMethod]
    public void BuildProfile_AllowProcessDebugging_AddsProcessInfoClause()
    {
        var permissions = new ProcessContainerPermissions { AllowProcessDebugging = true };

        string profile = MacOsSandboxExecContainer.BuildProfile(permissions);

        StringAssert.Contains(profile, "(allow process-info*)");
    }
}
