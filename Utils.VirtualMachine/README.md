# omy.Utils.VirtualMachine (byte-code interpreter)

`omy.Utils.VirtualMachine` implements a small, attribute-driven byte-code interpreter for custom instruction sets.

## Install
```bash
dotnet add package omy.Utils.VirtualMachine
```

## Supported frameworks
- net8.0

## Features
- Byte-code processor supporting little- and big-endian formats.
- Attribute-based opcode declarations for concise instruction definitions.
- Facilities to plug in stacks, registers, and memory models.

## Quick usage
```csharp
using Utils.VirtualMachine;

class SampleMachine : VirtualProcessor<DefaultContext>
{
    [Instruction("PUSH", 0x01, 0x01)]
    void Push(DefaultContext ctx, byte value) => ctx.Stack.Push(value);

    [Instruction("ADD", 0x10, 0x01)]
    void Add(DefaultContext ctx)
    {
        var b = (byte)ctx.Stack.Pop();
        var a = (byte)ctx.Stack.Pop();
        ctx.Stack.Push((byte)(a + b));
    }
}
```

## Related packages
- `omy.Utils.IO` – for binary parsing helpers.
- `omy.Utils.Fonts` – uses the VM framework for font table parsing.
