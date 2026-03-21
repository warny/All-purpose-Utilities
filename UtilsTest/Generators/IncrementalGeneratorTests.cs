using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData.Generators;
using Utils.Parser.Generators;

namespace UtilsTest.Generators;

/// <summary>
/// Verifies that repository source generators use the incremental Roslyn API.
/// </summary>
[TestClass]
public class IncrementalGeneratorTests
{
    /// <summary>
    /// Ensures the ANTLR generator implements <see cref="IIncrementalGenerator"/>.
    /// </summary>
    [TestMethod]
    public void Antlr4GrammarGenerator_ImplementsIncrementalGenerator()
    {
        Assert.IsInstanceOfType<IIncrementalGenerator>(new Antlr4GrammarGenerator());
    }

    /// <summary>
    /// Ensures the OData generator implements <see cref="IIncrementalGenerator"/>.
    /// </summary>
    [TestMethod]
    public void ODataEntityGenerator_ImplementsIncrementalGenerator()
    {
        Assert.IsInstanceOfType<IIncrementalGenerator>(new ODataEntityGenerator());
    }
}
