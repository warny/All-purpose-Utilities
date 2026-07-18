using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Identifies a method as a virtual machine instruction and stores its metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class InstructionAttribute : Attribute
{
    /// <summary>
    /// Gets the human-readable name associated with the instruction.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the byte sequence representing the instruction opcode.
    /// </summary>
    public byte[] Instruction { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InstructionAttribute"/> class.
    /// </summary>
    /// <param name="name">The descriptive name of the instruction. Must not be <see langword="null"/>, empty, or whitespace.</param>
    /// <param name="instruction">The byte sequence that identifies the instruction. Must contain at least one byte.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or whitespace-only, or when <paramref name="instruction"/> is empty.
    /// </exception>
    public InstructionAttribute(string name, params byte[] instruction)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Instruction name must not be empty or whitespace.", nameof(name));
        if (instruction.Length == 0)
            throw new ArgumentException("Instruction opcode cannot be empty.", nameof(instruction));
        Name = name;
        Instruction = instruction;
    }
}
