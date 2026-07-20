using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine;

// ── Minimal processors for limits-policy tests ────────────────────────────────

/// <summary>
/// Processor with a HALT (0x00) and STEP (0x01) instruction.
/// STEP increments a counter via a closure captured at construction.
/// </summary>
public class LimitTestProcessor : VirtualProcessor<DefaultContext>
{
    public LimitTestProcessor()
    {
        RegisterInstruction([0x00], "HALT", ctx => ctx.Terminate());
        RegisterInstruction([0x01], "STEP", ctx => { /* no-op side effect */ });
    }
}

[TestClass]
public class VmLimitsPolicyTests
{
    // ── VirtualMachineLimits — default values ─────────────────────────────────

    [TestMethod]
    public void VirtualMachineLimits_Default_MatchesExpectedDefaults()
    {
        var limits = VirtualMachineLimits.Default;
        Assert.AreEqual(512, limits.MaxCallStackDepth);
        Assert.AreEqual(1024, limits.MaxControlFlowDepth);
        Assert.AreEqual(1024, limits.MaxOperandStackDepth);
        Assert.AreEqual(int.MaxValue, limits.MaxPhysicalPages);
        Assert.AreEqual(int.MaxValue, limits.MaxMemoryProcesses);
        Assert.AreEqual(int.MaxValue, limits.MaxScheduledProcesses);
        Assert.AreEqual(100, limits.SchedulerQuantumSteps);
    }

    [TestMethod]
    public void VirtualMachineLimits_Default_IsSameInstance()
    {
        Assert.AreSame(VirtualMachineLimits.Default, VirtualMachineLimits.Default);
    }

    [TestMethod]
    public void VirtualMachineLimits_Zero_MaxCallStackDepth_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new VirtualMachineLimits(maxCallStackDepth: 0));
    }

    [TestMethod]
    public void VirtualMachineLimits_Negative_MaxCallStackDepth_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new VirtualMachineLimits(maxCallStackDepth: -1));
    }

    [TestMethod]
    public void VirtualMachineLimits_Zero_MaxControlFlowDepth_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new VirtualMachineLimits(maxControlFlowDepth: 0));
    }

    [TestMethod]
    public void VirtualMachineLimits_Zero_MaxOperandStackDepth_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new VirtualMachineLimits(maxOperandStackDepth: 0));
    }

    [TestMethod]
    public void VirtualMachineLimits_Zero_MaxPhysicalPages_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new VirtualMachineLimits(maxPhysicalPages: 0));
    }

    [TestMethod]
    public void VirtualMachineLimits_Zero_MaxMemoryProcesses_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new VirtualMachineLimits(maxMemoryProcesses: 0));
    }

    [TestMethod]
    public void VirtualMachineLimits_Zero_MaxScheduledProcesses_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new VirtualMachineLimits(maxScheduledProcesses: 0));
    }

    [TestMethod]
    public void VirtualMachineLimits_Zero_SchedulerQuantumSteps_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new VirtualMachineLimits(schedulerQuantumSteps: 0));
    }

    [TestMethod]
    public void VirtualMachineLimits_One_AllParams_Succeeds()
    {
        var limits = new VirtualMachineLimits(
            maxCallStackDepth: 1,
            maxControlFlowDepth: 1,
            maxOperandStackDepth: 1,
            maxPhysicalPages: 1,
            maxMemoryProcesses: 1,
            maxScheduledProcesses: 1,
            schedulerQuantumSteps: 1);
        Assert.AreEqual(1, limits.MaxCallStackDepth);
        Assert.AreEqual(1, limits.MaxControlFlowDepth);
        Assert.AreEqual(1, limits.MaxOperandStackDepth);
        Assert.AreEqual(1, limits.MaxPhysicalPages);
        Assert.AreEqual(1, limits.MaxMemoryProcesses);
        Assert.AreEqual(1, limits.MaxScheduledProcesses);
        Assert.AreEqual(1, limits.SchedulerQuantumSteps);
    }

    // ── ExecutionLimits ────────────────────────────────────────────────────────

    [TestMethod]
    public void ExecutionLimits_Unlimited_HasNullMaxInstructions()
    {
        Assert.IsNull(ExecutionLimits.Unlimited.MaxInstructions);
    }

    [TestMethod]
    public void ExecutionLimits_Unlimited_IsSameInstance()
    {
        Assert.AreSame(ExecutionLimits.Unlimited, ExecutionLimits.Unlimited);
    }

    [TestMethod]
    public void ExecutionLimits_DefaultCtor_IsUnlimited()
    {
        var el = new ExecutionLimits();
        Assert.IsNull(el.MaxInstructions);
    }

    [TestMethod]
    public void ExecutionLimits_Zero_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ExecutionLimits(0));
    }

    [TestMethod]
    public void ExecutionLimits_Negative_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ExecutionLimits(-1));
    }

    [TestMethod]
    public void ExecutionLimits_One_IsValid()
    {
        var el = new ExecutionLimits(1);
        Assert.AreEqual(1L, el.MaxInstructions);
    }

    [TestMethod]
    public void ExecutionLimits_Large_IsValid()
    {
        var el = new ExecutionLimits(1_000_000L);
        Assert.AreEqual(1_000_000L, el.MaxInstructions);
    }

    // ── VmLimitExceededException ───────────────────────────────────────────────

    [TestMethod]
    public void VmLimitExceededException_Properties_AreSet()
    {
        var ex = new VmLimitExceededException(VmLimitKind.OperandStackDepth, 10L, 11L);
        Assert.AreEqual(VmLimitKind.OperandStackDepth, ex.LimitKind);
        Assert.AreEqual(10L, ex.Limit);
        Assert.AreEqual(11L, ex.AttemptedValue);
        Assert.IsFalse(string.IsNullOrWhiteSpace(ex.Message));
    }

    [TestMethod]
    public void InstructionBudgetExceededException_DerivesFromVmLimitExceededException()
    {
        var ex = new InstructionBudgetExceededException(100L);
        Assert.IsInstanceOfType<VmLimitExceededException>(ex);
        Assert.AreEqual(VmLimitKind.InstructionCount, ex.LimitKind);
        Assert.AreEqual(100L, ex.Limit);
        Assert.AreEqual(100L, ex.Budget); // alias
    }

    // ── BoundedStack overflow ─────────────────────────────────────────────────

    [TestMethod]
    public void BoundedStack_ExactLimit_Push_Succeeds()
    {
        var stack = new BoundedStack<int>(maxDepth: 3);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        Assert.AreEqual(3, stack.Count);
    }

    [TestMethod]
    public void BoundedStack_OnePastLimit_ThrowsVmLimitExceededException()
    {
        var stack = new BoundedStack<int>(maxDepth: 3);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        var ex = Assert.ThrowsException<VmLimitExceededException>(() => stack.Push(4));
        Assert.AreEqual(VmLimitKind.OperandStackDepth, ex.LimitKind);
        Assert.AreEqual(3L, ex.Limit);
        Assert.AreEqual(4L, ex.AttemptedValue);
    }

    [TestMethod]
    public void BoundedStack_FailedPush_DoesNotChangeCount()
    {
        var stack = new BoundedStack<int>(maxDepth: 2);
        stack.Push(1);
        stack.Push(2);
        try { stack.Push(3); } catch (VmLimitExceededException) { }
        Assert.AreEqual(2, stack.Count);
    }

    // ── CallStack / SimpleCallStack overflow ──────────────────────────────────

    [TestMethod]
    public void CallStack_ExactLimit_Succeeds()
    {
        var cs = new CallStack(maxDepth: 2);
        cs.Call(10);
        cs.Call(20);
        Assert.AreEqual(2, cs.Depth);
    }

    [TestMethod]
    public void CallStack_OnePastLimit_ThrowsVmLimitExceededException()
    {
        var cs = new CallStack(maxDepth: 2);
        cs.Call(10);
        cs.Call(20);
        var ex = Assert.ThrowsException<VmLimitExceededException>(() => cs.Call(30));
        Assert.AreEqual(VmLimitKind.CallStackDepth, ex.LimitKind);
        Assert.AreEqual(2L, ex.Limit);
        Assert.AreEqual(3L, ex.AttemptedValue);
    }

    [TestMethod]
    public void CallStack_FailedCall_DoesNotChangeDepth()
    {
        var cs = new CallStack(maxDepth: 1);
        cs.Call(1);
        try { cs.Call(2); } catch (VmLimitExceededException) { }
        Assert.AreEqual(1, cs.Depth);
    }

    [TestMethod]
    public void SimpleCallStack_OnePastLimit_ThrowsVmLimitExceededException()
    {
        var cs = new SimpleCallStack(maxDepth: 2);
        cs.Call(10);
        cs.Call(20);
        var ex = Assert.ThrowsException<VmLimitExceededException>(() => cs.Call(30));
        Assert.AreEqual(VmLimitKind.CallStackDepth, ex.LimitKind);
        Assert.AreEqual(2L, ex.Limit);
        Assert.AreEqual(3L, ex.AttemptedValue);
    }

    // ── ControlFlowStack overflow ─────────────────────────────────────────────

    [TestMethod]
    public void ControlFlowStack_ExactLimit_Succeeds()
    {
        var cfs = new ControlFlowStack(maxDepth: 2);
        cfs.PushLoop(0, 10);
        cfs.PushLoop(1, 20);
        Assert.AreEqual(2, cfs.Depth);
    }

    [TestMethod]
    public void ControlFlowStack_OnePastLimit_ThrowsVmLimitExceededException()
    {
        var cfs = new ControlFlowStack(maxDepth: 2);
        cfs.PushLoop(0, 10);
        cfs.PushLoop(1, 20);
        var ex = Assert.ThrowsException<VmLimitExceededException>(() => cfs.PushLoop(2, 30));
        Assert.AreEqual(VmLimitKind.ControlFlowDepth, ex.LimitKind);
        Assert.AreEqual(2L, ex.Limit);
        Assert.AreEqual(3L, ex.AttemptedValue);
    }

    [TestMethod]
    public void ControlFlowStack_FailedPush_DoesNotChangeDepth()
    {
        var cfs = new ControlFlowStack(maxDepth: 1);
        cfs.PushLoop(0, 10);
        try { cfs.PushLoop(1, 20); } catch (VmLimitExceededException) { }
        Assert.AreEqual(1, cfs.Depth);
    }

    [TestMethod]
    public void ControlFlowStack_LimitsConstructor_UsesMaxControlFlowDepth()
    {
        var limits = new VirtualMachineLimits(maxControlFlowDepth: 3);
        var cfs = new ControlFlowStack(limits);
        Assert.AreEqual(3, cfs.MaxDepth);
    }

    // ── Context propagation via VirtualMachineLimits ──────────────────────────

    [TestMethod]
    public void DefaultContext_LimitsCtor_UsesMaxOperandStackDepth()
    {
        var limits = new VirtualMachineLimits(maxOperandStackDepth: 2);
        var ctx = new DefaultContext(ReadOnlyMemory<byte>.Empty, limits);
        ctx.Stack.Push(1);
        ctx.Stack.Push(2);
        Assert.ThrowsException<VmLimitExceededException>(() => ctx.Stack.Push(3));
    }

    [TestMethod]
    public void TypedStackContext_LimitsCtor_UsesMaxOperandStackDepth()
    {
        var limits = new VirtualMachineLimits(maxOperandStackDepth: 2);
        var ctx = new TypedStackContext<int>(ReadOnlyMemory<byte>.Empty, limits);
        ctx.Stack.Push(1);
        ctx.Stack.Push(2);
        var ex = Assert.ThrowsException<VmLimitExceededException>(() => ctx.Stack.Push(3));
        Assert.AreEqual(VmLimitKind.OperandStackDepth, ex.LimitKind);
    }

    [TestMethod]
    public void CallStackContext_LimitsCtor_UsesMaxCallStackDepthAndMaxOperandStackDepth()
    {
        var limits = new VirtualMachineLimits(maxCallStackDepth: 2, maxOperandStackDepth: 3);
        var ctx = new CallStackContext(ReadOnlyMemory<byte>.Empty, limits);
        // Verify call stack limit
        ctx.CallStack.Call(10);
        ctx.CallStack.Call(20);
        Assert.ThrowsException<VmLimitExceededException>(() => ctx.CallStack.Call(30));
        // Verify operand stack limit
        ctx.Stack.Push(1);
        ctx.Stack.Push(2);
        ctx.Stack.Push(3);
        Assert.ThrowsException<VmLimitExceededException>(() => ctx.Stack.Push(4));
    }

    // ── VirtualProcessor.Execute with ExecutionLimits ─────────────────────────

    // Builds a program of N STEP instructions followed by a HALT.
    private static byte[] BuildNStepProgram(int n)
    {
        var bytes = new byte[n + 1];
        for (int i = 0; i < n; i++) bytes[i] = 0x01; // STEP
        bytes[n] = 0x00; // HALT
        return bytes;
    }

    [TestMethod]
    public void Execute_UnlimitedLimits_RunsFullProgram()
    {
        var proc = new LimitTestProcessor();
        var ctx = new DefaultContext(BuildNStepProgram(5));
        proc.Execute(ctx, ExecutionLimits.Unlimited, CancellationToken.None);
        // If no exception, the program ran to completion.
        Assert.AreEqual(-1, ctx.InstructionPointer);
    }

    [TestMethod]
    public void Execute_BudgetLargerThanProgram_Succeeds()
    {
        var proc = new LimitTestProcessor();
        var ctx = new DefaultContext(BuildNStepProgram(3)); // 4 instructions: 3 STEP + HALT
        proc.Execute(ctx, new ExecutionLimits(100), CancellationToken.None);
        Assert.AreEqual(-1, ctx.InstructionPointer);
    }

    [TestMethod]
    public void Execute_BudgetExactlyProgram_Succeeds()
    {
        // Program has exactly 4 instructions (3 STEP + HALT).
        // Budget of 4 should allow all 4 to dispatch before the loop exits naturally.
        var proc = new LimitTestProcessor();
        var ctx = new DefaultContext(BuildNStepProgram(3)); // 4 instructions
        proc.Execute(ctx, new ExecutionLimits(4), CancellationToken.None);
        Assert.AreEqual(-1, ctx.InstructionPointer);
    }

    [TestMethod]
    public void Execute_BudgetBelowProgramLength_ThrowsInstructionBudgetExceededException()
    {
        // Program needs 4 instructions (3 STEP + HALT). Budget of 2 should throw.
        var proc = new LimitTestProcessor();
        var ctx = new DefaultContext(BuildNStepProgram(3));
        Assert.ThrowsException<InstructionBudgetExceededException>(
            () => proc.Execute(ctx, new ExecutionLimits(2), CancellationToken.None));
    }

    [TestMethod]
    public void Execute_BudgetExceeded_ThrowsVmLimitExceededException()
    {
        // InstructionBudgetExceededException is a VmLimitExceededException
        var proc = new LimitTestProcessor();
        var ctx = new DefaultContext(BuildNStepProgram(5));
        var ex = Assert.ThrowsException<InstructionBudgetExceededException>(
            () => proc.Execute(ctx, new ExecutionLimits(3), CancellationToken.None));
        Assert.AreEqual(3L, ex.Budget);
        Assert.IsInstanceOfType<VmLimitExceededException>(ex);
    }

    [TestMethod]
    public void Execute_NegativeLegacyBudget_Throws()
    {
        var proc = new LimitTestProcessor();
        var ctx = new DefaultContext(BuildNStepProgram(3));
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => proc.Execute(ctx, maxInstructions: -1));
    }

    [TestMethod]
    public void Execute_ZeroLegacyBudget_IsUnlimited()
    {
        var proc = new LimitTestProcessor();
        var ctx = new DefaultContext(BuildNStepProgram(3));
        // Should run to completion without throwing.
        proc.Execute(ctx, maxInstructions: 0);
        Assert.AreEqual(-1, ctx.InstructionPointer);
    }

    // ── Scheduler.Run with ExecutionLimits ────────────────────────────────────

    private static CountingProcessor SchProc() => new();
    private static DefaultContext SchCtx(params byte[] program) => new(program);

    [TestMethod]
    public void SchedulerRun_UnlimitedLimits_RunsAllProcesses()
    {
        var scheduler = new Scheduler<DefaultContext>();
        scheduler.AddProcess(SchCtx(0x01, 0x01, 0x00), SchProc()); // 2 STEP + HALT
        scheduler.Run(ExecutionLimits.Unlimited, CancellationToken.None);
        Assert.IsTrue(scheduler.Processes[0].State == ProcessState.Terminated);
    }

    [TestMethod]
    public void SchedulerRun_BudgetExhausted_ThrowsInstructionBudgetExceededException()
    {
        // 10 STEP instructions + HALT — budget of 3 should be exceeded.
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 5);
        var program = new byte[11];
        for (int i = 0; i < 10; i++) program[i] = 0x01;
        program[10] = 0x00;
        scheduler.AddProcess(new DefaultContext(program), SchProc());
        Assert.ThrowsException<InstructionBudgetExceededException>(
            () => scheduler.Run(new ExecutionLimits(3), CancellationToken.None));
    }

    [TestMethod]
    public void SchedulerRun_BudgetExhausted_ProcessIsReadyForResume()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 5);
        var program = new byte[11];
        for (int i = 0; i < 10; i++) program[i] = 0x01;
        program[10] = 0x00;
        var proc = scheduler.AddProcess(new DefaultContext(program), SchProc());
        try { scheduler.Run(new ExecutionLimits(3), CancellationToken.None); } catch (InstructionBudgetExceededException) { }
        Assert.AreEqual(ProcessState.Ready, proc.State);
    }

    [TestMethod]
    public void SchedulerRun_NegativeLegacyBudget_Throws()
    {
        var scheduler = new Scheduler<DefaultContext>();
        scheduler.AddProcess(SchCtx(0x00), SchProc());
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => scheduler.Run(cancellationToken: default, maxInstructions: -1));
    }

    // ── Scheduler process capacity ────────────────────────────────────────────

    [TestMethod]
    public void Scheduler_FromLimits_UsesMaxScheduledProcesses()
    {
        var limits = new VirtualMachineLimits(maxScheduledProcesses: 2);
        var scheduler = new Scheduler<DefaultContext>(limits);
        scheduler.AddProcess(SchCtx(0x00), SchProc());
        scheduler.AddProcess(SchCtx(0x00), SchProc());
        var ex = Assert.ThrowsException<VmLimitExceededException>(
            () => scheduler.AddProcess(SchCtx(0x00), SchProc()));
        Assert.AreEqual(VmLimitKind.ScheduledProcessCount, ex.LimitKind);
        Assert.AreEqual(2L, ex.Limit);
        Assert.AreEqual(3L, ex.AttemptedValue);
    }

    [TestMethod]
    public void Scheduler_ProcessCountAtLimit_Succeeds()
    {
        var limits = new VirtualMachineLimits(maxScheduledProcesses: 2);
        var scheduler = new Scheduler<DefaultContext>(limits);
        scheduler.AddProcess(SchCtx(0x00), SchProc());
        scheduler.AddProcess(SchCtx(0x00), SchProc());
        Assert.AreEqual(2, scheduler.Processes.Count);
    }

    [TestMethod]
    public void Scheduler_FailedAddProcess_DoesNotConsumeId()
    {
        var limits = new VirtualMachineLimits(maxScheduledProcesses: 1);
        var scheduler = new Scheduler<DefaultContext>(limits);
        scheduler.AddProcess(SchCtx(0x00), SchProc());
        try { scheduler.AddProcess(SchCtx(0x00), SchProc()); } catch (VmLimitExceededException) { }
        Assert.AreEqual(1, scheduler.Processes.Count);
    }

    [TestMethod]
    public void Scheduler_FromLimits_UsesSchedulerQuantumSteps()
    {
        var limits = new VirtualMachineLimits(schedulerQuantumSteps: 7);
        var scheduler = new Scheduler<DefaultContext>(limits);
        Assert.AreEqual(7, scheduler.QuantumSteps);
    }

    // ── VirtualMemory page capacity ────────────────────────────────────────────

    [TestMethod]
    public void VirtualMemory_AllocatePage_AtLimit_Succeeds()
    {
        var limits = new VirtualMachineLimits(maxPhysicalPages: 2);
        var mem = new VirtualMemory<int>(pageSize: 16, limits);
        mem.AllocatePage();
        mem.AllocatePage();
        Assert.AreEqual(2, mem.Pages.Count);
    }

    [TestMethod]
    public void VirtualMemory_AllocatePage_OnePastLimit_ThrowsVmLimitExceededException()
    {
        var limits = new VirtualMachineLimits(maxPhysicalPages: 2);
        var mem = new VirtualMemory<int>(pageSize: 16, limits);
        mem.AllocatePage();
        mem.AllocatePage();
        var ex = Assert.ThrowsException<VmLimitExceededException>(() => mem.AllocatePage());
        Assert.AreEqual(VmLimitKind.PhysicalPageCount, ex.LimitKind);
        Assert.AreEqual(2L, ex.Limit);
        Assert.AreEqual(3L, ex.AttemptedValue);
    }

    [TestMethod]
    public void VirtualMemory_AllocatePage_FailedAlloc_StateUnchanged()
    {
        var limits = new VirtualMachineLimits(maxPhysicalPages: 1);
        var mem = new VirtualMemory<int>(pageSize: 16, limits);
        mem.AllocatePage();
        int pagesBeforeFailure = mem.Pages.Count;
        try { mem.AllocatePage(); } catch (VmLimitExceededException) { }
        Assert.AreEqual(pagesBeforeFailure, mem.Pages.Count);
    }

    // ── VirtualMemory process capacity ────────────────────────────────────────

    [TestMethod]
    public void VirtualMemory_MasterCountsTowardProcessLimit()
    {
        // With maxMemoryProcesses=1, the master process fills the slot,
        // so CreateProcess should throw.
        var limits = new VirtualMachineLimits(maxMemoryProcesses: 1);
        var mem = new VirtualMemory<int>(pageSize: 16, limits);
        Assert.AreEqual(1, mem.Processes.Count); // master only
        var ex = Assert.ThrowsException<VmLimitExceededException>(() => mem.CreateProcess());
        Assert.AreEqual(VmLimitKind.MemoryProcessCount, ex.LimitKind);
    }

    [TestMethod]
    public void VirtualMemory_CreateProcess_AtLimit_Succeeds()
    {
        // maxMemoryProcesses=3: master + 2 extra.
        var limits = new VirtualMachineLimits(maxMemoryProcesses: 3);
        var mem = new VirtualMemory<int>(pageSize: 16, limits);
        mem.CreateProcess();
        mem.CreateProcess();
        Assert.AreEqual(3, mem.Processes.Count);
    }

    [TestMethod]
    public void VirtualMemory_CreateProcess_OnePastLimit_ThrowsVmLimitExceededException()
    {
        var limits = new VirtualMachineLimits(maxMemoryProcesses: 2);
        var mem = new VirtualMemory<int>(pageSize: 16, limits);
        mem.CreateProcess(); // 2 total (master + 1)
        var ex = Assert.ThrowsException<VmLimitExceededException>(() => mem.CreateProcess());
        Assert.AreEqual(VmLimitKind.MemoryProcessCount, ex.LimitKind);
        Assert.AreEqual(2L, ex.Limit);
        Assert.AreEqual(3L, ex.AttemptedValue);
    }

    [TestMethod]
    public void VirtualMemory_AfterFreeProcess_CapacityAvailable()
    {
        var limits = new VirtualMachineLimits(maxMemoryProcesses: 2);
        var mem = new VirtualMemory<int>(pageSize: 16, limits);
        var proc = mem.CreateProcess(); // 2 total
        // Can't add more.
        Assert.ThrowsException<VmLimitExceededException>(() => mem.CreateProcess());
        // Free one, then it should be possible to add again.
        mem.FreeProcess(proc);
        var newProc = mem.CreateProcess(); // Should succeed: back to 2 total.
        Assert.IsNotNull(newProc);
    }

    // ── Scheduler.Run budget shared across processes ───────────────────────────

    [TestMethod]
    public void SchedulerRun_BudgetSharedAcrossProcesses()
    {
        // Two processes, 5 STEPs each, total = 10 instructions.
        // Budget of 5 should be exceeded before any process completes.
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 3);
        var program = new byte[6];
        for (int i = 0; i < 5; i++) program[i] = 0x01;
        program[5] = 0x00;
        scheduler.AddProcess(new DefaultContext(program), SchProc());
        scheduler.AddProcess(new DefaultContext((byte[])program.Clone()), SchProc());
        Assert.ThrowsException<InstructionBudgetExceededException>(
            () => scheduler.Run(new ExecutionLimits(5), CancellationToken.None));
    }

    // ── SchedulerRun_RunAsync with ExecutionLimits ─────────────────────────────

    [TestMethod]
    public async Task SchedulerRunAsync_BudgetExhausted_ThrowsInstructionBudgetExceededException()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 5);
        var program = new byte[11];
        for (int i = 0; i < 10; i++) program[i] = 0x01;
        program[10] = 0x00;
        scheduler.AddProcess(new DefaultContext(program), SchProc());
        await Assert.ThrowsExceptionAsync<InstructionBudgetExceededException>(
            () => scheduler.RunAsync(new ExecutionLimits(3), CancellationToken.None));
    }

    [TestMethod]
    public async Task SchedulerRunAsync_NegativeLegacyBudget_Throws()
    {
        var scheduler = new Scheduler<DefaultContext>();
        scheduler.AddProcess(SchCtx(0x00), SchProc());
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
            () => scheduler.RunAsync(cancellationToken: default, maxInstructions: -1));
    }
}
