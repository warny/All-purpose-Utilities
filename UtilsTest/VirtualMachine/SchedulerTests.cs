using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine;

// ── Test infrastructure ────────────────────────────────────────────────────────

/// <summary>
/// Minimal processor for scheduler tests.
/// Opcode 0x01 = STEP (pushes 1 onto ctx.Stack to count executions).
/// Opcode 0x00 = HALT (calls ctx.Terminate()).
/// Use RegisterInstruction with overwrite:true to capture closures per test.
/// </summary>
public class CountingProcessor : VirtualProcessor<DefaultContext>
{
    public CountingProcessor()
    {
        RegisterInstruction([0x01], "STEP", ctx => ctx.Stack.Push(1));
        RegisterInstruction([0x00], "HALT", ctx => ctx.Terminate());
    }
}

// ── Tests ──────────────────────────────────────────────────────────────────────

[TestClass]
public class SchedulerTests
{
    private static DefaultContext Ctx(params byte[] program) => new(program);
    private static CountingProcessor Proc() => new();

    // ── AddProcess ────────────────────────────────────────────────────────────

    [TestMethod]
    public void AddProcess_ReturnsProcess()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = scheduler.AddProcess(Ctx(0x00), Proc());
        Assert.IsNotNull(proc);
    }

    [TestMethod]
    public void AddProcess_StateIsReady()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = scheduler.AddProcess(Ctx(0x00), Proc());
        Assert.AreEqual(ProcessState.Ready, proc.State);
    }

    [TestMethod]
    public void AddProcess_AddsToProcessesList()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = scheduler.AddProcess(Ctx(0x00), Proc());
        Assert.IsTrue(scheduler.Processes.Contains(proc));
    }

    [TestMethod]
    public void AddProcess_AssignsUniqueIds()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var p1 = scheduler.AddProcess(Ctx(0x00), Proc());
        var p2 = scheduler.AddProcess(Ctx(0x00), Proc());
        var p3 = scheduler.AddProcess(Ctx(0x00), Proc());
        var ids = new[] { p1.ProcessId, p2.ProcessId, p3.ProcessId };
        Assert.AreEqual(3, ids.Distinct().Count());
    }

    [TestMethod]
    public void AddProcess_PrioritySet()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = scheduler.AddProcess(Ctx(0x00), Proc(), priority: 42);
        Assert.AreEqual(42, proc.Priority);
    }

    // ── RemoveProcess ─────────────────────────────────────────────────────────

    [TestMethod]
    public void RemoveProcess_RemovesFromList()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = scheduler.AddProcess(Ctx(0x00), Proc());
        scheduler.RemoveProcess(proc.ProcessId);
        Assert.IsFalse(scheduler.Processes.Contains(proc));
    }

    [TestMethod]
    public void RemoveProcess_UnknownId_NoOp()
    {
        var scheduler = new Scheduler<DefaultContext>();
        scheduler.AddProcess(Ctx(0x00), Proc());
        scheduler.RemoveProcess(9999);
        Assert.AreEqual(1, scheduler.Processes.Count);
    }

    // ── Step / Quantum ────────────────────────────────────────────────────────

    [TestMethod]
    public void Step_RunsReadyProcesses()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var ctx = Ctx(0x01, 0x01, 0x00); // 2 STEPs then HALT
        scheduler.AddProcess(ctx, Proc());
        scheduler.Step();
        Assert.IsTrue(ctx.Stack.Count >= 1);
    }

    [TestMethod]
    public void Step_ReturnsFalse_WhenNoReady()
    {
        var scheduler = new Scheduler<DefaultContext>();
        bool result = scheduler.Step();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Step_ProcessTerminates_WhenNoMoreInstructions()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var proc = scheduler.AddProcess(Ctx(0x00), Proc()); // single HALT
        scheduler.Step();
        Assert.AreEqual(ProcessState.Terminated, proc.State);
    }

    [TestMethod]
    public void Step_ProcessGetsQuantumSteps_Exactly()
    {
        // 10 STEPs, no HALT — quantum of 3 → exactly 3 should run per Step()
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 3);
        var ctx = Ctx(0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01);
        scheduler.AddProcess(ctx, Proc());
        scheduler.Step();
        Assert.AreEqual(3, ctx.Stack.Count);
    }

    [TestMethod]
    public void Step_ProcessBecomesReady_AfterQuantum()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 2);
        // More instructions than the quantum, no HALT
        var proc = scheduler.AddProcess(Ctx(0x01, 0x01, 0x01, 0x01, 0x01), Proc());
        scheduler.Step();
        Assert.AreEqual(ProcessState.Ready, proc.State);
    }

    [TestMethod]
    public void Step_ReturnsTrue_WhenProcessesRan()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 5);
        scheduler.AddProcess(Ctx(0x01, 0x01, 0x01), Proc());
        bool result = scheduler.Step();
        Assert.IsTrue(result);
    }

    // ── Priority ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void Step_HighPriorityRunsBeforeLow()
    {
        var executionOrder = new List<int>();
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 1);

        var procLow  = new CountingProcessor();
        var procHigh = new CountingProcessor();

        procLow.RegisterInstruction([0x01],  "STEP", _ => executionOrder.Add(0), overwrite: true);
        procHigh.RegisterInstruction([0x01], "STEP", _ => executionOrder.Add(1), overwrite: true);

        scheduler.AddProcess(Ctx(0x01, 0x00), procLow,  priority: 0);
        scheduler.AddProcess(Ctx(0x01, 0x00), procHigh, priority: 10);

        scheduler.Step();

        Assert.IsTrue(executionOrder.Count >= 2);
        // High-priority process (id=1) must appear before low-priority process (id=0)
        Assert.IsTrue(executionOrder.IndexOf(1) < executionOrder.IndexOf(0));
    }

    [TestMethod]
    public void Step_SamePriority_AllRun()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 1);
        var ctx1 = Ctx(0x01, 0x00);
        var ctx2 = Ctx(0x01, 0x00);
        scheduler.AddProcess(ctx1, Proc(), priority: 5);
        scheduler.AddProcess(ctx2, Proc(), priority: 5);
        scheduler.Step();
        Assert.IsTrue(ctx1.Stack.Count >= 1);
        Assert.IsTrue(ctx2.Stack.Count >= 1);
    }

    // ── Yield coopératif ──────────────────────────────────────────────────────

    [TestMethod]
    public void RequestYield_StopsQuantumEarly()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var ctx = Ctx(0x01, 0x01, 0x01, 0x01, 0x01); // 5 STEPs, no HALT
        ScheduledProcess<DefaultContext>? handle = null;

        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x01], "STEP", c =>
        {
            c.Stack.Push(1);
            handle?.RequestYield();
        }, overwrite: true);

        handle = scheduler.AddProcess(ctx, processor);
        scheduler.Step();

        // Yield was requested after the first instruction → only 1 STEP ran
        Assert.AreEqual(1, ctx.Stack.Count);
    }

    [TestMethod]
    public void RequestYield_ProcessRemainsReady()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var ctx = Ctx(0x01, 0x01, 0x01);
        ScheduledProcess<DefaultContext>? handle = null;

        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x01], "STEP", c =>
        {
            c.Stack.Push(1);
            handle?.RequestYield();
        }, overwrite: true);

        handle = scheduler.AddProcess(ctx, processor);
        scheduler.Step();

        Assert.AreEqual(ProcessState.Ready, handle.State);
    }

    // ── Suspend / Resume ──────────────────────────────────────────────────────

    [TestMethod]
    public void Suspend_ReadyProcess_SetsSuspended()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = scheduler.AddProcess(Ctx(0x01, 0x01), Proc());
        Assert.AreEqual(ProcessState.Ready, proc.State);
        proc.Suspend();
        Assert.AreEqual(ProcessState.Suspended, proc.State);
    }

    [TestMethod]
    public void Suspend_FromInsideHandler_PausesProcess()
    {
        // Handler suspends the process after the first STEP — only 1 instruction should run
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var ctx = Ctx(0x01, 0x01, 0x01, 0x01);
        ScheduledProcess<DefaultContext>? handle = null;

        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x01], "STEP", c =>
        {
            c.Stack.Push(1);
            handle?.Suspend();
        }, overwrite: true);

        handle = scheduler.AddProcess(ctx, processor);
        scheduler.Step();

        Assert.AreEqual(ProcessState.Suspended, handle.State);
        Assert.AreEqual(1, ctx.Stack.Count);
    }

    [TestMethod]
    public void Resume_SuspendedProcess_SetsReady()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = scheduler.AddProcess(Ctx(0x01), Proc());
        proc.Suspend();
        proc.Resume();
        Assert.AreEqual(ProcessState.Ready, proc.State);
    }

    [TestMethod]
    public void SuspendedProcess_SkippedByScheduler()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var ctx = Ctx(0x01, 0x01, 0x01);
        var proc = scheduler.AddProcess(ctx, Proc());
        proc.Suspend();
        bool ran = scheduler.Step();
        Assert.IsFalse(ran);
        Assert.AreEqual(0, ctx.Stack.Count);
    }

    [TestMethod]
    public void Suspend_TerminatedProcess_NoOp()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var proc = scheduler.AddProcess(Ctx(0x00), Proc());
        scheduler.Step();
        Assert.AreEqual(ProcessState.Terminated, proc.State);
        proc.Suspend(); // must not throw, must not change state
        Assert.AreEqual(ProcessState.Terminated, proc.State);
    }

    [TestMethod]
    public void Suspend_FaultedProcess_RemainsFaulted()
    {
        // Faulted is a terminal state: Suspend must not demote it to Suspended.
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x02], "BOOM", _ => throw new InvalidOperationException("fault"));

        var proc = scheduler.AddProcess(Ctx(0x02), processor);
        scheduler.Step();
        Assert.AreEqual(ProcessState.Faulted, proc.State);

        proc.Suspend();

        Assert.AreEqual(ProcessState.Faulted, proc.State);
        Assert.IsNotNull(proc.FaultException);
    }

    [TestMethod]
    public void FaultedProcess_CannotBeResumed()
    {
        // A faulted process must not be resurrectable via Suspend() + Resume().
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x02], "BOOM", _ => throw new InvalidOperationException("fault"));

        var proc = scheduler.AddProcess(Ctx(0x02), processor);
        scheduler.Step();
        Assert.AreEqual(ProcessState.Faulted, proc.State);

        proc.Suspend();
        proc.Resume();

        Assert.AreEqual(ProcessState.Faulted, proc.State,
            "Suspend()+Resume() must not revive a Faulted process.");
    }

    // ── Quantum boundary termination ──────────────────────────────────────────

    [TestMethod]
    public void Step_ProcessTerminates_WhenHaltLandsOnLastQuantumSlot()
    {
        // HALT falls exactly on the last quantum slot: ExecuteStep returns true
        // (it executed the instruction), so the loop ends naturally without hitting
        // the early-break path. The scheduler must still detect the terminated context.
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 2);
        var proc = scheduler.AddProcess(Ctx(0x01, 0x00), Proc()); // STEP then HALT
        scheduler.Step();
        Assert.AreEqual(ProcessState.Terminated, proc.State);
    }

    // ── Cross-process interactions within a Step pass ─────────────────────────

    [TestMethod]
    public void Step_HighPriorityRemovesLow_LowDoesNotRunInSamePass()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var lowCtx = Ctx(0x01, 0x01, 0x01);
        ScheduledProcess<DefaultContext>? lowProc = null;

        var highProcessor = new CountingProcessor();
        highProcessor.RegisterInstruction([0x01], "STEP", _ =>
            scheduler.RemoveProcess(lowProc!.ProcessId), overwrite: true);

        scheduler.AddProcess(Ctx(0x01, 0x00), highProcessor, priority: 10);
        lowProc = scheduler.AddProcess(lowCtx, Proc(), priority: 0);

        scheduler.Step();

        Assert.AreEqual(0, lowCtx.Stack.Count);
        Assert.IsFalse(scheduler.Processes.Contains(lowProc));
    }

    [TestMethod]
    public void Step_HighPrioritySuspendsLow_LowDoesNotRunInSamePass()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var lowCtx = Ctx(0x01, 0x01, 0x01);
        ScheduledProcess<DefaultContext>? lowProc = null;

        var highProcessor = new CountingProcessor();
        highProcessor.RegisterInstruction([0x01], "STEP", _ =>
            lowProc!.Suspend(), overwrite: true);

        scheduler.AddProcess(Ctx(0x01, 0x00), highProcessor, priority: 10);
        lowProc = scheduler.AddProcess(lowCtx, Proc(), priority: 0);

        scheduler.Step();

        Assert.AreEqual(0, lowCtx.Stack.Count);
        Assert.AreEqual(ProcessState.Suspended, lowProc.State);
    }

    // ── Run ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Run_ExecutesUntilAllTerminated()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 2);
        var ctx = Ctx(0x01, 0x01, 0x01, 0x00); // 3 STEPs then HALT
        var proc = scheduler.AddProcess(ctx, Proc());
        scheduler.Run();
        Assert.AreEqual(ProcessState.Terminated, proc.State);
        Assert.AreEqual(3, ctx.Stack.Count);
    }

    [TestMethod]
    public void Run_CancellationToken_Stops()
    {
        var program = Enumerable.Repeat((byte)0x01, 10_000).ToArray();
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        scheduler.AddProcess(new DefaultContext(program), Proc());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsException<OperationCanceledException>(() => scheduler.Run(cts.Token));
    }

    [TestMethod]
    public void Run_MultipleConcurrentProcesses_AllTerminate()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 2);
        var tracked = new List<(DefaultContext ctx, ScheduledProcess<DefaultContext> proc)>();

        for (int i = 0; i < 5; i++)
        {
            // Each process: 4 STEPs then HALT
            var ctx = Ctx(0x01, 0x01, 0x01, 0x01, 0x00);
            var proc = scheduler.AddProcess(ctx, Proc(), priority: i);
            tracked.Add((ctx, proc));
        }

        scheduler.Run();

        foreach (var (ctx, proc) in tracked)
        {
            Assert.AreEqual(ProcessState.Terminated, proc.State,
                $"Process {proc.ProcessId} did not terminate.");
            Assert.AreEqual(4, ctx.Stack.Count,
                $"Process {proc.ProcessId} ran wrong number of steps.");
        }
    }

    // ── Process Name ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void AddProcess_NoName_NameIsNull()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = scheduler.AddProcess(Ctx(0x00), Proc());
        Assert.IsNull(proc.Name);
    }

    [TestMethod]
    public void AddProcess_WithName_NameStored()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = scheduler.AddProcess(Ctx(0x00), Proc(), name: "GC");
        Assert.AreEqual("GC", proc.Name);
    }

    [TestMethod]
    public void AddProcess_NameDoesNotAffectScheduling()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        scheduler.AddProcess(Ctx(0x01, 0x00), Proc(), name: "Worker");
        scheduler.Run();
        Assert.IsTrue(scheduler.Processes.All(p => p.State == ProcessState.Terminated));
    }

    // ── RunAsync ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void RunAsync_CompletesAllProcesses()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var ctx = Ctx(0x01, 0x01, 0x00); // STEP, STEP, HALT
        scheduler.AddProcess(ctx, Proc());
        scheduler.RunAsync().GetAwaiter().GetResult();
        Assert.AreEqual(ProcessState.Terminated, scheduler.Processes[0].State);
        Assert.AreEqual(2, ctx.Stack.Count);
    }

    [TestMethod]
    public void RunAsync_CancellationToken_Throws()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 1);
        // Infinite loop: only STEPs, no HALT
        scheduler.AddProcess(Ctx(0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01), Proc());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsException<OperationCanceledException>(
            () => scheduler.RunAsync(cts.Token).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void RunAsync_NoProcesses_CompletesImmediately()
    {
        var scheduler = new Scheduler<DefaultContext>();
        scheduler.RunAsync().GetAwaiter().GetResult(); // must not throw or block
    }

    // ── Item 2: scheduler exception transitions process to Faulted ───────────

    [TestMethod]
    public void Step_InstructionException_TransitionsProcessToFaulted()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x02], "BOOM", _ => throw new InvalidOperationException("test fault"));

        var proc = scheduler.AddProcess(Ctx(0x02), processor);
        scheduler.Step();

        Assert.AreEqual(ProcessState.Faulted, proc.State);
        Assert.IsInstanceOfType<InvalidOperationException>(proc.FaultException);
    }

    [TestMethod]
    public void Step_InstructionException_FaultExceptionStored()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x02], "BOOM", _ => throw new InvalidOperationException("sentinel"));

        var proc = scheduler.AddProcess(Ctx(0x02), processor);
        scheduler.Step();

        Assert.IsNotNull(proc.FaultException);
        Assert.AreEqual("sentinel", proc.FaultException.Message);
    }

    [TestMethod]
    public void Run_FaultedProcess_DoesNotLoopForever()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x02], "BOOM", _ => throw new InvalidOperationException());

        var proc = scheduler.AddProcess(Ctx(0x02), processor);
        scheduler.Run(); // must return, not loop forever

        Assert.AreEqual(ProcessState.Faulted, proc.State);
    }

    // ── Item 11: process removed from its handler stops further execution ─────

    [TestMethod]
    public void Step_ProcessRemovesItself_ProcessIsNotLeftReadyOrRunning()
    {
        // Regression guard: when a process removes itself from inside a handler, the
        // quantum-end cleanup block must not promote it back to Ready or leave it Running.
        // Before the fix, RemoveProcess only evicted from _processes; the cleanup block
        // then saw state==Running and set it to Ready (because IP was not past the end).
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var ctx = Ctx(0x01, 0x01, 0x01); // 3 STEPs — IP will not be past the end when removed
        ScheduledProcess<DefaultContext>? handle = null;

        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x01], "STEP", c =>
        {
            c.Stack.Push(1);
            scheduler.RemoveProcess(handle!.ProcessId);
        }, overwrite: true);

        handle = scheduler.AddProcess(ctx, processor);
        scheduler.Step();

        Assert.AreEqual(ProcessState.Terminated, handle.State,
            "A self-removed process must end up Terminated, not Ready or Running.");
        Assert.IsFalse(scheduler.Processes.Contains(handle));
    }

    [TestMethod]
    public void Step_ProcessRemovesItself_StopsFurtherExecution()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var ctx = Ctx(0x01, 0x01, 0x01, 0x01, 0x01); // 5 STEPs
        ScheduledProcess<DefaultContext>? handle = null;
        int stepCount = 0;

        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x01], "STEP", c =>
        {
            stepCount++;
            c.Stack.Push(1);
            scheduler.RemoveProcess(handle!.ProcessId); // self-remove on first step
        }, overwrite: true);

        handle = scheduler.AddProcess(ctx, processor);
        scheduler.Step();

        // Only 1 instruction must have executed: the process was removed before the 2nd.
        Assert.AreEqual(1, stepCount);
        Assert.IsFalse(scheduler.Processes.Contains(handle));
    }

    // ── Item 44: duplicate context registration is rejected ───────────────────

    [TestMethod]
    public void AddProcess_SameContext_Throws()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var ctx = Ctx(0x00);
        scheduler.AddProcess(ctx, Proc());
        Assert.ThrowsException<ArgumentException>(() => scheduler.AddProcess(ctx, Proc()));
    }

    [TestMethod]
    public void AddProcess_DifferentContexts_BothSucceed()
    {
        var scheduler = new Scheduler<DefaultContext>();
        scheduler.AddProcess(Ctx(0x00), Proc());
        scheduler.AddProcess(Ctx(0x00), Proc()); // different instance — must succeed
        Assert.AreEqual(2, scheduler.Processes.Count);
    }

    [TestMethod]
    public void AddProcess_NullProcessor_WithDuplicateContext_ThrowsArgumentNullException()
    {
        // processor null must be rejected before the duplicate-context check so that
        // the caller receives ArgumentNullException, not ArgumentException.
        var scheduler = new Scheduler<DefaultContext>();
        var ctx = Ctx(0x00);
        scheduler.AddProcess(ctx, Proc());

        var ex = Assert.ThrowsException<ArgumentNullException>(
            () => scheduler.AddProcess(ctx, null!));
        Assert.AreEqual("processor", ex.ParamName);
    }

    // ── ValidateCompletion hook (item 45) ──────────────────────────────────────────────────────

    private sealed class ValidatingProcessor : VirtualProcessor<DefaultContext>
    {
        public bool ValidateCalled;
        public bool ShouldThrow;

        public ValidatingProcessor()
        {
            RegisterInstruction([0x00], "HALT", ctx => ctx.Terminate());
        }

        public override void ValidateCompletion(DefaultContext context)
        {
            ValidateCalled = true;
            if (ShouldThrow)
                throw new InvalidOperationException("Structural completion check failed.");
        }
    }

    [TestMethod]
    public void ValidateCompletion_CalledWhenProcessTerminatesNormally()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = new ValidatingProcessor();
        scheduler.AddProcess(Ctx(0x00), proc); // HALT at byte 0
        scheduler.Run();
        Assert.IsTrue(proc.ValidateCalled);
    }

    [TestMethod]
    public void ValidateCompletion_Throws_TransitionsProcessToFaulted()
    {
        var scheduler = new Scheduler<DefaultContext>();
        var proc = new ValidatingProcessor { ShouldThrow = true };
        var sp = scheduler.AddProcess(Ctx(0x00), proc); // HALT at byte 0
        scheduler.Run();
        Assert.AreEqual(ProcessState.Faulted, sp.State);
        Assert.IsInstanceOfType<InvalidOperationException>(sp.FaultException);
    }

    // ── Instruction budget (item 21) ─────────────────────────────────────────

    [TestMethod]
    public void InstructionsExecuted_InitiallyZero()
    {
        var scheduler = new Scheduler<DefaultContext>();
        Assert.AreEqual(0L, scheduler.InstructionsExecuted);
    }

    [TestMethod]
    public void InstructionsExecuted_IncreasesAfterStep()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        // 3 STEPs then HALT = 4 instructions total.
        scheduler.AddProcess(Ctx(0x01, 0x01, 0x01, 0x00), Proc());
        scheduler.Step();
        Assert.AreEqual(4L, scheduler.InstructionsExecuted);
    }

    [TestMethod]
    public void Run_WithBudget_ThrowsInstructionBudgetExceededException()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var processor = new CountingProcessor();
        // Register a JUMP instruction that loops back to address 0.
        processor.RegisterInstruction([0x02], "JUMP_0", ctx => ctx.InstructionPointer = 0);
        scheduler.AddProcess(Ctx(0x01, 0x02), processor); // STEP, JUMP_0 — infinite loop
        Assert.ThrowsException<InstructionBudgetExceededException>(
            () => scheduler.Run(maxInstructions: 6));
    }

    [TestMethod]
    public void Run_WithBudget_ExceptionCarriesBudgetValue()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x02], "JUMP_0", ctx => ctx.InstructionPointer = 0);
        scheduler.AddProcess(Ctx(0x01, 0x02), processor);
        var ex = Assert.ThrowsException<InstructionBudgetExceededException>(
            () => scheduler.Run(maxInstructions: 4));
        Assert.AreEqual(4L, ex.Budget);
    }

    [TestMethod]
    public void Run_WithBudgetZero_Unlimited_TerminatesNormally()
    {
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        scheduler.AddProcess(Ctx(0x01, 0x01, 0x00), Proc()); // 2 STEPs then HALT
        scheduler.Run(maxInstructions: 0); // unlimited — must not throw
        Assert.AreEqual(3L, scheduler.InstructionsExecuted);
    }

    [TestMethod]
    public void Run_Budget_IsRelativeToCallStart()
    {
        // A first Run() executes 3 instructions; a second Run() with maxInstructions: 3
        // should allow another 3 instructions without throwing.
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);
        scheduler.AddProcess(Ctx(0x01, 0x01, 0x00), Proc()); // 3 instructions
        scheduler.Run();
        long afterFirst = scheduler.InstructionsExecuted;

        scheduler.AddProcess(Ctx(0x01, 0x01, 0x00), Proc()); // another 3 instructions
        scheduler.Run(maxInstructions: 3); // budget relative to this Run() start — must not throw
        Assert.AreEqual(afterFirst + 3, scheduler.InstructionsExecuted);
    }

    [TestMethod]
    public void Run_BudgetOne_ExecutesExactlyOneInstruction()
    {
        // quantum=100, budget=1 — only one instruction must be dispatched.
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x02], "JUMP_0", ctx => ctx.InstructionPointer = 0);
        scheduler.AddProcess(Ctx(0x01, 0x02), processor); // STEP, JUMP_0 — infinite loop

        Assert.ThrowsException<InstructionBudgetExceededException>(
            () => scheduler.Run(maxInstructions: 1));

        Assert.AreEqual(1L, scheduler.InstructionsExecuted);
    }

    [TestMethod]
    public void Run_BudgetSmallerThanQuantum_DoesNotOvershoot()
    {
        // quantum=100, budget=5 — must stop at 5, not at 100.
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x02], "JUMP_0", ctx => ctx.InstructionPointer = 0);
        scheduler.AddProcess(Ctx(0x01, 0x02), processor); // STEP, JUMP_0 — infinite loop

        Assert.ThrowsException<InstructionBudgetExceededException>(
            () => scheduler.Run(maxInstructions: 5));

        Assert.AreEqual(5L, scheduler.InstructionsExecuted);
    }

    [TestMethod]
    public void Run_BudgetSharedAcrossMultipleProcesses_DoesNotOvershoot()
    {
        // 2 processes, quantum=100, budget=5 — total instructions must not exceed 5.
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x02], "JUMP_0", ctx => ctx.InstructionPointer = 0);
        scheduler.AddProcess(Ctx(0x01, 0x02), processor); // infinite loop
        scheduler.AddProcess(Ctx(0x01, 0x02), processor); // infinite loop

        Assert.ThrowsException<InstructionBudgetExceededException>(
            () => scheduler.Run(maxInstructions: 5));

        Assert.IsTrue(scheduler.InstructionsExecuted <= 5,
            $"Expected ≤5 instructions but got {scheduler.InstructionsExecuted}");
    }

    [TestMethod]
    public async Task RunAsync_BudgetDoesNotOvershoot()
    {
        // async variant: budget=3 with quantum=100 must dispatch exactly 3 instructions.
        var scheduler = new Scheduler<DefaultContext>(quantumSteps: 100);
        var processor = new CountingProcessor();
        processor.RegisterInstruction([0x02], "JUMP_0", ctx => ctx.InstructionPointer = 0);
        scheduler.AddProcess(Ctx(0x01, 0x02), processor); // infinite loop

        await Assert.ThrowsExceptionAsync<InstructionBudgetExceededException>(
            () => scheduler.RunAsync(maxInstructions: 3));

        Assert.AreEqual(3L, scheduler.InstructionsExecuted);
    }

    [TestMethod]
    public void Run_NegativeMaxInstructions_ThrowsArgumentOutOfRangeException()
    {
        var scheduler = new Scheduler<DefaultContext>();
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => scheduler.Run(maxInstructions: -1));
    }

    [TestMethod]
    public async Task RunAsync_NegativeMaxInstructions_ThrowsArgumentOutOfRangeException()
    {
        var scheduler = new Scheduler<DefaultContext>();
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
            () => scheduler.RunAsync(maxInstructions: -1));
    }
}
