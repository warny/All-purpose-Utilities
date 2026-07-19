using System;
using System.Collections.Generic;

namespace Utils.VirtualMachine;

/// <summary>
/// A context providing a strongly-typed operand stack, avoiding boxing for value types.
/// Use this instead of <see cref="DefaultContext"/> when all stack values share a common type
/// (e.g. <see cref="int"/> for integer VMs, <see cref="double"/> for floating-point VMs).
/// </summary>
/// <typeparam name="TValue">Element type stored on the stack.</typeparam>
public class TypedStackContext<TValue> : Context
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TypedStackContext{TValue}"/> class with the
    /// default operand-stack depth limit (<see cref="BoundedStack{T}.DefaultMaxDepth"/>).
    /// </summary>
    /// <param name="data">The byte data containing the instruction stream.</param>
    public TypedStackContext(ReadOnlyMemory<byte> data) : base(data)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedStackContext{TValue}"/> class with a
    /// custom operand-stack depth limit.
    /// </summary>
    /// <param name="data">The byte data containing the instruction stream.</param>
    /// <param name="maxOperandStackDepth">
    /// Maximum number of values the operand stack may hold simultaneously.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxOperandStackDepth"/> is less than one.</exception>
    public TypedStackContext(ReadOnlyMemory<byte> data, int maxOperandStackDepth) : base(data)
    {
        Stack = new BoundedStack<TValue>(maxOperandStackDepth);
    }

    /// <summary>
    /// A bounded strongly-typed operand stack for storing values without boxing overhead.
    /// Push throws <see cref="InvalidOperationException"/> when the depth limit is reached;
    /// Pop and Peek throw when the stack is empty.
    /// </summary>
    public BoundedStack<TValue> Stack { get; } = new BoundedStack<TValue>();
}
