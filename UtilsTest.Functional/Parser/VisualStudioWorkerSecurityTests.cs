using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.VisualStudio.Worker;

namespace UtilsTest.Parser;

/// <summary>
/// Functional (non-security) tests for the plugin worker pipeline's assembly filtering.
/// </summary>
[TestClass]
public class VisualStudioWorkerSecurityTests
{
    [TestMethod]
    public void PluginAssemblyVerifier_Filter_EmptyInputReturnsEmpty()
    {
        string[] result = PluginAssemblyVerifier.Filter([]);
        Assert.AreEqual(0, result.Length);
    }
}
