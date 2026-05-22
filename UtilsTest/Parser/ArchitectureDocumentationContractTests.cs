using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Parser;

[TestClass]
public class ArchitectureDocumentationContractTests
{
    [TestMethod]
    public void RuntimeArchitecture_ContainsPipelineInOrder()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/parser/RuntimeArchitecture.md"));
        var content = File.ReadAllText(path);

        var expectedPipeline = "Grammar\n→ Preparation\n→ Scheduler\n→ Execution\n→ Observation\n→ Export\n→ Analysis";
        StringAssert.Contains(content, expectedPipeline);
    }
}
