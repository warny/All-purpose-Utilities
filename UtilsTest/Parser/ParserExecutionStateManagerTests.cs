using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies parser execution-state manager contracts.
/// </summary>
[TestClass]
public class ParserExecutionStateManagerTests
{
    /// <summary>
    /// Verifies that the no-op execution-state manager is exposed as a singleton.
    /// </summary>
    [TestMethod]
    public void NullParserExecutionStateManager_Instance_IsSingleton()
    {
        Assert.IsNotNull(NullParserExecutionStateManager.Instance);
        Assert.AreSame(NullParserExecutionStateManager.Instance, NullParserExecutionStateManager.Instance);
    }

    /// <summary>
    /// Verifies that the no-op execution-state manager captures a non-null snapshot.
    /// </summary>
    [TestMethod]
    public void NullParserExecutionStateManager_Capture_ReturnsNonNullSnapshot()
    {
        var snapshot = NullParserExecutionStateManager.Instance.Capture();

        Assert.IsNotNull(snapshot);
    }

    /// <summary>
    /// Verifies that restoring a no-op snapshot does not throw.
    /// </summary>
    [TestMethod]
    public void NullParserExecutionStateManager_RestoreCapture_DoesNotThrow()
    {
        var manager = NullParserExecutionStateManager.Instance;
        var snapshot = manager.Capture();

        manager.Restore(snapshot);
    }

    /// <summary>
    /// Verifies that the no-op execution-state manager rejects null snapshots.
    /// </summary>
    [TestMethod]
    public void NullParserExecutionStateManager_RestoreNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => NullParserExecutionStateManager.Instance.Restore(null!));
    }
}
