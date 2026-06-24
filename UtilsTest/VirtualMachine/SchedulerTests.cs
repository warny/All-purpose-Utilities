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
}
