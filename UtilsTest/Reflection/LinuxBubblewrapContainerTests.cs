using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates the mapping from <see cref="ProcessContainerPermissions"/> to <c>bwrap</c> arguments,
/// without requiring Linux or a real <c>bwrap</c> binary (pure string/list construction).
/// </summary>
[TestClass]
public class LinuxBubblewrapContainerTests
{
    [TestMethod]
    public void BuildArguments_DefaultPermissions_IsRestrictive()
    {
        List<string> args = LinuxBubblewrapContainer.BuildArguments(
            "/bin/worker", ["--flag"], ProcessContainerPermissions.Default);

        CollectionAssert.Contains(args, "--die-with-parent");
        CollectionAssert.Contains(args, "--unshare-all");
        CollectionAssert.Contains(args, "--tmpfs");
        CollectionAssert.Contains(args, "--dev");
        CollectionAssert.DoesNotContain(args, "--share-net");
        CollectionAssert.DoesNotContain(args, "--dev-bind");
        CollectionAssert.DoesNotContain(args, "--bind");
    }

    [TestMethod]
    public void BuildArguments_AllowNetwork_AddsShareNet()
    {
        var permissions = new ProcessContainerPermissions { AllowNetwork = true };

        List<string> args = LinuxBubblewrapContainer.BuildArguments("/bin/worker", [], permissions);

        CollectionAssert.Contains(args, "--share-net");
    }

    [TestMethod]
    public void BuildArguments_AllowDeviceAccess_BindsRealDev()
    {
        var permissions = new ProcessContainerPermissions { AllowDeviceAccess = true };

        List<string> args = LinuxBubblewrapContainer.BuildArguments("/bin/worker", [], permissions);

        CollectionAssert.Contains(args, "--dev-bind");
        CollectionAssert.DoesNotContain(args, "--dev");
    }

    [TestMethod]
    public void BuildArguments_AllowDiskWrite_WithoutIpcSocket_UsesTmpfsOnly()
    {
        // When AllowDiskWrite is true but no IPC socket path is provided, /tmp must still be an
        // isolated tmpfs. The host /tmp must never be exposed without an explicit socket path.
        var permissions = new ProcessContainerPermissions { AllowDiskWrite = true };

        List<string> args = LinuxBubblewrapContainer.BuildArguments("/bin/worker", [], permissions);

        CollectionAssert.Contains(args, "--tmpfs");
        CollectionAssert.DoesNotContain(args, "--bind");
    }

    [TestMethod]
    public void BuildArguments_AllowDiskWrite_WithIpcSocket_UsesTmpfsAndBindsSocketOnly()
    {
        // The worker should see only the specific IPC socket, not all of /tmp.
        const string socketPath = "/tmp/CoreFxPipe_abc-1234";
        var permissions = new ProcessContainerPermissions { AllowDiskWrite = true };

        List<string> args = LinuxBubblewrapContainer.BuildArguments(
            "/bin/worker", [], permissions, ipcSocketPath: socketPath);

        // /tmp is a fresh tmpfs ...
        CollectionAssert.Contains(args, "--tmpfs");

        // ... and the exact socket file is bind-mounted into it.
        int bindIndex = args.IndexOf("--bind");
        Assert.AreNotEqual(-1, bindIndex, "--bind must be present when ipcSocketPath is supplied.");

        // Both source and destination paths must be the socket path, not /tmp as a whole.
        Assert.AreEqual(socketPath, args[bindIndex + 1]);
        Assert.AreEqual(socketPath, args[bindIndex + 2]);

        // The bind must appear BEFORE the -- separator so it applies to the container setup.
        int separatorIndex = args.IndexOf("--");
        Assert.IsTrue(bindIndex < separatorIndex, "--bind must precede the -- separator.");
    }

    [TestMethod]
    public void BuildArguments_AppendsExecutablePathAndArgumentsAfterSeparator()
    {
        List<string> args = LinuxBubblewrapContainer.BuildArguments(
            "/bin/worker", ["--mode", "safe"], ProcessContainerPermissions.Default);

        int separatorIndex = args.IndexOf("--");
        Assert.AreNotEqual(-1, separatorIndex);
        Assert.AreEqual("/bin/worker", args[separatorIndex + 1]);
        Assert.AreEqual("--mode", args[separatorIndex + 2]);
        Assert.AreEqual("safe", args[separatorIndex + 3]);
    }

    [TestMethod]
    public void BuildArguments_AlwaysBindsRootReadOnly()
    {
        List<string> args = LinuxBubblewrapContainer.BuildArguments("/bin/worker", [], ProcessContainerPermissions.Default);

        int roBindIndex = args.IndexOf("--ro-bind");
        Assert.AreNotEqual(-1, roBindIndex);
        Assert.AreEqual("/", args[roBindIndex + 1]);
        Assert.AreEqual("/", args[roBindIndex + 2]);
    }
}
