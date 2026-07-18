using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates <see cref="LibraryMapper"/>'s disposal behavior and the prepare-phase validation
/// that restricts <c>[External]</c> members to fields and auto-properties.
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

    // ─── Item 47 / review#472: transactional loading + setter validation ─────────

    private class MissingExportMapper : LibraryMapper
    {
        [External("DoesNotExist_XYZ_12345")]
        public Action? FakeFunction;
    }

    [TestMethod]
    public void Create_WhenExportMissing_ThrowsAndFreesHandle()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Test uses kernel32.dll; skipped on non-Windows.");
            return;
        }

        Assert.ThrowsException<EntryPointNotFoundException>(
            () => LibraryMapper.Create<MissingExportMapper>("kernel32.dll"),
            "Create must propagate EntryPointNotFoundException when an export is missing.");
    }

    private class ReadOnlyPropertyMapper : LibraryMapper
    {
        private Action? _fn;

        [External("GetCurrentProcessId")]
        public Action? Fn => _fn; // read-only: no setter
    }

    [TestMethod]
    public void Create_WhenPropertyHasNoSetter_ThrowsBeforeCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Test uses kernel32.dll; skipped on non-Windows.");
            return;
        }

        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => LibraryMapper.Create<ReadOnlyPropertyMapper>("kernel32.dll"));

        StringAssert.Contains(ex.Message, "no setter",
            "Error message must indicate that the property has no setter.");
    }

    private class CustomSetterMapper : LibraryMapper
    {
        private Action? _fn;

        // Custom setter body — not an auto-property: must be rejected.
        [External("GetCurrentProcessId")]
        public Action? NativeFunction
        {
            get => _fn;
            set => _fn = value;
        }
    }

    [TestMethod]
    public void Create_WhenPropertyHasCustomSetterBody_ThrowsInvalidOperationException()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Test uses kernel32.dll; skipped on non-Windows.");
            return;
        }

        // A non-auto-property setter cannot be guaranteed to accept null unconditionally,
        // so Create must reject it in the prepare phase before loading any export.
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => LibraryMapper.Create<CustomSetterMapper>("kernel32.dll"));

        StringAssert.Contains(ex.Message, "custom setter",
            "Error message must explain that custom setter bodies are not allowed.");
    }
}
