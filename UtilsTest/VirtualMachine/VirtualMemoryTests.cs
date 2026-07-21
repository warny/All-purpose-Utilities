using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine;

[TestClass]
public class VirtualMemoryTests
{
    // ───────────────────────────── AllocatePage / MasterProcess ─────────────────────────────

    [TestMethod]
    public void AllocatePage_ReturnsNewPage()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        Assert.IsNotNull(page);
        Assert.AreEqual(16, page.Size);
    }

    [TestMethod]
    public void AllocatePage_AddsToPages()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        Assert.AreEqual(0, mem.Pages.Count);
        mem.AllocatePage();
        mem.AllocatePage();
        Assert.AreEqual(2, mem.Pages.Count);
    }

    [TestMethod]
    public void AllocatePage_MasterAutoMapped_ReadWrite()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var mapping = mem.MasterProcess.Mappings.Single();
        Assert.AreSame(page, mapping.Page);
        Assert.AreEqual(PageAccess.ReadWrite, mapping.Access);
    }

    [TestMethod]
    public void AllocatePage_MasterMapsAtIncreasingIndices()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        mem.AllocatePage();
        mem.AllocatePage();
        var indices = mem.MasterProcess.Mappings.Select(m => m.VirtualPageIndex).OrderBy(i => i).ToArray();
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, indices);
    }

    // ─────────────────────────────── CreateProcess ───────────────────────────────────────────

    [TestMethod]
    public void CreateProcess_AddedToProcesses()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        Assert.IsTrue(mem.Processes.Contains(proc));
    }

    [TestMethod]
    public void CreateProcess_IsNotMaster()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        Assert.IsFalse(proc.IsMaster);
    }

    [TestMethod]
    public void CreateProcess_EmptyMappings()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        Assert.AreEqual(0, proc.Mappings.Count());
    }

    // ─────────────────────────────── MapPage / rights ────────────────────────────────────────

    [TestMethod]
    public void MapPage_TwoProcesses_DifferentRights()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page, 0, PageAccess.ReadOnly);
        Assert.AreEqual(PageAccess.ReadWrite, mem.MasterProcess.GetAccess(0));
        Assert.AreEqual(PageAccess.ReadOnly, proc.GetAccess(0));
    }

    [TestMethod]
    public void MapPage_PageNotOwned_Throws()
    {
        var mem1 = new VirtualMemory<int>(pageSize: 16);
        var mem2 = new VirtualMemory<int>(pageSize: 16);
        var foreignPage = mem2.AllocatePage();
        var proc = mem1.CreateProcess();
        Assert.ThrowsException<ArgumentException>(() => mem1.MapPage(proc, foreignPage, 0, PageAccess.ReadWrite));
    }

    [TestMethod]
    public void MapPage_NullProcess_Throws()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        Assert.ThrowsException<ArgumentNullException>(() => mem.MapPage(null!, page, 0, PageAccess.ReadWrite));
    }

    [TestMethod]
    public void MapPage_NullPage_Throws()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        Assert.ThrowsException<ArgumentNullException>(() => mem.MapPage(proc, null!, 0, PageAccess.ReadWrite));
    }

    [TestMethod]
    public void MapPage_ProcessFromAnotherMemory_ThrowsArgumentException()
    {
        var memA = new VirtualMemory<int>(pageSize: 16);
        var memB = new VirtualMemory<int>(pageSize: 16);
        var pageA = memA.AllocatePage();
        var procB = memB.CreateProcess();
        Assert.ThrowsException<ArgumentException>(() => memA.MapPage(procB, pageA, 0, PageAccess.ReadWrite));
    }

    [TestMethod]
    public void UnmapPage_ProcessFromAnotherMemory_ThrowsArgumentException()
    {
        var memA = new VirtualMemory<int>(pageSize: 16);
        var memB = new VirtualMemory<int>(pageSize: 16);
        var procB = memB.CreateProcess();
        Assert.ThrowsException<ArgumentException>(() => memA.UnmapPage(procB, 0));
    }

    // ───────────────────────────────────── Read ──────────────────────────────────────────────

    [TestMethod]
    public void Read_SinglePage_CorrectBytes()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        // Write via public API, then read back
        mem.MasterProcess.Write(2, new byte[] { 0xAB, 0xCD });
        var buf = new byte[2];
        mem.MasterProcess.Read(2, buf);
        Assert.AreEqual(0xAB, buf[0]);
        Assert.AreEqual(0xCD, buf[1]);
    }

    [TestMethod]
    public void Read_CrossPage_TransparentCopy()
    {
        // pageSize=4 forces cross-page with a 6-byte read starting at offset 2 in page 0
        var mem = new VirtualMemory<int>(pageSize: 4);
        mem.AllocatePage();
        mem.AllocatePage();
        // Virtual address 0..3 = page 0, 4..7 = page 1
        mem.MasterProcess.Write(2, new byte[] { 1, 2, 3, 4, 5, 6 });
        var buf = new byte[6];
        mem.MasterProcess.Read(2, buf);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6 }, buf);
    }

    [TestMethod]
    public void Read_UnmappedAddress_ThrowsMemoryAccessException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var buf = new byte[1];
        Assert.ThrowsException<MemoryAccessException>(() => mem.MasterProcess.Read(0, buf));
    }

    // ── Item 7: negative addresses rejected ───────────────────────────────────────────────────

    [TestMethod]
    public void Read_NegativeAddress_ThrowsMemoryAccessException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        var buf = new byte[1];
        Assert.ThrowsException<MemoryAccessException>(() => mem.MasterProcess.Read(-1, buf));
    }

    [TestMethod]
    public void Write_NegativeAddress_ThrowsMemoryAccessException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        Assert.ThrowsException<MemoryAccessException>(() => mem.MasterProcess.Write(-1, new byte[] { 0 }));
    }

    [TestMethod]
    public void IsAccessible_NegativeAddress_ThrowsMemoryAccessException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        Assert.ThrowsException<MemoryAccessException>(() => mem.MasterProcess.IsAccessible(-1));
    }

    [TestMethod]
    public void GetAccess_NegativeAddress_ThrowsMemoryAccessException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        Assert.ThrowsException<MemoryAccessException>(() => mem.MasterProcess.GetAccess(-1));
    }

    [TestMethod]
    public void Read_NegativeAddress_ZeroLength_ThrowsMemoryAccessException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        Assert.ThrowsException<MemoryAccessException>(
            () => mem.MasterProcess.Read(-1, Span<byte>.Empty));
    }

    [TestMethod]
    public void Write_NegativeAddress_ZeroLength_ThrowsMemoryAccessException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        Assert.ThrowsException<MemoryAccessException>(
            () => mem.MasterProcess.Write(-1, ReadOnlySpan<byte>.Empty));
    }

    // ── Item 22: VirtualAddressText is lossless for wide address types ────────────────────────

    [TestMethod]
    public void MemoryAccessException_VirtualAddressText_ContainsHexAddress()
    {
        var mem = new VirtualMemory<uint>(pageSize: 16);
        var buf = new byte[1];
        var ex = Assert.ThrowsException<MemoryAccessException>(() => mem.MasterProcess.Read(0xDEADBEEFu, buf));
        StringAssert.Contains(ex.VirtualAddressText, "DEADBEEF");
    }

    // ── Items 9 + 23: cross-page read/write atomicity ─────────────────────────────────────────

    [TestMethod]
    public void Write_CrossPage_SecondPageReadOnly_NoByteWritten()
    {
        // pageSize=4: addr 0-3 = page 0 (ReadWrite), addr 4-7 = page 1 (ReadOnly).
        // Writing 5 bytes starting at addr 2 touches both pages. The write must fail
        // without modifying the first page.
        var mem = new VirtualMemory<int>(pageSize: 4);
        var page0 = mem.AllocatePage();
        var page1 = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page0, 0, PageAccess.ReadWrite);
        mem.MapPage(proc, page1, 1, PageAccess.ReadOnly);

        // Write sentinel to page0 so we can detect mutation.
        mem.MasterProcess.Write(0, new byte[] { 0xAA, 0xAA, 0xAA, 0xAA });

        Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Write(2, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }));

        // Page 0 must be unchanged.
        var buf = new byte[4];
        mem.MasterProcess.Read(0, buf);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xAA, 0xAA, 0xAA }, buf,
            "Write atomicity violated: first page was partially modified before the fault.");
    }

    [TestMethod]
    public void Read_CrossPage_SecondPageUnmapped_DestinationUnchanged()
    {
        // pageSize=4: addr 0-3 = page 0 mapped, addr 4-7 = unmapped.
        // Reading 5 bytes starting at addr 2 spans the boundary. The read must fail
        // without modifying the destination buffer.
        var mem = new VirtualMemory<int>(pageSize: 4);
        var page0 = mem.AllocatePage(); // mapped into process at virtual index 0
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page0, 0, PageAccess.ReadOnly);
        mem.MasterProcess.Write(0, new byte[] { 0x11, 0x22, 0x33, 0x44 });

        byte[] destination = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xFF };

        Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Read(2, destination.AsSpan()));

        // Destination buffer must be unchanged.
        CollectionAssert.AreEqual(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xFF }, destination,
            "Read atomicity violated: destination buffer was modified before the fault.");
    }

    // ───────────────────────────────────── Write ─────────────────────────────────────────────

    [TestMethod]
    public void Write_SinglePage_DataPersists()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        mem.MasterProcess.Write(5, new byte[] { 0x11, 0x22 });
        var buf = new byte[2];
        mem.MasterProcess.Read(5, buf);
        Assert.AreEqual(0x11, buf[0]);
        Assert.AreEqual(0x22, buf[1]);
    }

    [TestMethod]
    public void Write_CrossPage_TransparentCopy()
    {
        var mem = new VirtualMemory<int>(pageSize: 4);
        mem.AllocatePage();
        mem.AllocatePage();
        // byte at virtual addr 3 goes into page 0, byte at addr 4 goes into page 1
        mem.MasterProcess.Write(3, new byte[] { 0xAA, 0xBB });
        var buf = new byte[2];
        mem.MasterProcess.Read(3, buf);
        Assert.AreEqual(0xAA, buf[0]);
        Assert.AreEqual(0xBB, buf[1]);
    }

    [TestMethod]
    public void Write_ReadOnlyPage_ThrowsMemoryAccessException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page, 0, PageAccess.ReadOnly);
        var ex = Assert.ThrowsException<MemoryAccessException>(() => proc.Write(0, new byte[] { 1 }));
        Assert.AreEqual(PageAccess.ReadWrite, ex.RequestedAccess);
        Assert.AreEqual(PageAccess.ReadOnly, ex.ActualAccess);
    }

    [TestMethod]
    public void Write_UnmappedAddress_ThrowsMemoryAccessException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        var ex = Assert.ThrowsException<MemoryAccessException>(() => proc.Write(0, new byte[] { 1 }));
        Assert.IsNull(ex.ActualAccess);
    }

    // ──────────────────────────────────── VirtualPage ────────────────────────────────────────

    [TestMethod]
    public void VirtualPage_AsReadOnlyMemory_LengthEqualsPageSize()
    {
        var mem = new VirtualMemory<int>(pageSize: 32);
        var page = mem.AllocatePage();
        Assert.AreEqual(32, page.AsReadOnlyMemory().Length);
    }

    [TestMethod]
    public void VirtualPage_AsReadOnlyMemory_UsableAsContextData()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var ctx = new DefaultContext(page.AsReadOnlyMemory());
        Assert.AreEqual(16, ctx.Data.Length);
    }

    [TestMethod]
    public void VirtualPage_Write_ReflectedInAsReadOnlyMemory()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        mem.MasterProcess.Write(0, new byte[] { 0xFF });
        Assert.AreEqual(0xFF, page.AsReadOnlyMemory().Span[0]);
    }

    // ──────────────────────────────── VirtualMemoryContext ───────────────────────────────────

    [TestMethod]
    public void VirtualMemoryContext_ExternalBuffer_CtorSetsProcess()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.MasterProcess;
        var ctx = new VirtualMemoryContext<int>(new byte[] { 0x01, 0x02 }, proc);
        Assert.AreSame(proc, ctx.Process);
        Assert.AreEqual(2, ctx.Data.Length);
    }

    [TestMethod]
    public void VirtualMemoryContext_PageCtor_DataIsPageContent()
    {
        var mem = new VirtualMemory<int>(pageSize: 8);
        var page = mem.AllocatePage();
        mem.MasterProcess.Write(0, new byte[] { 0xDE });
        var ctx = new VirtualMemoryContext<int>(page, mem.MasterProcess);
        Assert.AreEqual(8, ctx.Data.Length);
        Assert.AreEqual(0xDE, ctx.Data.Span[0]);
    }

    [TestMethod]
    public void VirtualMemoryContext_PageCtor_NullPage_Throws()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        Assert.ThrowsException<ArgumentNullException>(() =>
            new VirtualMemoryContext<int>((VirtualPage)null!, mem.MasterProcess));
    }

    [TestMethod]
    public void VirtualMemoryContext_NullProcess_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new VirtualMemoryContext<int>(ReadOnlyMemory<byte>.Empty, null!));
    }

    [TestMethod]
    public void VirtualMemoryContext_FreedProcess_ThrowsObjectDisposedException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        mem.FreeProcess(proc);
        Assert.ThrowsException<ObjectDisposedException>(() =>
            new VirtualMemoryContext<int>(ReadOnlyMemory<byte>.Empty, proc));
    }

    [TestMethod]
    public void VirtualMemoryContext_PageCtor_UnmappedPage_ThrowsArgumentException()
    {
        var mem1 = new VirtualMemory<int>(pageSize: 16);
        var mem2 = new VirtualMemory<int>(pageSize: 16);
        var page = mem1.AllocatePage();
        // page belongs to mem1 but proc belongs to mem2 — not mapped
        var proc = mem2.MasterProcess;
        Assert.ThrowsException<ArgumentException>(() =>
            new VirtualMemoryContext<int>(page, proc));
    }

    [TestMethod]
    public void VirtualMemoryContext_PageCtor_FreedProcess_ThrowsObjectDisposedException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.FreeProcess(proc);
        Assert.ThrowsException<ObjectDisposedException>(() =>
            new VirtualMemoryContext<int>(page, proc));
    }

    // ──────────────────────────────── IsAccessible / GetAccess ───────────────────────────────

    [TestMethod]
    public void IsAccessible_Mapped_ReturnsTrue()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        Assert.IsTrue(mem.MasterProcess.IsAccessible(0));
        Assert.IsTrue(mem.MasterProcess.IsAccessible(15));
    }

    [TestMethod]
    public void IsAccessible_Unmapped_ReturnsFalse()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        Assert.IsFalse(mem.MasterProcess.IsAccessible(0));
    }

    [TestMethod]
    public void GetAccess_MappedReadOnly()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page, 0, PageAccess.ReadOnly);
        Assert.AreEqual(PageAccess.ReadOnly, proc.GetAccess(0));
    }

    [TestMethod]
    public void GetAccess_Unmapped_ReturnsNull()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        Assert.IsNull(mem.MasterProcess.GetAccess(0));
    }

    // ─────────────────────────────── FreeProcess ────────────────────────────────

    [TestMethod]
    public void FreeProcess_RemovesProcessFromList()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        Assert.AreEqual(2, mem.Processes.Count); // master + proc
        mem.FreeProcess(proc);
        Assert.AreEqual(1, mem.Processes.Count);
        Assert.IsFalse(mem.Processes.Contains(proc));
    }

    [TestMethod]
    public void FreeProcess_RemovesAllMappings()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page0 = mem.AllocatePage();
        var page1 = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page0, 0, PageAccess.ReadOnly);
        mem.MapPage(proc, page1, 1, PageAccess.ReadWrite);
        Assert.AreEqual(2, proc.Mappings.Count());
        mem.FreeProcess(proc);
        // After free, Mappings is inaccessible; verify via IsFreed instead.
        Assert.IsTrue(proc.IsFreed);
    }

    [TestMethod]
    public void FreeProcess_DoesNotAffectOtherProcesses()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var proc1 = mem.CreateProcess();
        var proc2 = mem.CreateProcess();
        mem.MapPage(proc1, page, 0, PageAccess.ReadOnly);
        mem.MapPage(proc2, page, 0, PageAccess.ReadWrite);
        mem.FreeProcess(proc1);
        Assert.AreEqual(PageAccess.ReadWrite, proc2.GetAccess(0));
    }

    [TestMethod]
    public void FreeProcess_MasterProcess_Throws()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        Assert.ThrowsException<ArgumentException>(() => mem.FreeProcess(mem.MasterProcess));
    }

    [TestMethod]
    public void FreeProcess_ProcessNotBelongingToInstance_Throws()
    {
        var mem1 = new VirtualMemory<int>(pageSize: 16);
        var mem2 = new VirtualMemory<int>(pageSize: 16);
        var proc = mem2.CreateProcess();
        Assert.ThrowsException<ArgumentException>(() => mem1.FreeProcess(proc));
    }

    [TestMethod]
    public void FreeProcess_Null_Throws()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        Assert.ThrowsException<ArgumentNullException>(() => mem.FreeProcess(null!));
    }

    // ── Item 25: operations on a freed process throw ObjectDisposedException ──

    [TestMethod]
    public void FreeProcess_SetsIsFreed()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        Assert.IsFalse(proc.IsFreed);
        mem.FreeProcess(proc);
        Assert.IsTrue(proc.IsFreed);
    }

    [TestMethod]
    public void FreeProcess_Read_AfterFree_Throws()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        mem.FreeProcess(proc);
        Assert.ThrowsException<ObjectDisposedException>(() => proc.Read(0, new byte[1]));
    }

    [TestMethod]
    public void FreeProcess_Write_AfterFree_Throws()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        mem.FreeProcess(proc);
        Assert.ThrowsException<ObjectDisposedException>(() => proc.Write(0, new byte[1]));
    }

    [TestMethod]
    public void FreeProcess_IsAccessible_AfterFree_Throws()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        mem.FreeProcess(proc);
        Assert.ThrowsException<ObjectDisposedException>(() => proc.IsAccessible(0));
    }

    [TestMethod]
    public void FreeProcess_GetAccess_AfterFree_Throws()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        mem.FreeProcess(proc);
        Assert.ThrowsException<ObjectDisposedException>(() => proc.GetAccess(0));
    }

    [TestMethod]
    public void FreeProcess_Mappings_AfterFree_Throws()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var proc = mem.CreateProcess();
        mem.FreeProcess(proc);
        Assert.ThrowsException<ObjectDisposedException>(() => proc.Mappings);
    }

    [TestMethod]
    public void Mappings_SnapshotCapturedBeforeFree_RetainsContentAfterFree()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page, 42, PageAccess.ReadOnly);

        var snapshot = proc.Mappings;

        mem.FreeProcess(proc);

        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual(42, snapshot[0].VirtualPageIndex);
        Assert.AreEqual(PageAccess.ReadOnly, snapshot[0].Access);
    }

    [TestMethod]
    public void MapPage_FreedProcess_ThrowsObjectDisposedException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.FreeProcess(proc);
        Assert.ThrowsException<ObjectDisposedException>(() => mem.MapPage(proc, page, 0, PageAccess.ReadWrite));
    }

    [TestMethod]
    public void UnmapPage_FreedProcess_ThrowsObjectDisposedException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.FreeProcess(proc);
        Assert.ThrowsException<ObjectDisposedException>(() => mem.UnmapPage(proc, 0));
    }

    // ── Item 33: FreePage — page lifecycle ────────────────────────────────────

    [TestMethod]
    public void FreePage_SetsIsFreed()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        Assert.IsFalse(page.IsFreed);
        mem.FreePage(page);
        Assert.IsTrue(page.IsFreed);
    }

    [TestMethod]
    public void FreePage_RemovesPageFromPages()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        mem.FreePage(page);
        Assert.AreEqual(0, mem.Pages.Count);
    }

    [TestMethod]
    public void FreePage_RemovesMasterMapping()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        mem.FreePage(page);
        Assert.AreEqual(0, mem.MasterProcess.Mappings.Count());
    }

    [TestMethod]
    public void FreePage_AlreadyFreed_ThrowsObjectDisposedException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        mem.FreePage(page);
        Assert.ThrowsException<ObjectDisposedException>(() => mem.FreePage(page));
    }

    [TestMethod]
    public void FreePage_NullPage_ThrowsArgumentNullException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        Assert.ThrowsException<ArgumentNullException>(() => mem.FreePage(null!));
    }

    [TestMethod]
    public void FreePage_ForeignPage_ThrowsArgumentException()
    {
        var mem1 = new VirtualMemory<int>(pageSize: 16);
        var mem2 = new VirtualMemory<int>(pageSize: 16);
        var page = mem2.AllocatePage();
        Assert.ThrowsException<ArgumentException>(() => mem1.FreePage(page));
    }

    [TestMethod]
    public void FreePage_ActiveNonMasterMapping_WithoutForce_ThrowsInvalidOperationException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page, 0, PageAccess.ReadOnly);
        Assert.ThrowsException<VmInvalidOperationException>(() => mem.FreePage(page));
    }

    [TestMethod]
    public void FreePage_ActiveNonMasterMapping_WithForce_RemovesAllMappings()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page, 0, PageAccess.ReadOnly);
        mem.FreePage(page, force: true);
        Assert.IsTrue(page.IsFreed);
        Assert.IsFalse(proc.IsAccessible(0));
    }

    [TestMethod]
    public void AsReadOnlyMemory_FreedPage_ThrowsObjectDisposedException()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        mem.FreePage(page);
        Assert.ThrowsException<ObjectDisposedException>(() => page.AsReadOnlyMemory());
    }

    [TestMethod]
    public void FreePage_NoNonMasterMappings_SucceedsWithoutForce()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var page = mem.AllocatePage();
        var _ = mem.CreateProcess(); // process exists but no mapping to the page
        mem.FreePage(page); // must not throw
        Assert.IsTrue(page.IsFreed);
    }

    // ── Item 9: cross-page atomicity and address-space overflow ──────────────────────────────────

    // ── Write atomicity: multi-page faults leave all prior pages unchanged ───

    [TestMethod]
    public void Write_SecondPageUnmapped_FirstPageUnchanged()
    {
        // page0 mapped ReadWrite, no page1 mapping; write spans boundary.
        var mem = new VirtualMemory<int>(pageSize: 4);
        var page0 = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page0, 0, PageAccess.ReadWrite);
        mem.MasterProcess.Write(0, new byte[] { 0xAA, 0xAA, 0xAA, 0xAA });

        Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Write(2, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }));

        var buf = new byte[4];
        proc.Read(0, buf);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xAA, 0xAA, 0xAA }, buf,
            "First page must be unchanged after fault on unmapped second page.");
    }

    [TestMethod]
    public void Write_ThirdPageReadOnly_FirstTwoPagesUnchanged()
    {
        // page0+page1 ReadWrite, page2 ReadOnly; write spans all three.
        var mem = new VirtualMemory<int>(pageSize: 4);
        var page0 = mem.AllocatePage();
        var page1 = mem.AllocatePage();
        var page2 = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page0, 0, PageAccess.ReadWrite);
        mem.MapPage(proc, page1, 1, PageAccess.ReadWrite);
        mem.MapPage(proc, page2, 2, PageAccess.ReadOnly);

        mem.MasterProcess.Write(0, new byte[] { 0x11, 0x11, 0x11, 0x11 });
        mem.MasterProcess.Write(4, new byte[] { 0x22, 0x22, 0x22, 0x22 });

        Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Write(0, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }));

        var buf0 = new byte[4];
        var buf1 = new byte[4];
        proc.Read(0, buf0);
        proc.Read(4, buf1);
        CollectionAssert.AreEqual(new byte[] { 0x11, 0x11, 0x11, 0x11 }, buf0,
            "Page 0 must be unchanged.");
        CollectionAssert.AreEqual(new byte[] { 0x22, 0x22, 0x22, 0x22 }, buf1,
            "Page 1 must be unchanged.");
    }

    [TestMethod]
    public void Write_ThirdPageUnmapped_FirstTwoPagesUnchanged()
    {
        // page0+page1 ReadWrite, no page2; write spans all three.
        var mem = new VirtualMemory<int>(pageSize: 4);
        var page0 = mem.AllocatePage();
        var page1 = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page0, 0, PageAccess.ReadWrite);
        mem.MapPage(proc, page1, 1, PageAccess.ReadWrite);

        mem.MasterProcess.Write(0, new byte[] { 0x11, 0x11, 0x11, 0x11 });
        mem.MasterProcess.Write(4, new byte[] { 0x22, 0x22, 0x22, 0x22 });

        Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Write(0, new byte[9]));

        var buf0 = new byte[4];
        var buf1 = new byte[4];
        proc.Read(0, buf0);
        proc.Read(4, buf1);
        CollectionAssert.AreEqual(new byte[] { 0x11, 0x11, 0x11, 0x11 }, buf0,
            "Page 0 must be unchanged.");
        CollectionAssert.AreEqual(new byte[] { 0x22, 0x22, 0x22, 0x22 }, buf1,
            "Page 1 must be unchanged.");
    }

    [TestMethod]
    public void Write_ThreePages_AllBytesWrittenCorrectly()
    {
        // All three pages ReadWrite; exact 12-byte write across all three.
        var mem = new VirtualMemory<int>(pageSize: 4);
        mem.AllocatePage();
        mem.AllocatePage();
        mem.AllocatePage();

        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        mem.MasterProcess.Write(0, data);

        var buf = new byte[12];
        mem.MasterProcess.Read(0, buf);
        CollectionAssert.AreEqual(data, buf,
            "All 12 bytes must be transferred across three pages.");
    }

    // ── Write: unsigned address wrap-around must be rejected ────────────────

    [TestMethod]
    public void Write_UintAddress_OverflowNearMaxValue_NoPagesModified()
    {
        // pageSize=4; last virtual page index = uint.MaxValue / 4 = 1073741823.
        // Write starting at uint.MaxValue - 1 (offset 2 of last page) with length 4
        // would need address uint.MaxValue + 2 — overflow.  Neither the last page
        // nor page-zero may be modified.
        const uint lastPageIdx = uint.MaxValue / 4u;  // 1073741823

        var mem = new VirtualMemory<uint>(pageSize: 4);
        var lastPage = mem.AllocatePage(); // master at virtual index 0u
        var pageZero = mem.AllocatePage(); // master at virtual index 1u
        var proc = mem.CreateProcess();
        mem.MapPage(proc, lastPage, lastPageIdx, PageAccess.ReadWrite);
        mem.MapPage(proc, pageZero, 0u,          PageAccess.ReadWrite);

        // Write sentinels via master (lastPage at master byte 0; pageZero at master byte 4).
        mem.MasterProcess.Write(0u, new byte[] { 0xAA, 0xAA, 0xAA, 0xAA });
        mem.MasterProcess.Write(4u, new byte[] { 0xBB, 0xBB, 0xBB, 0xBB });

        uint startAddr = uint.MaxValue - 1u; // 0xFFFFFFFE, offset 2 in last page
        var ex = Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Write(startAddr, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));

        var bufLast = new byte[4];
        var bufZero = new byte[4];
        mem.MasterProcess.Read(0u, bufLast);
        mem.MasterProcess.Read(4u, bufZero);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xAA, 0xAA, 0xAA }, bufLast,
            "Last page must not be modified.");
        CollectionAssert.AreEqual(new byte[] { 0xBB, 0xBB, 0xBB, 0xBB }, bufZero,
            "Page zero must not be treated as address-space continuation.");

        Assert.IsNull(ex.ActualAccess, "Range overflow must report ActualAccess null.");
        Assert.AreEqual(PageAccess.ReadWrite, ex.RequestedAccess);
        StringAssert.Contains(ex.VirtualAddressText.ToUpperInvariant(), "FFFFFFFE",
            "VirtualAddressText must report the start address, not a wrapped zero.");
    }

    [TestMethod]
    public void Write_UintAddress_RangeEndingAtMaxValue_Succeeds()
    {
        // Valid range: startAddr = uint.MaxValue - 2 (offset 1 in last page), length = 3.
        // End address = uint.MaxValue — exactly representable, must succeed.
        const uint lastPageIdx = uint.MaxValue / 4u; // 1073741823

        var mem = new VirtualMemory<uint>(pageSize: 4);
        var lastPage = mem.AllocatePage(); // master at virtual index 0u
        var proc = mem.CreateProcess();
        mem.MapPage(proc, lastPage, lastPageIdx, PageAccess.ReadWrite);

        uint startAddr = uint.MaxValue - 2u; // offset 1 in last page
        proc.Write(startAddr, new byte[] { 0x11, 0x22, 0x33 });

        var buf = new byte[3];
        proc.Read(startAddr, buf);
        CollectionAssert.AreEqual(new byte[] { 0x11, 0x22, 0x33 }, buf);
    }

    [TestMethod]
    public void Write_ByteAddress_WrapAround_Rejected()
    {
        // byte address space: 0..255, pageSize=4, last page index=63 (addresses 252..255).
        // Write at address 254 (offset 2 in page 63) with length 4 would reach address 257 > 255.
        var mem = new VirtualMemory<byte>(pageSize: 4);
        var lastPage = mem.AllocatePage(); // master at virtual index 0
        var pageZero = mem.AllocatePage(); // master at virtual index 1
        var proc = mem.CreateProcess();
        mem.MapPage(proc, lastPage, (byte)63, PageAccess.ReadWrite);
        mem.MapPage(proc, pageZero, (byte)0,  PageAccess.ReadWrite);

        mem.MasterProcess.Write((byte)0, new byte[] { 0xAA, 0xAA, 0xAA, 0xAA });
        mem.MasterProcess.Write((byte)4, new byte[] { 0xBB, 0xBB, 0xBB, 0xBB });

        Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Write((byte)254, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));

        var bufLast = new byte[4];
        var bufZero = new byte[4];
        mem.MasterProcess.Read((byte)0, bufLast);
        mem.MasterProcess.Read((byte)4, bufZero);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xAA, 0xAA, 0xAA }, bufLast,
            "Last page must not be modified.");
        CollectionAssert.AreEqual(new byte[] { 0xBB, 0xBB, 0xBB, 0xBB }, bufZero,
            "Page zero must not be treated as continuation.");
    }

    [TestMethod]
    public void Write_IntAddress_SignedOverflow_NoBytesWritten()
    {
        // startAddress = int.MaxValue - 1, length = 3: end = int.MaxValue + 1 → signed overflow.
        // No page near int.MaxValue is needed; the overflow check fires first.
        var mem = new VirtualMemory<int>(pageSize: 4);
        mem.AllocatePage();
        Assert.ThrowsException<MemoryAccessException>(() =>
            mem.MasterProcess.Write(int.MaxValue - 1, new byte[] { 0x01, 0x02, 0x03 }));
    }

    // ── Read atomicity: destination must be unchanged when plan fails ────────

    [TestMethod]
    public void Read_ThirdPageUnmapped_DestinationUnchanged()
    {
        var mem = new VirtualMemory<int>(pageSize: 4);
        var page0 = mem.AllocatePage();
        var page1 = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page0, 0, PageAccess.ReadOnly);
        mem.MapPage(proc, page1, 1, PageAccess.ReadOnly);
        mem.MasterProcess.Write(0, new byte[] { 0x11, 0x22, 0x33, 0x44 });
        mem.MasterProcess.Write(4, new byte[] { 0x55, 0x66, 0x77, 0x88 });

        byte[] dest = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xDE, 0xAD, 0xBE, 0xEF, 0xFF };

        Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Read(0, dest.AsSpan()));

        // Every sentinel must remain intact.
        CollectionAssert.AreEqual(
            new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xDE, 0xAD, 0xBE, 0xEF, 0xFF }, dest,
            "Destination must not be partially filled before the unmapped-page fault.");
    }

    [TestMethod]
    public void Read_UintAddress_OverflowNearMaxValue_DestinationUnchanged()
    {
        const uint lastPageIdx = uint.MaxValue / 4u;

        var mem = new VirtualMemory<uint>(pageSize: 4);
        var lastPage = mem.AllocatePage();
        var pageZero = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, lastPage, lastPageIdx, PageAccess.ReadOnly);
        mem.MapPage(proc, pageZero, 0u,          PageAccess.ReadOnly);

        byte[] dest = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Read(uint.MaxValue - 1u, dest.AsSpan()));

        CollectionAssert.AreEqual(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, dest,
            "Destination must be unchanged when read overflows the address space.");
    }

    [TestMethod]
    public void Read_UintAddress_RangeEndingAtMaxValue_Succeeds()
    {
        // startAddr = uint.MaxValue - 2 (offset 1 in last page), length 3: exactly valid.
        const uint lastPageIdx = uint.MaxValue / 4u;

        var mem = new VirtualMemory<uint>(pageSize: 4);
        var lastPage = mem.AllocatePage(); // master at virtual index 0u
        var proc = mem.CreateProcess();
        mem.MapPage(proc, lastPage, lastPageIdx, PageAccess.ReadOnly);

        // Write test pattern via master (lastPage is at master byte address 0..3).
        mem.MasterProcess.Write(0u, new byte[] { 0x11, 0x22, 0x33, 0x44 });

        uint startAddr = uint.MaxValue - 2u; // offset 1 in last page
        var buf = new byte[3];
        proc.Read(startAddr, buf);

        CollectionAssert.AreEqual(new byte[] { 0x22, 0x33, 0x44 }, buf);
    }

    [TestMethod]
    public void Read_ByteAddress_WrapAround_DestinationUnchanged()
    {
        var mem = new VirtualMemory<byte>(pageSize: 4);
        var lastPage = mem.AllocatePage();
        var pageZero = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, lastPage, (byte)63, PageAccess.ReadOnly);
        mem.MapPage(proc, pageZero, (byte)0,  PageAccess.ReadOnly);

        byte[] dest = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Read((byte)254, dest.AsSpan()));

        CollectionAssert.AreEqual(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, dest,
            "Destination must be unchanged when read wraps the byte address space.");
    }

    [TestMethod]
    public void Read_IntAddress_SignedOverflow_DestinationUnchanged()
    {
        var mem = new VirtualMemory<int>(pageSize: 4);
        mem.AllocatePage();

        byte[] dest = new byte[] { 0xDE, 0xAD, 0xBE };

        Assert.ThrowsException<MemoryAccessException>(() =>
            mem.MasterProcess.Read(int.MaxValue - 1, dest.AsSpan()));

        CollectionAssert.AreEqual(new byte[] { 0xDE, 0xAD, 0xBE }, dest,
            "Destination must be unchanged on signed overflow.");
    }

    // ── Zero-length operations succeed without a mapping ─────────────────────

    [TestMethod]
    public void Read_ZeroLength_NonNegativeUnmappedAddress_Succeeds()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        // Address 42 is not mapped.
        mem.MasterProcess.Read(42, Span<byte>.Empty); // must not throw
    }

    [TestMethod]
    public void Write_ZeroLength_NonNegativeUnmappedAddress_Succeeds()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        mem.MasterProcess.Write(42, ReadOnlySpan<byte>.Empty); // must not throw
    }

    // ── Diagnostics ──────────────────────────────────────────────────────────

    [TestMethod]
    public void Read_UnmappedAddress_ActualAccess_IsNull()
    {
        var mem = new VirtualMemory<int>(pageSize: 16);
        var ex = Assert.ThrowsException<MemoryAccessException>(
            () => mem.MasterProcess.Read(0, new byte[1]));
        Assert.IsNull(ex.ActualAccess);
        Assert.AreEqual(PageAccess.ReadOnly, ex.RequestedAccess);
    }

    [TestMethod]
    public void Write_CrossPage_ReadOnlySecondPage_RequestedAndActualAccess_Correct()
    {
        var mem = new VirtualMemory<int>(pageSize: 4);
        var page0 = mem.AllocatePage();
        var page1 = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, page0, 0, PageAccess.ReadWrite);
        mem.MapPage(proc, page1, 1, PageAccess.ReadOnly);

        var ex = Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Write(2, new byte[] { 1, 2, 3, 4, 5 }));

        Assert.AreEqual(PageAccess.ReadWrite, ex.RequestedAccess);
        Assert.AreEqual(PageAccess.ReadOnly,  ex.ActualAccess);
    }

    [TestMethod]
    public void Write_UintAddress_OverflowNearMaxValue_VirtualAddressTextIsStartAddress()
    {
        const uint lastPageIdx = uint.MaxValue / 4u;

        var mem = new VirtualMemory<uint>(pageSize: 4);
        var lastPage = mem.AllocatePage();
        var proc = mem.CreateProcess();
        mem.MapPage(proc, lastPage, lastPageIdx, PageAccess.ReadWrite);

        var ex = Assert.ThrowsException<MemoryAccessException>(() =>
            proc.Write(uint.MaxValue - 1u, new byte[] { 0x01, 0x02, 0x03, 0x04 }));

        // Must report the start address (0xFFFFFFFE), not wrapped zero.
        StringAssert.Contains(ex.VirtualAddressText.ToUpperInvariant(), "FFFFFFFE",
            "VirtualAddressText must not show wrapped address zero.");
        Assert.IsNull(ex.ActualAccess, "Range overflow must report ActualAccess null.");
    }

    // ── Plan consistency: bytes copied in address order without gaps ──────────

    [TestMethod]
    public void Read_MultiPage_BytesCopiedExactlyOnceInOrder()
    {
        var mem = new VirtualMemory<int>(pageSize: 4);
        mem.AllocatePage();
        mem.AllocatePage();
        mem.AllocatePage();

        var source = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        mem.MasterProcess.Write(0, source);

        var dest = new byte[12];
        mem.MasterProcess.Read(0, dest);

        CollectionAssert.AreEqual(source, dest,
            "All 12 bytes must be copied in address order across three pages.");
    }

    // ── Pre-allocation safety: unmapped range must throw MAE, not OOM ────────

    [TestMethod]
    public void Read_LargeUnmappedRange_PageSize1_ThrowsMemoryAccessException()
    {
        // With pageSize=1, the theoretical upper bound on segments is length.
        // Pre-allocating that many entries before the first mapping check could throw
        // OutOfMemoryException instead of MemoryAccessException.
        // This test verifies the contract: an invalid range produces MemoryAccessException
        // regardless of how large the requested length is.
        var mem = new VirtualMemory<int>(pageSize: 1);
        // No pages allocated; address 0 is unmapped.
        var buf = new byte[1_000_000];
        Assert.ThrowsException<MemoryAccessException>(
            () => mem.MasterProcess.Read(0, buf),
            "A large read on an unmapped address must throw MemoryAccessException, not OOM.");
    }

    [TestMethod]
    public void Write_LargeUnmappedRange_PageSize1_ThrowsMemoryAccessException()
    {
        var mem = new VirtualMemory<int>(pageSize: 1);
        var buf = new byte[1_000_000];
        Assert.ThrowsException<MemoryAccessException>(
            () => mem.MasterProcess.Write(0, buf),
            "A large write on an unmapped address must throw MemoryAccessException, not OOM.");
    }
}
