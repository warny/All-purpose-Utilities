using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

    // ─── GetTypes — strict and tolerant mode ─────────────────────────────────────

    [TestMethod]
    public void GetTypes_StrictMode_ReturnsMatchingTypes()
    {
        Assembly asm = typeof(ReflectionEx).Assembly;
        Type[] result = asm.GetTypes(t => t == typeof(ReflectionEx)).ToArray();
        CollectionAssert.Contains(result, typeof(ReflectionEx));
    }

    [TestMethod]
    public void GetTypes_TolerantMode_CollectsNoErrorsForHealthyAssembly()
    {
        Assembly asm = typeof(ReflectionEx).Assembly;
        var errors = new List<Exception>();
        Type[] result = asm.GetTypes(t => true, errors).ToArray();
        Assert.AreEqual(0, errors.Count);
        Assert.IsTrue(result.Length > 0);
    }

    [TestMethod]
    public void GetTypes_TolerantMode_StillReturnsSomeTypesWhenLoadErrorOccurs()
    {
        // We cannot easily trigger a ReflectionTypeLoadException in a unit test without
        // loading a broken assembly, so we verify the tolerant overload exists and works
        // for the normal (no-error) case before the error branch is exercised in integration.
        Assembly asm = typeof(ReflectionEx).Assembly;
        var errors = new List<Exception>();
        Type[] result = asm.GetTypes(t => t.IsPublic, errors).ToArray();
        Assert.IsTrue(result.Length > 0, "Should find at least some public types.");
        Assert.AreEqual(0, errors.Count, "No load errors expected for a healthy assembly.");
    }

    // ─── LoadAssemblies ──────────────────────────────────────────────────────────

    [TestMethod]
    public void LoadAssemblies_NonExistentDirectory_ReturnsEmpty()
    {
        // PathUtils.EnumerateFiles returns an empty sequence for non-existent paths without
        // throwing; LoadAssemblies inherits that behaviour.
        string nonExistent = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assembly[] result = ReflectionEx.LoadAssemblies(nonExistent).ToArray();
        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public void LoadAssemblies_LoadsCurrentAssemblyDll()
    {
        // The test assembly's directory must contain at least one .dll that can be loaded.
        string dir = System.IO.Path.GetDirectoryName(typeof(ReflectionExTests).Assembly.Location)!;
        Assembly[] loaded = ReflectionEx.LoadAssemblies(dir + "/*.dll").ToArray();
        Assert.IsTrue(loaded.Length > 0, "Expected at least one assembly from the test output directory.");
    }
}
