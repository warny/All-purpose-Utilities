using System;
using System.Collections;
using System.Collections.Generic;

namespace Utils.VirtualMachine;

/// <summary>
/// A stack with a configurable maximum depth. Push throws <see cref="InvalidOperationException"/>
/// when the depth limit is reached; Pop and Peek throw when the stack is empty.
/// All exceptions carry VM-specific messages that include the depth limit for diagnostics.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
public sealed class BoundedStack<T> : IEnumerable<T>
{
    /// <summary>The default maximum depth when no explicit limit is provided.</summary>
    public const int DefaultMaxDepth = 1024;

    private readonly Stack<T> _inner = new();

    /// <summary>Gets the maximum number of elements that may be on the stack simultaneously.</summary>
    public int MaxDepth { get; }

    /// <summary>Gets the number of elements currently on the stack.</summary>
    public int Count => _inner.Count;

    /// <summary>
    /// Initializes a new <see cref="BoundedStack{T}"/> with the specified maximum depth.
    /// </summary>
    /// <param name="maxDepth">
    /// Maximum number of elements. Defaults to <see cref="DefaultMaxDepth"/> (<c>1024</c>).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDepth"/> is less than one.</exception>
    public BoundedStack(int maxDepth = DefaultMaxDepth)
    {
        if (maxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth must be at least 1.");
        MaxDepth = maxDepth;
    }

    /// <summary>
    /// Pushes an element onto the stack.
    /// </summary>
    /// <param name="item">The element to push.</param>
    /// <exception cref="VmLimitExceededException">
    /// Thrown when the stack already contains <see cref="MaxDepth"/> elements.
    /// </exception>
    public void Push(T item)
    {
        if (_inner.Count >= MaxDepth)
            throw new VmLimitExceededException(VmLimitKind.OperandStackDepth, MaxDepth, _inner.Count + 1L);
        _inner.Push(item);
    }

    /// <summary>
    /// Removes and returns the element at the top of the stack.
    /// </summary>
    /// <returns>The element removed from the top.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the stack is empty.</exception>
    public T Pop()
    {
        if (_inner.Count == 0)
            throw new InvalidOperationException("Operand stack underflow: Pop called on an empty stack.");
        return _inner.Pop();
    }

    /// <summary>
    /// Returns the element at the top of the stack without removing it.
    /// </summary>
    /// <returns>The element at the top.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the stack is empty.</exception>
    public T Peek()
    {
        if (_inner.Count == 0)
            throw new InvalidOperationException("Operand stack underflow: Peek called on an empty stack.");
        return _inner.Peek();
    }

    /// <summary>
    /// Attempts to remove and return the element at the top of the stack.
    /// </summary>
    /// <param name="result">The element removed, or the default value when the stack is empty.</param>
    /// <returns><see langword="true"/> if an element was removed; <see langword="false"/> when empty.</returns>
    public bool TryPop(out T result) => _inner.TryPop(out result!);

    /// <summary>
    /// Attempts to return the element at the top of the stack without removing it.
    /// </summary>
    /// <param name="result">The element at the top, or the default value when the stack is empty.</param>
    /// <returns><see langword="true"/> if an element was found; <see langword="false"/> when empty.</returns>
    public bool TryPeek(out T result) => _inner.TryPeek(out result!);

    /// <summary>Removes all elements from the stack.</summary>
    public void Clear() => _inner.Clear();

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();
}
