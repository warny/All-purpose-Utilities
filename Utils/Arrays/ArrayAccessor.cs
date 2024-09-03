using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Utils.Objects;

namespace Utils.Arrays;

/// <summary>
/// Abstract class that provides a base for accessing multi-dimensional arrays using a one-dimensional storage.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
/// <typeparam name="D">The type of the underlying data structure, which must implement IEnumerable&lt;T&gt;.</typeparam>
public abstract class ArrayAccessor<T, D> : IEnumerable<T> where D : IEnumerable<T>
{
	/// <summary>
	/// Gets the sizes of each dimension of the array.
	/// </summary>
	public int[] Sizes { get; }

	/// <summary>
	/// Gets the number of dimensions in the array.
	/// </summary>
	public int Dimensions => Sizes.Length;

	protected D innerObject;

	/// <summary>
	/// Initializes a new instance of the <see cref="ArrayAccessor{T, D}"/> class.
	/// </summary>
	/// <param name="obj">The underlying data structure.</param>
	/// <param name="sizes">The sizes of each dimension of the array.</param>
	protected ArrayAccessor(D obj, params int[] sizes)
	{
		this.innerObject = obj  ?? throw new ArgumentNullException(nameof(obj));
		this.Sizes = sizes.ArgMustNotBeNull();
		if (!CheckSize()) throw new ArgumentOutOfRangeException("The underlying object does not match the specified dimensions.");
	}

	/// <summary>
	/// Gets or sets the element at the specified multi-dimensional index.
	/// </summary>
	/// <param name="references">An array of integers representing the indices in each dimension.</param>
	/// <returns>The element at the specified index.</returns>
	public T this[params int[] references]
	{
		get {
			CheckReference(references);
			return GetElement(references);
		}
		set {
			CheckReference(references);
			SetElement(value, references);
		}
	}

	/// <summary>
	/// Ensures that the provided indices are within the bounds of the array dimensions.
	/// </summary>
	/// <param name="references">An array of indices to check.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void CheckReference(int[] references)
	{
		if (references.Length != Sizes.Length) throw new ArgumentException("Reference dimensions do not match array dimensions.");
		for (int i = 0; i < references.Length; i++)
		{
			if (references[i] < 0 || references[i] >= Sizes[i])
			{
				throw new IndexOutOfRangeException($"Index {references[i]} is out of bounds for dimension {i}.");
			}
		}
	}

	/// <summary>
	/// Checks if the underlying object has a valid size based on the specified dimensions.
	/// </summary>
	/// <returns><c>true</c> if the size is valid; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected abstract bool CheckSize();

	/// <summary>
	/// Retrieves the element at the specified multi-dimensional index.
	/// </summary>
	/// <param name="references">The indices in each dimension.</param>
	/// <returns>The element at the specified index.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected abstract T GetElement(int[] references);

	/// <summary>
	/// Sets the element at the specified multi-dimensional index.
	/// </summary>
	/// <param name="value">The value to set.</param>
	/// <param name="references">The indices in each dimension.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected abstract void SetElement(T value, int[] references);

	public IEnumerator<T> GetEnumerator() => innerObject.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => innerObject.GetEnumerator();
}

/// <summary>
/// A class that provides multi-dimensional array access over a one-dimensional array.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
public class ArrayAccessor<T> : ArrayAccessor<T, T[]>
{
	/// <summary>
	/// Gets the offset used in the underlying array to access elements.
	/// </summary>
	public int Offset { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ArrayAccessor{T}"/> class.
	/// </summary>
	/// <param name="array">The underlying one-dimensional array.</param>
	/// <param name="offset">The offset in the array where elements start.</param>
	/// <param name="dimensions">The sizes of each dimension of the array.</param>
	public ArrayAccessor(T[] array, int offset, params int[] dimensions) : base(array, dimensions)
	{
		this.Offset = offset.ArgMustBeGreaterOrEqualsThan(0);
	}

	/// <summary>
	/// Validates that the underlying array has sufficient size for the specified dimensions.
	/// </summary>
	/// <returns><c>true</c> if the array has sufficient size; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override bool CheckSize()
	{
		int requiredSize = Sizes.Aggregate(1, (total, dimensionSize) => total * dimensionSize) + Offset;
		return this.innerObject.Length >= requiredSize;
	}

	/// <summary>
	/// Calculates the position in the one-dimensional array corresponding to the specified multi-dimensional indices.
	/// </summary>
	/// <param name="references">The indices in each dimension.</param>
	/// <returns>The position in the one-dimensional array.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int Position(int[] references)
	{
		int position = 0;
		for (int i = 0; i < references.Length; i++)
		{
			position = position * Sizes[i] + references[i];
		}
		return position + Offset;
	}

	/// <summary>
	/// Retrieves the element at the specified multi-dimensional index.
	/// </summary>
	/// <param name="references">The indices in each dimension.</param>
	/// <returns>The element at the specified index.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override T GetElement(int[] references) => this.innerObject[Position(references)];

	/// <summary>
	/// Sets the element at the specified multi-dimensional index.
	/// </summary>
	/// <param name="value">The value to set.</param>
	/// <param name="references">The indices in each dimension.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void SetElement(T value, int[] references) => this.innerObject[Position(references)] = value;
}
