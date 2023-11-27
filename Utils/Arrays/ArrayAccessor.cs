using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Arrays;

public abstract class ArrayAccessor<T, D> : IEnumerable<T> where D : IEnumerable<T>
{
	public int[] Sizes { get; }
	public int Dimensions => Sizes.Length;

	protected D innerObject;
	protected ArrayAccessor(D obj, params int[] sizes)
	{
		this.innerObject = obj;
		this.Sizes = sizes;
		if (!CheckSize()) throw new ArgumentOutOfRangeException();
	}

	public T this[params int[] references]
	{
		get
		{
			CheckReference(references);
			return GetElement(references);
		}
		set
		{
			CheckReference(references);
			SetElement(value, references);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void CheckReference(int[] references)
	{
		if (references.Length != Sizes.Length) throw new ArgumentException("les references n'ont pas la bonne dimension", nameof(references));
		for (int i = 0; i < references.Length; i++)
		{
			if (references[i] >= Sizes[i]) throw new IndexOutOfRangeException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected abstract bool CheckSize();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected abstract T GetElement(int[] references);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected abstract void SetElement(T value, int[] references);

	public IEnumerator<T> GetEnumerator() => innerObject.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => innerObject.GetEnumerator();
}

public class ArrayAccessor<T> : ArrayAccessor<T, T[]>
{
	public int Offset { get; }

	public ArrayAccessor(T[] array, int offset, params int[] dimensions) : base(array, dimensions)
	{
		this.Offset = offset;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override bool CheckSize()
	{
		int size = 1;
		foreach (var width in Sizes)
		{
			size *= width;
		}
		size += Offset;
		return this.innerObject.Length >= size;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int Position(int[] references)
	{
		int position = 0;
		for (int i = references.Length - 1; i >= 0; i--)
		{
			position *= Sizes[i];
			position += references[i];
		}
		return position + Offset;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override T GetElement(int[] references) => this.innerObject[Position(references)];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void SetElement(T value, int[] references) => this.innerObject[Position(references)] = value;
}
