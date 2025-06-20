# Utils.VirtualMachine Library

The **Utils.VirtualMachine** package implements a small and extensible byte-code interpreter.
Instructions are defined via attributes, making it easy to build custom instruction sets.

## Features

- Byte-code processor supporting little and big endian formats
- Attribute based declaration of opcodes and instruction handlers
- Facilities to implement stacks, registers and memory models as separate components
- Used by the other libraries for parsing binary data and implementing simple VMs
