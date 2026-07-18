using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.Reflection.Emit;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates which interface member shapes can be forwarded across a process boundary for the
/// isolated <c>LibraryMapper.Emit&lt;I&gt;</c> path.
/// </summary>
[TestClass]
public class CrossProcessMarshalingTests
{
    public enum SampleEnum { A, B }

    public struct BlittableStruct
    {
        public int X;
        public double Y;
        public SampleEnum Kind;
    }

    public struct NestedUnsupportedStruct
    {
        public int X;
        public object Reference;
    }

    public sealed class ReferenceType { }

    public interface ISupportedOperations : IDisposable
    {
        int Add(int a, int b);
        string Concat(string a, string b);
        SampleEnum Identity(SampleEnum value);
        BlittableStruct Transform(BlittableStruct value);
        int[] Sum(int[] values, int count);
        void SetByRef(ref int value);
        bool TryParse(string text, out int value);
    }

    public interface IUnsupportedPointer : IDisposable
    {
        void Foo(IntPtr handle);
    }

    public interface IUnsupportedReferenceType : IDisposable
    {
        void Foo(ReferenceType value);
    }

    public interface IUnsupportedNestedField : IDisposable
    {
        void Foo(NestedUnsupportedStruct value);
    }

    public interface IUnsupportedReturnType : IDisposable
    {
        ReferenceType Foo();
    }

    [TestMethod]
    [DataRow(typeof(int))]
    [DataRow(typeof(uint))]
    [DataRow(typeof(long))]
    [DataRow(typeof(bool))]
    [DataRow(typeof(double))]
    [DataRow(typeof(char))]
    [DataRow(typeof(string))]
    [DataRow(typeof(SampleEnum))]
    [DataRow(typeof(BlittableStruct))]
    [DataRow(typeof(int[]))]
    public void IsSupportedType_ReturnsTrue_ForMarshalableShapes(Type type)
    {
        Assert.IsTrue(CrossProcessMarshaling.IsSupportedType(type, 0));
    }

    [TestMethod]
    public void IsSupportedType_ReturnsFalse_ForIntPtr()
    {
        Assert.IsFalse(CrossProcessMarshaling.IsSupportedType(typeof(IntPtr), 0));
        Assert.IsFalse(CrossProcessMarshaling.IsSupportedType(typeof(UIntPtr), 0));
    }

    [TestMethod]
    public void IsSupportedType_ReturnsFalse_ForRawPointer()
    {
        Type pointerType = typeof(int).MakePointerType();
        Assert.IsFalse(CrossProcessMarshaling.IsSupportedType(pointerType, 0));
    }

    [TestMethod]
    public void IsSupportedType_ReturnsFalse_ForArbitraryReferenceType()
    {
        Assert.IsFalse(CrossProcessMarshaling.IsSupportedType(typeof(ReferenceType), 0));
    }

    [TestMethod]
    public void IsSupportedType_ReturnsFalse_ForStructWithUnsupportedField()
    {
        Assert.IsFalse(CrossProcessMarshaling.IsSupportedType(typeof(NestedUnsupportedStruct), 0));
    }

    [TestMethod]
    public void IsSupportedType_ReturnsTrue_ForByRefOfSupportedType()
    {
        Type byRefInt = typeof(int).MakeByRefType();
        Assert.IsTrue(CrossProcessMarshaling.IsSupportedType(byRefInt, 0));
    }

    [TestMethod]
    public void EnsureInterfaceIsSupported_DoesNotThrow_ForFullySupportedInterface()
    {
        CrossProcessMarshaling.EnsureInterfaceIsSupported(typeof(ISupportedOperations));
    }

    [TestMethod]
    public void EnsureInterfaceIsSupported_Throws_ForIntPtrParameter()
    {
        var ex = Assert.ThrowsException<NotSupportedException>(
            () => CrossProcessMarshaling.EnsureInterfaceIsSupported(typeof(IUnsupportedPointer)));
        StringAssert.Contains(ex.Message, "EmitInProcess");
    }

    [TestMethod]
    public void EnsureInterfaceIsSupported_Throws_ForArbitraryReferenceTypeParameter()
    {
        Assert.ThrowsException<NotSupportedException>(
            () => CrossProcessMarshaling.EnsureInterfaceIsSupported(typeof(IUnsupportedReferenceType)));
    }

    [TestMethod]
    public void EnsureInterfaceIsSupported_Throws_ForStructWithUnsupportedField()
    {
        Assert.ThrowsException<NotSupportedException>(
            () => CrossProcessMarshaling.EnsureInterfaceIsSupported(typeof(IUnsupportedNestedField)));
    }

    [TestMethod]
    public void EnsureInterfaceIsSupported_Throws_ForUnsupportedReturnType()
    {
        Assert.ThrowsException<NotSupportedException>(
            () => CrossProcessMarshaling.EnsureInterfaceIsSupported(typeof(IUnsupportedReturnType)));
    }

    // ─── Item 42: struct validation matches JSON contract ────────────────────────

    /// <summary>Struct with only a non-public field (not serialized by JsonSerializer).</summary>
    public struct StructWithPrivateUnsupportedField
    {
        // The private field holds an object but is not serialized by System.Text.Json,
        // so the struct must still be considered supported.
#pragma warning disable CS0649
        private object _hidden;
#pragma warning restore CS0649
        public int Public;
    }

    /// <summary>Struct with a public property that uses a supported type.</summary>
    public struct StructWithSupportedProperty
    {
        public int Count { get; set; }
    }

    /// <summary>Struct with a public property that uses an unsupported type.</summary>
    public struct StructWithUnsupportedProperty
    {
        public object Bad { get; set; }
    }

    public interface IStructWithPrivateField : IDisposable
    {
        StructWithPrivateUnsupportedField Get();
    }

    public interface IStructWithSupportedProp : IDisposable
    {
        StructWithSupportedProperty Get();
    }

    public interface IStructWithUnsupportedProp : IDisposable
    {
        StructWithUnsupportedProperty Get();
    }

    [TestMethod]
    public void IsSupportedType_ReturnsTrue_ForStructWithOnlyPrivateUnsupportedField()
    {
        // Non-public fields are not serialized, so the struct is valid across the boundary.
        Assert.IsTrue(CrossProcessMarshaling.IsSupportedType(typeof(StructWithPrivateUnsupportedField), 0));
    }

    [TestMethod]
    public void IsSupportedType_ReturnsTrue_ForStructWithSupportedPublicProperty()
    {
        Assert.IsTrue(CrossProcessMarshaling.IsSupportedType(typeof(StructWithSupportedProperty), 0));
    }

    [TestMethod]
    public void IsSupportedType_ReturnsFalse_ForStructWithUnsupportedPublicProperty()
    {
        Assert.IsFalse(CrossProcessMarshaling.IsSupportedType(typeof(StructWithUnsupportedProperty), 0));
    }

    [TestMethod]
    public void EnsureInterfaceIsSupported_DoesNotThrow_ForStructWithPrivateUnsupportedField()
    {
        CrossProcessMarshaling.EnsureInterfaceIsSupported(typeof(IStructWithPrivateField));
    }

    [TestMethod]
    public void EnsureInterfaceIsSupported_DoesNotThrow_ForStructWithSupportedProperty()
    {
        CrossProcessMarshaling.EnsureInterfaceIsSupported(typeof(IStructWithSupportedProp));
    }

    [TestMethod]
    public void EnsureInterfaceIsSupported_Throws_ForStructWithUnsupportedPublicProperty()
    {
        Assert.ThrowsException<NotSupportedException>(
            () => CrossProcessMarshaling.EnsureInterfaceIsSupported(typeof(IStructWithUnsupportedProp)));
    }
}
