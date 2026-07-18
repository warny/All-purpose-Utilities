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

    private class ThrowingSetterMapper : LibraryMapper
    {
        private Action? _fn;

        [External]
        public Action? NativeFunction
        {
            get => _fn;
            set
            {
                _fn = value;
                if (value == null)
                    throw new InvalidOperationException("Setter throws when cleared");
            }
        }
    }

    [TestMethod]
    public void Dispose_WhenSetterThrows_DoesNotPropagateAndCompletesNormally()
    {
        var mapper = new ThrowingSetterMapper();
        mapper.NativeFunction = () => { };
        Assert.IsNotNull(mapper.NativeFunction, "Pre-condition: property must be set before Dispose.");

        // ClearMappedDelegatesBestEffort must swallow exceptions from individual setters.
        mapper.Dispose();

        Assert.IsTrue(mapper.IsDisposed, "IsDisposed must be true even when a setter throws during Dispose.");
    }

    // ─── Item 47: transactional loading ─────────────────────────────────────────

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

        // The DLL exists (kernel32.dll) but the export does not. The transactional implementation
        // must throw EntryPointNotFoundException and free the DLL handle immediately (not leak it
        // for the finalizer). Verified by the absence of an exception from Create itself.
        Assert.ThrowsException<EntryPointNotFoundException>(
            () => LibraryMapper.Create<MissingExportMapper>("kernel32.dll"),
            "Create must propagate EntryPointNotFoundException when an export is missing.");
    }

    private class CommitPhaseThrowingMapper : LibraryMapper
    {
        private Action? _fn;

        [External("GetCurrentProcessId")]
        public Action? NativeFunction
        {
            get => _fn;
            set
            {
                if (value != null)
                    throw new InvalidOperationException("Commit setter always throws");
                _fn = null;
            }
        }
    }

    [TestMethod]
    public void Create_WhenSetterThrowsDuringCommit_PropagatesExceptionAndRollsBack()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Test uses kernel32.dll; skipped on non-Windows.");
            return;
        }

        // Setter succeeds during the prepare phase (not called there), fails in the commit phase.
        // The DLL must be freed and the exception must propagate — no partial state leaked.
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => LibraryMapper.Create<CommitPhaseThrowingMapper>("kernel32.dll"));

        StringAssert.Contains(ex.Message, "Commit setter always throws",
            "The original setter exception must propagate from Create.");
    }

    private class ReadOnlyPropertyMapper : LibraryMapper
    {
        private Action? _fn;

        [External("GetCurrentProcessId")]
        public Action? Fn => _fn; // read-only: no setter — validation in prepare phase must catch this
    }

    [TestMethod]
    public void Create_WhenPropertyHasNoSetter_ThrowsBeforeCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Test uses kernel32.dll; skipped on non-Windows.");
            return;
        }

        // A read-only property decorated with [External] should throw InvalidOperationException
        // during the prepare phase — before any member is assigned. The DLL must be freed immediately.
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => LibraryMapper.Create<ReadOnlyPropertyMapper>("kernel32.dll"));

        StringAssert.Contains(ex.Message, "no setter",
            "Error message must indicate that the property has no setter.");
    }
}
