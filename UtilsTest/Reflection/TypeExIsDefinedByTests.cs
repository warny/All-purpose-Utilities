using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Reflection;

namespace UtilsTest.Reflection;

/// <summary>
/// Characterization coverage for <see cref="TypeEx.IsDefinedBy(Type, Type)"/>. #588 replaces the two
/// <c>Enumerable.Any</c> traversals inside its interface branches (generic-interface-definition and
/// plain-interface) with indexed loops over the same <see cref="Type.GetInterfaces"/> array, purely as
/// allocation/scaffolding cleanup — same order, same short-circuit, same exceptions, same
/// <c>Type.GetInterfaces()</c> call site and result. These tests lock the exact observable baseline,
/// including behavior for a malformed custom <see cref="Type.GetInterfaces"/> override (via
/// <see cref="TypeDelegator"/>) that a naive rewrite (indexing <c>types.Length</c> or a foreach without a
/// null guard) could silently turn from <see cref="ArgumentNullException"/> into
/// <see cref="NullReferenceException"/>.
/// </summary>
[TestClass]
public class TypeExIsDefinedByTests
{
    // ------------------------------------------------------------------------------------------
    // Test-only fixtures
    // ------------------------------------------------------------------------------------------

    /// <summary>A minimal non-generic base interface for the generic-interface-inheritance fixture.</summary>
    private interface IBase<T> { }

    /// <summary>A generic interface that itself extends <see cref="IBase{T}"/>.</summary>
    private interface IDerived<T> : IBase<T> { }

    /// <summary>Implements <see cref="IDerived{T}"/> (and transitively <see cref="IBase{T}"/>) closed over <see cref="int"/>.</summary>
    private sealed class ImplementsDerived : IDerived<int> { }

    /// <summary>An unrelated non-generic marker interface used for miss cases.</summary>
    private interface IUnrelated { }

    /// <summary>
    /// A <see cref="TypeDelegator"/> that forwards every member to <paramref name="delegatingType"/> except
    /// <see cref="GetInterfaces"/>, which returns a caller-supplied (possibly malformed) array instead of the
    /// delegating type's real interfaces. Used only to characterize <c>IsDefinedBy</c>'s behavior against a
    /// custom <see cref="Type"/> implementation, since ordinary CLR types never return null or contain null
    /// elements from <see cref="Type.GetInterfaces"/>.
    /// </summary>
    private sealed class InterfacesOverrideType : TypeDelegator
    {
        private readonly Type[] _interfaces;

        /// <summary>Creates a delegator over <paramref name="delegatingType"/> that reports <paramref name="interfaces"/> instead of its real interfaces.</summary>
        /// <param name="delegatingType">The real type to forward all other members to.</param>
        /// <param name="interfaces">The (possibly null or null-containing) array <see cref="GetInterfaces"/> should return.</param>
        public InterfacesOverrideType(Type delegatingType, Type[] interfaces)
            : base(delegatingType)
        {
            _interfaces = interfaces;
        }

        /// <inheritdoc />
        public override Type[] GetInterfaces() => _interfaces;
    }

    // ------------------------------------------------------------------------------------------
    // Null-argument guards (unaffected control — TypeEx.cs untouched by #588 for these lines)
    // ------------------------------------------------------------------------------------------

    /// <summary>A null <c>type</c> throws <see cref="ArgumentNullException"/> with <c>ParamName</c> "type", before any interface logic runs.</summary>
    [TestMethod]
    public void IsDefinedBy_NullType_ThrowsArgumentNullExceptionForType()
    {
        var ex = Assert.ThrowsExactly<ArgumentNullException>(() => ((Type)null!).IsDefinedBy(typeof(object)));
        Assert.AreEqual("type", ex.ParamName);
    }

    /// <summary>A null <c>baseType</c> throws <see cref="ArgumentNullException"/> with <c>ParamName</c> "baseType".</summary>
    [TestMethod]
    public void IsDefinedBy_NullBaseType_ThrowsArgumentNullExceptionForBaseType()
    {
        var ex = Assert.ThrowsExactly<ArgumentNullException>(() => typeof(string).IsDefinedBy(null!));
        Assert.AreEqual("baseType", ex.ParamName);
    }

    // ------------------------------------------------------------------------------------------
    // Class-path negative controls (untouched code path)
    // ------------------------------------------------------------------------------------------

    [TestMethod]
    public void IsDefinedBy_ExactSameType_ReturnsTrue()
    {
        Assert.IsTrue(typeof(string).IsDefinedBy(typeof(string)));
    }

    [TestMethod]
    public void IsDefinedBy_DerivedClassToBaseClass_ReturnsTrue()
    {
        Assert.IsTrue(typeof(string).IsDefinedBy(typeof(object)));
    }

    [TestMethod]
    public void IsDefinedBy_UnrelatedClasses_ReturnsFalse()
    {
        Assert.IsFalse(typeof(string).IsDefinedBy(typeof(Exception)));
    }

    [TestMethod]
    public void IsDefinedBy_ClosedGenericClassToGenericClassDefinition_ReturnsTrue()
    {
        Assert.IsTrue(typeof(List<int>).IsDefinedBy(typeof(List<>)));
    }

    // ------------------------------------------------------------------------------------------
    // Non-generic interface branch
    // ------------------------------------------------------------------------------------------

    [TestMethod]
    public void IsDefinedBy_DirectNonGenericInterfaceImplementation_ReturnsTrue()
    {
        Assert.IsTrue(typeof(List<int>).IsDefinedBy(typeof(IEnumerable)));
    }

    [TestMethod]
    public void IsDefinedBy_ClosedGenericInterfaceAsBaseType_ReturnsTrue()
    {
        // IEnumerable<int> is generic but NOT a generic type definition, so this intentionally
        // goes through the non-generic-interface branch (baseType.IsGenericTypeDefinition == false).
        Assert.IsTrue(typeof(List<int>).IsDefinedBy(typeof(IEnumerable<int>)));
    }

    [TestMethod]
    public void IsDefinedBy_NonGenericInterface_NoMatch_ReturnsFalse()
    {
        Assert.IsFalse(typeof(List<int>).IsDefinedBy(typeof(IUnrelated)));
    }

    // ------------------------------------------------------------------------------------------
    // Generic-interface-definition branch
    // ------------------------------------------------------------------------------------------

    [TestMethod]
    public void IsDefinedBy_GenericInterfaceDefinition_ImplementedThroughInterfaceScan_ReturnsTrue()
    {
        Assert.IsTrue(typeof(List<int>).IsDefinedBy(typeof(IEnumerable<>)));
    }

    [TestMethod]
    public void IsDefinedBy_GenericMathInterface_Double_INumber_ReturnsTrue()
    {
        Assert.IsTrue(typeof(double).IsDefinedBy(typeof(INumber<>)));
    }

    [TestMethod]
    public void IsDefinedBy_GenericMathInterface_Int_IBinaryInteger_ReturnsTrue()
    {
        Assert.IsTrue(typeof(int).IsDefinedBy(typeof(IBinaryInteger<>)));
    }

    [TestMethod]
    public void IsDefinedBy_GenericMathInterface_Double_IFloatingPoint_ReturnsTrue()
    {
        Assert.IsTrue(typeof(double).IsDefinedBy(typeof(IFloatingPoint<>)));
    }

    /// <summary>A miss on the generic-interface-definition branch must scan the complete interface array before returning false.</summary>
    [TestMethod]
    public void IsDefinedBy_GenericMathInterface_Double_IBinaryInteger_Miss_ReturnsFalse()
    {
        Assert.IsFalse(typeof(double).IsDefinedBy(typeof(IBinaryInteger<>)));
    }

    /// <summary>
    /// When <c>type</c> itself is the closed form of the generic interface definition, the direct
    /// <c>type.IsGenericType &amp;&amp; type.GetGenericTypeDefinition() == baseType</c> check short-circuits
    /// before <see cref="Type.GetInterfaces"/> is ever called.
    /// </summary>
    [TestMethod]
    public void IsDefinedBy_InterfaceTypeItself_MatchesThroughDirectGenericCheck()
    {
        Assert.IsTrue(typeof(IEnumerable<int>).IsDefinedBy(typeof(IEnumerable<>)));
    }

    // ------------------------------------------------------------------------------------------
    // Generic interface inheritance (test-only hierarchy)
    // ------------------------------------------------------------------------------------------

    [TestMethod]
    public void IsDefinedBy_GenericInterfaceInheritance_DirectInterface_ReturnsTrue()
    {
        Assert.IsTrue(typeof(ImplementsDerived).IsDefinedBy(typeof(IDerived<>)));
    }

    [TestMethod]
    public void IsDefinedBy_GenericInterfaceInheritance_InheritedInterface_ReturnsTrue()
    {
        Assert.IsTrue(typeof(ImplementsDerived).IsDefinedBy(typeof(IBase<>)));
    }

    // ------------------------------------------------------------------------------------------
    // Malformed custom Type.GetInterfaces() — generic-interface-definition branch
    // ------------------------------------------------------------------------------------------

    /// <summary>A null <see cref="Type.GetInterfaces"/> result on the generic branch throws <see cref="ArgumentNullException"/> ("source"), mirroring <c>Enumerable.Any</c>'s own null-source guard.</summary>
    [TestMethod]
    public void IsDefinedBy_GenericBranch_NullInterfacesArray_ThrowsArgumentNullExceptionForSource()
    {
        var fakeType = new InterfacesOverrideType(typeof(object), null!);

        var ex = Assert.ThrowsExactly<ArgumentNullException>(() => fakeType.IsDefinedBy(typeof(IEnumerable<>)));
        Assert.AreEqual("source", ex.ParamName);
    }

    /// <summary>A null element in the generic branch's interfaces array throws when <c>i.IsGenericType</c> is evaluated on it.</summary>
    [TestMethod]
    public void IsDefinedBy_GenericBranch_NullElementInInterfacesArray_ThrowsNullReferenceException()
    {
        var fakeType = new InterfacesOverrideType(typeof(object), [null!]);

        Assert.ThrowsExactly<NullReferenceException>(() => fakeType.IsDefinedBy(typeof(IEnumerable<>)));
    }

    /// <summary>A matching element BEFORE a null element short-circuits — the null element is never inspected.</summary>
    [TestMethod]
    public void IsDefinedBy_GenericBranch_MatchBeforeNullElement_ReturnsTrueWithoutThrowing()
    {
        var fakeType = new InterfacesOverrideType(typeof(object), [typeof(IEnumerable<int>), null!]);

        Assert.IsTrue(fakeType.IsDefinedBy(typeof(IEnumerable<>)));
    }

    /// <summary>A null element BEFORE a later matching element still throws — the scan does not skip past it.</summary>
    [TestMethod]
    public void IsDefinedBy_GenericBranch_NullElementBeforeLaterMatch_ThrowsNullReferenceException()
    {
        var fakeType = new InterfacesOverrideType(typeof(object), [null!, typeof(IEnumerable<int>)]);

        Assert.ThrowsExactly<NullReferenceException>(() => fakeType.IsDefinedBy(typeof(IEnumerable<>)));
    }

    // ------------------------------------------------------------------------------------------
    // Malformed custom Type.GetInterfaces() — non-generic-interface branch
    // ------------------------------------------------------------------------------------------

    /// <summary>A null <see cref="Type.GetInterfaces"/> result on the non-generic branch throws <see cref="ArgumentNullException"/> ("source"), same as the generic branch.</summary>
    [TestMethod]
    public void IsDefinedBy_NonGenericBranch_NullInterfacesArray_ThrowsArgumentNullExceptionForSource()
    {
        var fakeType = new InterfacesOverrideType(typeof(object), null!);

        var ex = Assert.ThrowsExactly<ArgumentNullException>(() => fakeType.IsDefinedBy(typeof(IDisposable)));
        Assert.AreEqual("source", ex.ParamName);
    }

    /// <summary>
    /// A null element in the non-generic branch's interfaces array does NOT throw — <c>i == baseType</c>
    /// simply evaluates to false for a null <paramref name="i"/>, unlike the generic branch's
    /// <c>i.IsGenericType</c> property access.
    /// </summary>
    [TestMethod]
    public void IsDefinedBy_NonGenericBranch_NullElementInInterfacesArray_ComparesFalseWithoutThrowing()
    {
        var fakeType = new InterfacesOverrideType(typeof(object), [null!]);

        Assert.IsFalse(fakeType.IsDefinedBy(typeof(IDisposable)));
    }

    /// <summary>A matching element BEFORE a null element short-circuits on the non-generic branch too.</summary>
    [TestMethod]
    public void IsDefinedBy_NonGenericBranch_MatchBeforeNullElement_ReturnsTrue()
    {
        var fakeType = new InterfacesOverrideType(typeof(object), [typeof(IDisposable), null!]);

        Assert.IsTrue(fakeType.IsDefinedBy(typeof(IDisposable)));
    }

    /// <summary>A null element BEFORE a later matching element does not prevent the later match from being found.</summary>
    [TestMethod]
    public void IsDefinedBy_NonGenericBranch_NullElementBeforeLaterMatch_ReturnsTrue()
    {
        var fakeType = new InterfacesOverrideType(typeof(object), [null!, typeof(IDisposable)]);

        Assert.IsTrue(fakeType.IsDefinedBy(typeof(IDisposable)));
    }
}
