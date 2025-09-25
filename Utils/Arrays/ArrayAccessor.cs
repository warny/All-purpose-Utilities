using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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

	/// <summary>
	/// The underlying data source used by the accessor and returned when enumerating elements.
	/// </summary>
	protected D innerObject;

	/// <summary>
	/// Initializes a new instance of the <see cref="ArrayAccessor{T, D}"/> class.
	/// </summary>
	/// <param name="obj">The underlying data structure.</param>
	/// <param name="sizes">The sizes of each dimension of the array.</param>
	protected ArrayAccessor(D obj, params int[] sizes)
	{
		this.innerObject = obj ?? throw new ArgumentNullException(nameof(obj));
		this.Sizes = sizes.Arg().MustNotBeNull();
		if (!CheckSize()) throw new ArgumentOutOfRangeException(nameof(obj), "The underlying object does not match the specified dimensions.");
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
	/// <returns><see langword="true"/> if the size is valid; otherwise, <see langword="false"/>.</returns>
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

	/// <inheritdoc />
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
        /// Cache describing each dimension to accelerate offset and length calculations.
        /// </summary>
        private readonly ImmutableArray<DimensionCache> dimensionCaches;

	/// <summary>
	/// Initializes a new instance of the <see cref="ArrayAccessor{T}"/> class.
	/// </summary>
	/// <param name="array">The underlying one-dimensional array.</param>
	/// <param name="offset">The offset in the array where elements start.</param>
	/// <param name="dimensions">The sizes of each dimension of the array.</param>
	public ArrayAccessor(T[] array, int offset, params int[] dimensions) : base(array, dimensions)
	{
		this.Offset = offset.ArgMustBeGreaterOrEqualsThan(0);
                dimensionCaches = CreateDimensionCaches(dimensions);
        }

        /// <summary>
        /// Creates the cache describing each dimension of the accessor.
        /// </summary>
        /// <param name="dimensions">The sizes of each dimension.</param>
        /// <returns>The cache entries for every dimension.</returns>
        private static ImmutableArray<DimensionCache> CreateDimensionCaches(int[] dimensions)
        {
                DimensionCache[] caches = new DimensionCache[dimensions.Length];
                int elementCount = 1;

                for (int dimensionIndex = dimensions.Length - 1; dimensionIndex >= 0; dimensionIndex--)
                {
                        int size = dimensions[dimensionIndex];
                        int[] offsets = new int[size];

                        for (int index = 0; index < size; index++)
                        {
                                offsets[index] = index * elementCount;
                        }

                        caches[dimensionIndex] = new DimensionCache(offsets, elementCount, size);
                        elementCount *= size;
                }

                return [.. caches];
        }

	/// <summary>
	/// Validates that the underlying array has sufficient size for the specified dimensions.
	/// </summary>
	/// <returns><see langword="true"/> if the array has sufficient size; otherwise, <see langword="false"/>.</returns>
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
                int position = Offset;
                for (int i = 0; i < references.Length - 1; i++)
                {
                        position += dimensionCaches[i].Offsets[references[i]];
                }
                position += references[^1];
                return position;
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

	/// <summary>
	/// Retrieves a <see cref="Span{T}"/> covering the requested indexes.
	/// </summary>
	/// <param name="indexes">
	/// Indexes for each dimension. If fewer indexes than dimensions are
	/// provided, the returned span will include all elements of the
	/// remaining sub-dimensions.
	/// </param>
	/// <returns>A span over the specified section of the underlying array.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Span<T> AsSpan(params int[] indexes)
	{
                if (indexes.Length == 0)
                {
                        DimensionCache root = dimensionCaches[0];
                        return this.innerObject.AsSpan(Offset, root.ElementCount * root.Size);
                }

                indexes.ArgMustBe(a => a.Length <= Dimensions, "Too many indexes provided.");

                int position = Offset;
                for (int i = 0; i < indexes.Length; i++)
                {
                        var index = indexes[i];

                        if (index < 0 || index >= Sizes[i])
                                throw new IndexOutOfRangeException($"Index {index} is out of bounds for dimension {i}.");

                        if (i < dimensionCaches.Length - 1)
                                position += dimensionCaches[i].Offsets[index];
                        else
                                position += index;
                }

                int length = dimensionCaches[indexes.Length - 1].ElementCount;
                return this.innerObject.AsSpan(position, length);
        }

        /// <summary>
        /// Cache entry describing a single dimension.
        /// </summary>
        private readonly struct DimensionCache
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="DimensionCache"/> struct.
                /// </summary>
                /// <param name="offsets">Offsets allowing direct access to the underlying array.</param>
                /// <param name="elementCount">Number of elements contained in a single entry of the dimension.</param>
                /// <param name="size">Size of the dimension.</param>
                public DimensionCache(int[] offsets, int elementCount, int size)
                {
                        Offsets = offsets;
                        ElementCount = elementCount;
                        Size = size;
                }

                /// <summary>
                /// Gets the offsets for each element of the dimension.
                /// </summary>
                public int[] Offsets { get; }

                /// <summary>
                /// Gets the number of elements contained by a single entry of the dimension.
                /// </summary>
                public int ElementCount { get; }

                /// <summary>
                /// Gets the size of the dimension.
                /// </summary>
                public int Size { get; }
        }
}
