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
        Assert.ThrowsException<ObjectDisposedException>(() => proc.Mappings.ToList());
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
}
