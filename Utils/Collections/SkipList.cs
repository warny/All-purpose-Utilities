using System;
using System.Collections;
using System.Collections.Generic;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Collections;

/// <summary>
/// Represents a skip list, which is a probabilistic data structure that allows for fast search, insertion, and deletion operations.
/// </summary>
/// <typeparam name="T">The type of elements in the skip list.</typeparam>
public class SkipList<T> : ICollection<T>
{
	private readonly Random rng = new Random();
	private readonly IComparer<T> comparer;
	private readonly double density;
	private Element firstElement;

	/// <summary>
	/// Initializes a new instance of the <see cref="SkipList{T}"/> class with the default comparer and a default density of 0.02.
	/// </summary>
	public SkipList() : this(Comparer<T>.Default, 0.02) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="SkipList{T}"/> class with the specified density.
	/// </summary>
	/// <param name="density">The probability that a new element is promoted to a higher level.</param>
	public SkipList(double density) : this(Comparer<T>.Default, density) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="SkipList{T}"/> class with the specified comparer and density.
	/// </summary>
	/// <param name="comparer">The comparer to use when comparing elements.</param>
	/// <param name="density">The probability that a new element is promoted to a higher level.</param>
	public SkipList(IComparer<T> comparer, double density = 0.02)
	{
		if (!density.Between(0.001, 0.5))
			throw new ArgumentOutOfRangeException(nameof(density), "Density must be between 0.001 and 0.5.");

		this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
		this.density = density;
	}

	/// <summary>
	/// Gets the number of elements contained in the skip list.
	/// </summary>
	public int Count { get; private set; }

	/// <summary>
	/// Gets a value indicating whether the skip list is read-only.
	/// </summary>
	public bool IsReadOnly => false;

	/// <summary>
	/// Adds an element to the skip list.
	/// </summary>
	/// <param name="item">The element to add.</param>
	public void Add(T item)
	{
		if (firstElement is null)
		{
			firstElement = new Element(item);
			Count = 1;
			return;
		}

		var position = FindInsertingPosition(item);
		if (position is null)
		{
			InsertAtStart(item);
		}
		else
		{
			InsertAtPosition(item, position);
		}
		Count++;
	}

	/// <summary>
	/// Removes all elements from the skip list.
	/// </summary>
	public void Clear()
	{
		firstElement = null;
		Count = 0;
	}

	/// <summary>
	/// Determines whether the skip list contains a specific element.
	/// </summary>
	/// <param name="item">The element to locate in the skip list.</param>
	/// <returns><see langword="true"/> if the element is found; otherwise, <see langword="false"/>.</returns>
	public bool Contains(T item)
	{
		var currentElement = firstElement;

		while (currentElement is not null)
		{
			int comparison = comparer.Compare(item, currentElement.Value);
			if (comparison == 0)
				return true;

			currentElement = Navigate(currentElement, comparison);
		}

		return false;
	}

	/// <summary>
	/// Copies the elements of the skip list to an array, starting at a particular array index.
	/// </summary>
	/// <param name="array">The destination array.</param>
	/// <param name="arrayIndex">The zero-based index in the destination array.</param>
	public void CopyTo(T[] array, int arrayIndex)
	{
		foreach (var item in this)
		{
			array[arrayIndex++] = item;
		}
	}

	/// <summary>
	/// Removes a specific element from the skip list.
	/// </summary>
	/// <param name="item">The element to remove.</param>
	/// <returns><see langword="true"/> if the element was successfully removed; otherwise, <see langword="false"/>.</returns>
	public bool Remove(T item)
	{
		var currentElement = firstElement;
		bool removed = false;

		while (currentElement is not null)
		{
			int comparison = comparer.Compare(item, currentElement.Value);
			if (comparison == 0)
			{
				RemoveElement(currentElement);
				removed = true;
				break;
			}

			currentElement = Navigate(currentElement, comparison);
		}

		if (removed)
			Count--;

		return removed;
	}

	public IEnumerator<T> GetEnumerator() => Enumerate().GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	private Stack<Element> FindInsertingPosition(T value)
	{
		var result = new Stack<Element>();
		var element = firstElement;

		while (element is not null)
		{
			var comparison = comparer.Compare(value, element.Value);
			if (comparison < 0 || element.Next is null)
			{
				result.Push(element);
				element = element.Sub;
			}
			else
			{
				element = element.Next;
			}
		}

		return result;
	}

	private void InsertAtStart(T item)
	{
		var position = new Stack<Element>();
		for (var element = firstElement; element is not null; element = element.Sub)
		{
			position.Push(element);
		}

		Element subElement = null;
		bool indexOldElement = true;

		while (position.Count > 0)
		{
			var element = position.Pop();
			Element newElement = new Element(item) { Sub = subElement };

			if (indexOldElement)
			{
				newElement.Next = element;
				newElement.Prev = null;
				element.Prev = newElement;
			}
			else
			{
				newElement.Next = element.Next;
				if (newElement.Next is not null) newElement.Next.Prev = newElement;
			}

			indexOldElement &= rng.NextDouble() <= density;
			subElement = newElement;
		}

		firstElement = subElement;
	}

	private void InsertAtPosition(T item, Stack<Element> position)
	{
		var previousElement = position.Pop();
		var newElement = new Element(item)
		{
			Next = previousElement.Next,
			Prev = previousElement
		};
		previousElement.Next = newElement;
		if (newElement.Next is not null) { newElement.Next.Prev = newElement; }

		while (rng.NextDouble() <= density)
		{
			var subElement = newElement;
			newElement = new Element(item) { Sub = subElement };
			previousElement = position.Count == 0 ? null : position.Pop();

			if (previousElement is null)
			{
				var newFirstElement = new Element(firstElement.Value)
				{
					Sub = firstElement,
					Next = newElement,
					Prev = null
				};
				newElement.Prev = newFirstElement;
				firstElement = newFirstElement;
			}
			else
			{
				newElement.Next = previousElement.Next;
				previousElement.Next = newElement;
				newElement.Prev = previousElement;
				if (newElement.Next is not null) newElement.Next.Prev = newElement;
			}
		}
	}

	private void RemoveElement(Element currentElement)
	{
		while (currentElement is not null)
		{
			if (currentElement.Prev is not null)
				currentElement.Prev.Next = currentElement.Next;

			if (currentElement.Next is not null)
				currentElement.Next.Prev = currentElement.Prev;

			currentElement = currentElement.Sub;
		}
	}

	private Element Navigate(Element element, int comparison)
	{
		if (comparison == 1)
		{
			if (element.Next is not null)
				return element.Next;

			return element.Sub;
		}

		if (element.Prev is null)
			return null;

		return element.Prev.Sub;
	}

	private IEnumerable<T> Enumerate()
	{
		var element = firstElement;
		while (element?.Sub is not null) element = element.Sub;
		while (element is not null)
		{
			yield return element.Value;
			element = element.Next;
		}
	}

	private sealed class Element
	{
		public T Value { get; }
		public Element Next { get; set; }
		public Element Prev { get; set; }
		public Element Sub { get; set; }

		public Element(T value)
		{
			Value = value;
		}

		public override string ToString() => Value.ToString();
	}
}
