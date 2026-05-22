using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Parser;

[TestClass]
public class ArchitectureDocumentationContractTests
{
    [TestMethod]
    public void RuntimeArchitecture_ContainsDocumentedPipelineStages()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/parser/RuntimeArchitecture.md"));
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "Grammar");
        StringAssert.Contains(content, "Preparation");
        StringAssert.Contains(content, "Scheduler");
        StringAssert.Contains(content, "Execution");
        StringAssert.Contains(content, "Observation");
        StringAssert.Contains(content, "Export");
        StringAssert.Contains(content, "Analysis");
    }
}
