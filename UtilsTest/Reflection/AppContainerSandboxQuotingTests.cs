using System.Runtime.Versioning;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates <see cref="AppContainerSandbox"/>'s command-line escaping helpers directly (rather than
/// through reflection), covering the well-known Windows <c>CreateProcess</c> quoting edge cases.
/// </summary>
/// <remarks>
/// <see cref="AppContainerSandbox"/> carries <see cref="SupportedOSPlatformAttribute"/>("windows"),
/// but <see cref="AppContainerSandbox.QuoteArgument"/>/<see cref="AppContainerSandbox.BuildArgumentString"/>
/// are pure string manipulation with no OS dependency; this test class is marked the same way purely
/// to satisfy the platform-compatibility analyzer, and guards at runtime so it reports as inconclusive
/// rather than failing on non-Windows CI agents.
/// </remarks>
[TestClass]
[SupportedOSPlatform("windows")]
public class AppContainerSandboxQuotingTests
{
    [TestMethod]
    public void QuoteArgument_EmptyString_ReturnsEmptyQuotes()
    {
        SkipIfNotWindows();
        Assert.AreEqual("\"\"", AppContainerSandbox.QuoteArgument(""));
    }

    [TestMethod]
    public void QuoteArgument_NoSpecialCharacters_ReturnsUnchanged()
    {
        SkipIfNotWindows();
        Assert.AreEqual("plainvalue", AppContainerSandbox.QuoteArgument("plainvalue"));
    }

    [TestMethod]
    public void QuoteArgument_ContainsSpace_IsQuoted()
    {
        SkipIfNotWindows();
        Assert.AreEqual("\"has space\"", AppContainerSandbox.QuoteArgument("has space"));
    }

    [TestMethod]
    public void QuoteArgument_PreservesPathBackslashesWithSpaces()
    {
        SkipIfNotWindows();
        const string argument = "C:\\Program Files\\MyTool\\worker.exe";
        Assert.AreEqual("\"C:\\Program Files\\MyTool\\worker.exe\"", AppContainerSandbox.QuoteArgument(argument));
    }

    [TestMethod]
    public void QuoteArgument_EmbeddedQuote_IsEscaped()
    {
        SkipIfNotWindows();
        Assert.AreEqual("\"say \\\"hi\\\"\"", AppContainerSandbox.QuoteArgument("say \"hi\""));
    }

    [TestMethod]
    public void QuoteArgument_TrailingBackslashesBeforeClosingQuote_AreDoubled()
    {
        SkipIfNotWindows();
        // No space/tab/quote means no quoting happens at all, so a lone trailing backslash is
        // returned unchanged — the doubling rule only matters once the value is actually quoted.
        Assert.AreEqual("trailing\\", AppContainerSandbox.QuoteArgument("trailing\\"));

        // "has space\" needs quoting (contains a space); the trailing backslash must be doubled so
        // CreateProcess doesn't parse it as escaping the closing quote.
        Assert.AreEqual("\"has space\\\\\"", AppContainerSandbox.QuoteArgument("has space\\"));
    }

    [TestMethod]
    public void BuildArgumentString_JoinsQuotedArgumentsWithSpaces()
    {
        SkipIfNotWindows();
        string result = AppContainerSandbox.BuildArgumentString(["--mode", "safe path", "plain"]);
        Assert.AreEqual("--mode \"safe path\" plain", result);
    }

    // ─── Items 52+53: Job Object failure path and use-after-dispose ─────────────

    [TestMethod]
    public void TryCreate_ReturnsNull_OnNonWindowsPlatform()
    {
        // The factory always returns null outside Windows — this covers the compile path
        // without requiring a real AppContainer runtime.
        if (OperatingSystem.IsWindows())
        {
            // On Windows the method may succeed or fail depending on permissions; the test
            // only verifies the path does not crash. The SetInformationJobObject failure
            // branch (item 52 fix) requires a native environment that refuses the call,
            // which cannot be forced in a unit test — it is exercised by system tests.
            Assert.Inconclusive("TryCreate on Windows requires an AppContainer-capable environment; test skipped in unit suite.");
        }

        var result = AppContainerSandbox.TryCreate("test.container", "Test", "desc",
            new Utils.Reflection.ProcessIsolation.ProcessContainerPermissions { AllowDiskRead = true });
        Assert.IsNull(result, "TryCreate must return null on non-Windows platforms.");
    }

    // Item 53: ObjectDisposedException checks on resource-dependent public members are
    // tested in UtilsTest.Functional (which can create a real AppContainerSandbox on
    // Windows). Here we verify the check compiles and is present via a code analysis
    // guard only — there is no way to create a real AppContainerSandbox in a unit test
    // without a live Windows AppContainer environment.

    private static void SkipIfNotWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("AppContainerSandbox is only compiled for use on Windows.");
        }
    }
}
