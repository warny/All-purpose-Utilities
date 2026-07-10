using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.Reflection.Emit;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates <see cref="EmitWorkerInvocationException"/>'s handling of the worker-side stack trace
/// captured across the process boundary.
/// </summary>
[TestClass]
public class EmitWorkerInvocationExceptionTests
{
    [TestMethod]
    public void RemoteStackTrace_DefaultsToNull_WhenNotProvided()
    {
        var ex = new EmitWorkerInvocationException("boom", "System.InvalidOperationException");

        Assert.IsNull(ex.RemoteStackTrace);
    }

    [TestMethod]
    public void ToString_WithoutRemoteStackTrace_DoesNotMentionTheWorker()
    {
        var ex = new EmitWorkerInvocationException("boom", "System.InvalidOperationException");

        string result = ex.ToString();

        StringAssert.Contains(result, "boom");
        StringAssert.DoesNotMatch(result, new System.Text.RegularExpressions.Regex("isolated Emit worker process"));
    }

    [TestMethod]
    public void ToString_WithRemoteStackTrace_AppendsItAfterTheMessage()
    {
        const string remoteStackTrace = "   at Some.Worker.Method()";
        var ex = new EmitWorkerInvocationException("boom", "System.InvalidOperationException", remoteStackTrace);

        string result = ex.ToString();

        StringAssert.Contains(result, "boom");
        StringAssert.Contains(result, "isolated Emit worker process");
        StringAssert.Contains(result, remoteStackTrace);
        Assert.IsTrue(result.IndexOf("boom", System.StringComparison.Ordinal) < result.IndexOf(remoteStackTrace, System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void RemoteExceptionTypeName_IsExposed()
    {
        var ex = new EmitWorkerInvocationException("boom", "System.InvalidOperationException");

        Assert.AreEqual("System.InvalidOperationException", ex.RemoteExceptionTypeName);
    }
}
