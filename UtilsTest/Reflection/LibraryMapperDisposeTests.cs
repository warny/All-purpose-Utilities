using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates <see cref="LibraryMapper"/>'s disposal behavior, specifically that mapped delegate
/// fields and properties are cleared on <see cref="LibraryMapper.Dispose"/> so post-Dispose accesses
/// return <see langword="null"/> instead of a stale function pointer into freed native memory.
/// </summary>
[TestClass]
public class LibraryMapperDisposeTests
{
    // ─── Item 46: clear mapped delegates on Dispose ──────────────────────────────

    private class MapperWithField : LibraryMapper
    {
        [External]
        public Action? NativeFunction;
    }

    private class MapperWithProperty : LibraryMapper
    {
        [External]
        public Action? NativeFunction { get; set; }
    }

    [TestMethod]
    public void Dispose_SetsMappedFieldToNull()
    {
        var mapper = new MapperWithField();
        mapper.NativeFunction = () => { };
        Assert.IsNotNull(mapper.NativeFunction, "Pre-condition: field must be set before Dispose.");

        mapper.Dispose();

        Assert.IsNull(mapper.NativeFunction,
            "Dispose must clear [External] delegate fields to prevent stale function-pointer calls.");
    }

    [TestMethod]
    public void Dispose_SetsMappedPropertyToNull()
    {
        var mapper = new MapperWithProperty();
        mapper.NativeFunction = () => { };
        Assert.IsNotNull(mapper.NativeFunction, "Pre-condition: property must be set before Dispose.");

        mapper.Dispose();

        Assert.IsNull(mapper.NativeFunction,
            "Dispose must clear [External] delegate properties to prevent stale function-pointer calls.");
    }

    [TestMethod]
    public void Dispose_SetsIsDisposedTrue()
    {
        var mapper = new MapperWithField();
        Assert.IsFalse(mapper.IsDisposed, "Pre-condition: IsDisposed must be false before Dispose.");

        mapper.Dispose();

        Assert.IsTrue(mapper.IsDisposed);
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        var mapper = new MapperWithField();
        mapper.Dispose();
        mapper.Dispose(); // Must not throw on second call.
    }
}
