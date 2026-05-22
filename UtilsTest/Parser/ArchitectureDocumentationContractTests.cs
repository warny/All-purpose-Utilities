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

        var grammarIndex = content.IndexOf("Grammar", StringComparison.Ordinal);
        var preparationIndex = content.IndexOf("→ Preparation", StringComparison.Ordinal);
        var schedulerIndex = content.IndexOf("→ Scheduler", StringComparison.Ordinal);
        var executionIndex = content.IndexOf("→ Execution", StringComparison.Ordinal);
        var observationIndex = content.IndexOf("→ Observation", StringComparison.Ordinal);
        var exportIndex = content.IndexOf("→ Export", StringComparison.Ordinal);
        var analysisIndex = content.IndexOf("→ Analysis", StringComparison.Ordinal);

        Assert.IsTrue(grammarIndex >= 0);
        Assert.IsTrue(preparationIndex > grammarIndex);
        Assert.IsTrue(schedulerIndex > preparationIndex);
        Assert.IsTrue(executionIndex > schedulerIndex);
        Assert.IsTrue(observationIndex > executionIndex);
        Assert.IsTrue(exportIndex > observationIndex);
        Assert.IsTrue(analysisIndex > exportIndex);
    }
}
