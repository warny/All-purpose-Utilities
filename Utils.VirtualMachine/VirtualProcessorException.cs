using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.VirtualMachine;

/// <summary>
/// Represents errors that occur while executing a <see cref="VirtualProcessor{T}"/>.
/// </summary>
/// <remarks>
/// This exception is not marked <c>[Serializable]</c> because the custom diagnostic
/// fields (<see cref="InstructionPointer"/>, <see cref="OpcodeBytes"/>, and
/// <see cref="InstructionName"/>) have no corresponding serialization constructor or
/// <c>GetObjectData</c> override. Cross-process fault reporting should use a dedicated
/// diagnostic DTO instead of binary exception serialization.
/// </remarks>
public class VirtualProcessorException : VirtualMachineException
{
    /// <summary>Gets the byte offset in the instruction stream where the error occurred, if available.</summary>
    public int? InstructionPointer { get; }

    /// <summary>Gets the opcode bytes that triggered the error, if available.</summary>
    /// <remarks>Each access returns a new defensive copy so callers cannot modify the stored diagnostic data.</remarks>
    public byte[]? OpcodeBytes => _opcodeBytes is null ? null : (byte[])_opcodeBytes.Clone();

    private readonly byte[]? _opcodeBytes;

    /// <summary>Gets the human-readable name of the instruction that caused the error, if available.</summary>
    public string? InstructionName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualProcessorException"/> class.
    /// </summary>
    public VirtualProcessorException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualProcessorException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public VirtualProcessorException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualProcessorException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public VirtualProcessorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance with the instruction position and opcode bytes that caused the error.
    /// Used when an unrecognised or truncated opcode is encountered.
    /// </summary>
    /// <param name="instructionPointer">Byte offset in the instruction stream where the error occurred.</param>
    /// <param name="opcodeBytes">Bytes read before the error was detected.</param>
    public VirtualProcessorException(int instructionPointer, IReadOnlyList<byte> opcodeBytes)
        : base(BuildMessage(instructionPointer, opcodeBytes))
    {
        InstructionPointer = instructionPointer;
        _opcodeBytes = [.. opcodeBytes];
    }

    /// <summary>
    /// Initializes a new instance with the instruction position, opcode bytes, and an inner exception.
    /// Used when a matched instruction's operand read fails due to a truncated stream.
    /// </summary>
    /// <param name="instructionPointer">Byte offset in the instruction stream where the error occurred.</param>
    /// <param name="opcodeBytes">Bytes of the matched opcode.</param>
    /// <param name="innerException">The exception raised while reading the operand bytes.</param>
    public VirtualProcessorException(int instructionPointer, IReadOnlyList<byte> opcodeBytes, Exception innerException)
        : base(BuildMessage(instructionPointer, opcodeBytes), innerException)
    {
        InstructionPointer = instructionPointer;
        _opcodeBytes = [.. opcodeBytes];
    }

    /// <summary>
    /// Initializes a new instance with the instruction position, opcode bytes, instruction name, and an inner exception.
    /// Used when a matched and named instruction's operand read fails due to a truncated stream.
    /// </summary>
    /// <param name="instructionPointer">Byte offset in the instruction stream where the error occurred.</param>
    /// <param name="opcodeBytes">Bytes of the matched opcode.</param>
    /// <param name="instructionName">Human-readable name of the instruction that failed.</param>
    /// <param name="innerException">The exception raised while reading the operand bytes.</param>
    public VirtualProcessorException(int instructionPointer, IReadOnlyList<byte> opcodeBytes, string? instructionName, Exception innerException)
        : base(BuildMessage(instructionPointer, opcodeBytes, instructionName), innerException)
    {
        InstructionPointer = instructionPointer;
        _opcodeBytes = [.. opcodeBytes];
        InstructionName = instructionName;
    }

    private static string BuildMessage(int instructionPointer, IReadOnlyList<byte> opcodeBytes, string? instructionName = null)
    {
        var opStr = string.Join(", ", opcodeBytes.Select(b => $"0x{b:X2}"));
        return instructionName is null
            ? $"Instruction error at position {instructionPointer}: [{opStr}]."
            : $"Instruction '{instructionName}' error at position {instructionPointer}: [{opStr}].";
    }
}
