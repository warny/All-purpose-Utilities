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

## Related packages
- `omy.Utils.IO` – binary parsing helpers used by the VM framework.
- `omy.Utils.Fonts` – uses the VM framework for font table parsing.
