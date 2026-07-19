using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Expressions.Resolvers;

namespace UtilsTest.Expressions;

[TestClass]
public class TypeFinderTests
{
    // ── GetExportedTypes safety ─────────────────────────────────────────────

    [TestMethod]
    public void TypeFinder_DoesNotIndexPublicNestedTypesBySimpleName()
    {
        // TypeFinderFixtureItem (top-level, IsPublic=true) and
        // TypeFinderFixtureContainer.TypeFinderFixtureItem (nested, IsNestedPublic=true)
        // share the same Name. Only the top-level type must be indexed under the simple name.
        const string ns = "UtilsTest.Expressions";
        var finder = new TypeFinder(new ParserOptions(), [ns], [typeof(TypeFinderTests).Assembly]);

        Type? found = finder.FindType("TypeFinderFixtureItem", []);

        Assert.IsNotNull(found, "TypeFinderFixtureItem must be found by simple name.");
        Assert.AreEqual(typeof(TypeFinderFixtureItem), found,
            "Simple name must resolve to the top-level type, not the nested public one.");
    }

    [TestMethod]
    public void TypeFinder_IndexesTopLevelPublicTypeByFullName()
    {
        const string ns = "UtilsTest.Expressions";
        var finder = new TypeFinder(new ParserOptions(), [ns], [typeof(TypeFinderTests).Assembly]);

        Type? found = finder.FindType(typeof(TypeFinderFixtureItem).FullName!, []);

        Assert.IsNotNull(found);
        Assert.AreEqual(typeof(TypeFinderFixtureItem), found);
    }

    [TestMethod]
    public void TypeFinder_HandlesAssemblyWithInaccessibleTypes_DoesNotThrow()
    {
        // Use all domain assemblies (some may have types that cannot be loaded) to verify
        // that GetExportedTypes() exception handling prevents TypeInitializationException.
        Assert.IsNotNull(new TypeFinder(new ParserOptions(), [], null));
    }
}

// ── Fixture types ───────────────────────────────────────────────────────────
// Top-level public class: IsPublic=true — must be indexed by simple name.

/// <summary>Top-level fixture type whose simple name clashes with a nested public type.</summary>
public class TypeFinderFixtureItem { }

/// <summary>
/// Container whose nested <see cref="TypeFinderFixtureItem"/> shares the simple name of the
/// top-level <see cref="TypeFinderFixtureItem"/> but must not replace it in the TypeFinder index
/// because it is a nested type (IsPublic=false, IsNestedPublic=true).
/// </summary>
public class TypeFinderFixtureContainer
{
    /// <summary>Nested public type — must not be indexed by simple name in TypeFinder.</summary>
    public class TypeFinderFixtureItem { }
}
