# Utils.VirtualMachine Library

The **Utils.VirtualMachine** package implements a small and extensible byte-code interpreter.
It targets **.NET 9** and uses attributes to define instructions, making it easy to build custom instruction sets.

## Features

- Byte-code processor supporting little and big endian formats
- Attribute based declaration of opcodes and instruction handlers
- Facilities to implement stacks, registers and memory models as separate components
- Used by the other libraries for parsing binary data and implementing simple VMs

## Usage example

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

byte[] program =
[
    0x01, 0x01, 0x10, // PUSH 0x10
    0x01, 0x01, 0x01, // PUSH 0x01
    0x10, 0x01        // ADD
];

var context = new DefaultContext(program);
new SampleMachine().Execute(context);
```
