# omy.Utils.VirtualMachine (byte-code interpreter)

`omy.Utils.VirtualMachine` implements a small, attribute-driven byte-code interpreter for custom instruction sets.

## Install
```bash
dotnet add package omy.Utils.VirtualMachine
```

## Supported frameworks
- net8.0

## Features
- `VirtualProcessor<T>` — abstract base class; derive and annotate methods with `[Instruction]` to register opcodes.
- `[Instruction(name, params byte[] opcode)]` — maps a method to a single- or multi-byte opcode.
- `Context` — abstract execution context carrying the byte-code array and instruction pointer.
- `DefaultContext` — concrete context with a `Stack<object>` for stack-based machines.
- Supports little-endian (default) and big-endian operand reading.

## Quick usage

```csharp
using Utils.VirtualMachine;

class StackMachine : VirtualProcessor<DefaultContext>
{
    [Instruction("PUSH", 0x01)]
    void Push(DefaultContext ctx, byte value) => ctx.Stack.Push(value);

    [Instruction("ADD", 0x10)]
    void Add(DefaultContext ctx)
    {
        byte b = (byte)ctx.Stack.Pop();
        byte a = (byte)ctx.Stack.Pop();
        ctx.Stack.Push((byte)(a + b));
    }
}

byte[] program = [0x01, 3, 0x01, 4, 0x10]; // PUSH 3, PUSH 4, ADD
var machine = new StackMachine();
var context = new DefaultContext(program);
machine.Execute(context);

Console.WriteLine((byte)context.Stack.Peek()); // 7
```

## Multi-byte opcodes

When different instruction variants share a common prefix, use a multi-byte opcode to disambiguate:

```csharp
using Utils.VirtualMachine;

class ExtendedMachine : VirtualProcessor<DefaultContext>
{
    [Instruction("PUSH_INT", 0x01, 0x01)]
    void PushInt(DefaultContext ctx, int value) => ctx.Stack.Push(value);

    [Instruction("PUSH_BYTE", 0x01, 0x02)]
    void PushByte(DefaultContext ctx, byte value) => ctx.Stack.Push(value);
}
```

## Custom context

Derive from `Context` to add registers, memory, or other execution state:

```csharp
using Utils.VirtualMachine;

class CpuContext : Context
{
    public CpuContext(byte[] program) : base(program) { }

    public int[] Registers { get; } = new int[8];
}

class Cpu : VirtualProcessor<CpuContext>
{
    [Instruction("LOAD", 0x20)]
    void Load(CpuContext ctx, byte reg, int value)
        => ctx.Registers[reg] = value;

    [Instruction("ADD_REG", 0x21)]
    void AddReg(CpuContext ctx, byte dest, byte src)
        => ctx.Registers[dest] += ctx.Registers[src];
}
```

## Big-endian mode

Pass `littleEndian: false` to interpret multi-byte operands as big-endian:

```csharp
using Utils.VirtualMachine;

class BigEndianMachine : VirtualProcessor<DefaultContext>
{
    protected BigEndianMachine() : base(littleEndian: false) { }

    [Instruction("PUSH16", 0x03)]
    void Push16(DefaultContext ctx, short value) => ctx.Stack.Push(value);
}
```

## Cooperative scheduler

`Scheduler<T>` runs multiple processes concurrently using cooperative, priority-based time-slicing.
Each `Step()` call advances **all** ready processes in descending priority order, giving each one up
to `quantumSteps` instructions before moving to the next.

```csharp
using Utils.VirtualMachine;

var scheduler = new Scheduler<DefaultContext>(quantumSteps: 10);

// Add processes; optional name aids diagnostics.
var p1 = scheduler.AddProcess(ctx1, machine1, priority: 1, name: "worker-A");
var p2 = scheduler.AddProcess(ctx2, machine2, priority: 2, name: "worker-B");

// Blocking run (executes on the calling thread).
scheduler.Run();

// Non-blocking async variant — yields between each quantum.
await scheduler.RunAsync(cancellationToken);

Console.WriteLine(p1.Name);      // "worker-A"
Console.WriteLine(p1.State);     // ProcessState.Terminated
```

## Structured control flow (CallStack / ControlFlow)

`ControlFlowStack` tracks nested conditionals, loops, and try/catch/finally blocks at runtime.
`ControlFlowContext` bundles a `DefaultContext` with a `ControlFlowStack`; `FullContext` also adds a call stack.

```csharp
using Utils.VirtualMachine;

class MyMachine : VirtualProcessor<ControlFlowContext>
{
    // TRY — opens an exception block.
    [Instruction("TRY", 0x30)]
    void Try(ControlFlowContext ctx, int catchAddr, int finallyAddr)
        => ctx.ControlFlow.PushException(ctx.InstructionPointer - 1, catchAddr, finallyAddr);

    // THROW — finds the nearest handler; runs finally first when present.
    [Instruction("THROW", 0x31)]
    void Throw(ControlFlowContext ctx)
        => ctx.ControlFlow.Throw(ctx, ctx.Stack.Pop());

    // ENDFINALLY — after finally completes, jumps to catch (if pending) or pops the block.
    [Instruction("ENDFINALLY", 0x32)]
    void EndFinally(ControlFlowContext ctx)
        => ctx.ControlFlow.EndFinally(ctx);
}
```

When `Throw()` finds a block with both a catch and a finally clause, it stores the catch address
in `ExceptionBlock.PendingCatchAddress` and jumps to the finally block first. `EndFinally()` then
redirects to the catch handler automatically.

## Virtual memory

`VirtualMemory<TAddress>` provides a paged, process-isolated address space. Each physical page can
be mapped into multiple processes with independent access rights.

```csharp
using Utils.VirtualMachine;

var mem = new VirtualMemory<int>(pageSize: 256);
var page = mem.AllocatePage();  // auto-mapped into MasterProcess with ReadWrite.

var child = mem.CreateProcess();
mem.MapPage(child, page, virtualPageIndex: 0, PageAccess.ReadOnly);

// Read and write through a process view.
mem.MasterProcess.Write(0, new byte[] { 1, 2, 3 });
var buf = new byte[3];
child.Read(0, buf);   // succeeds (ReadOnly)

// Release a child process and unmap all its pages.
mem.FreeProcess(child);
```

## Debugging with IVmInspector

Attach an `IVmInspector<T>` to a `VirtualProcessor<T>` to intercept every instruction and
trigger breakpoints without modifying the instruction set.

```csharp
using Utils.VirtualMachine;

class TraceInspector : IVmInspector<DefaultContext>
{
    public void BeforeInstruction(DefaultContext ctx, int address, string name)
        => Console.WriteLine($"[{address:X4}] {name}");

    public void OnBreakpoint(DefaultContext ctx, int address, string name)
        => Console.WriteLine($"*** breakpoint at {address:X4} ({name})");
}

var machine = new StackMachine();
machine.Inspector = new TraceInspector();
machine.Breakpoints.Add(0x0005);  // break when IP == 5

machine.Execute(ctx);
```

`BeforeInstruction` is called for every instruction. `OnBreakpoint` is called first when the
current address is in `Breakpoints`, then `BeforeInstruction` follows.

## Related packages
- `omy.Utils.IO` – binary parsing helpers used by the VM framework.
- `omy.Utils.Fonts` – uses the VM framework for font table parsing.
