using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection;

namespace UtilsTest.Reflection;

/// <summary>
/// Tests for <see cref="ReflectionEx"/> extension methods.
/// </summary>
[TestClass]
public class ReflectionExTests
{
    // ─── Type hierarchy used in the tests ───────────────────────────────────────

    private interface IBase { }
    private interface ILeft : IBase { }
    private interface IRight : IBase { }
    private interface IDiamond : ILeft, IRight { }
    private interface IDeep : IDiamond { }

    private class ClassBase : IBase { }
    private class ClassChild : ClassBase, ILeft { }

    // ─── GetDirectInterfaces — interface types ───────────────────────────────────

    [TestMethod]
    public void GetDirectInterfaces_InterfaceWithNoParents_ReturnsEmpty()
    {
        Type[] result = typeof(IBase).GetDirectInterfaces().ToArray();
        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public void GetDirectInterfaces_InterfaceWithOneParent_ReturnsThatParent()
    {
        Type[] result = typeof(ILeft).GetDirectInterfaces().ToArray();
        CollectionAssert.AreEquivalent(new[] { typeof(IBase) }, result);
    }

    [TestMethod]
    public void GetDirectInterfaces_DiamondInterface_ReturnsBothDirectParents()
    {
        Type[] result = typeof(IDiamond).GetDirectInterfaces().ToArray();
        // IDiamond : ILeft, IRight — IBase is only transitive
        CollectionAssert.AreEquivalent(new[] { typeof(ILeft), typeof(IRight) }, result);
    }

    [TestMethod]
    public void GetDirectInterfaces_InterfaceWithTransitiveParent_ExcludesTransitive()
    {
        Type[] result = typeof(IDiamond).GetDirectInterfaces().ToArray();
        CollectionAssert.DoesNotContain(result, typeof(IBase));
    }

    [TestMethod]
    public void GetDirectInterfaces_SingleLevelDeepInterface_ReturnsDirectParentOnly()
    {
        Type[] result = typeof(IDeep).GetDirectInterfaces().ToArray();
        CollectionAssert.AreEquivalent(new[] { typeof(IDiamond) }, result);
    }

    // ─── GetDirectInterfaces — class types ──────────────────────────────────────

    [TestMethod]
    public void GetDirectInterfaces_ClassWithNoBaseClassInterfaces_ReturnsOwnInterfaces()
    {
        Type[] result = typeof(ClassBase).GetDirectInterfaces().ToArray();
        CollectionAssert.AreEquivalent(new[] { typeof(IBase) }, result);
    }

    [TestMethod]
    public void GetDirectInterfaces_ChildClassAddingInterface_ReturnsOnlyNewInterface()
    {
        Type[] result = typeof(ClassChild).GetDirectInterfaces().ToArray();
        // ClassChild adds ILeft; IBase is already exposed by ClassBase
        CollectionAssert.AreEquivalent(new[] { typeof(ILeft) }, result);
    }

    [TestMethod]
    public void GetDirectInterfaces_ClassWithNoInterfaces_ReturnsEmpty()
    {
        Type[] result = typeof(object).GetDirectInterfaces().ToArray();
        Assert.AreEqual(0, result.Length);
    }
}
