using System;
using System.Collections;
using System.Collections.Generic;

namespace Utils.Collections;

/// <summary>
/// Represents a skip list, which is a probabilistic data structure that allows for fast search, insertion, and deletion operations.
/// </summary>
/// <typeparam name="T">The type of elements in the skip list.</typeparam>
public class SkipList<T> : ICollection<T>
{
	private readonly IComparer<T> comparer;
	private readonly int threshold;
	private Element firstElement;
	private Element lastElement;

	/// <summary>
	/// Initializes a new instance of the <see cref="SkipList{T}"/> class with the default comparer and a default density of 0.02.
	/// </summary>
	public SkipList() : this(Comparer<T>.Default, 10) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="SkipList{T}"/> class with the specified density.
	/// </summary>
	/// <param name="density">The probability that a new element is promoted to a higher level.</param>
	public SkipList(int threshold) : this(Comparer<T>.Default, threshold) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="SkipList{T}"/> class with the specified comparer and density.
	/// </summary>
	/// <param name="comparer">The comparer to use when comparing elements.</param>
	/// <param name="density">The probability that a new element is promoted to a higher level.</param>
	public SkipList(IComparer<T> comparer, int threshold = 10)
	{
		if (threshold < 2)
			throw new ArgumentOutOfRangeException(nameof(threshold), "Density must be between 0.001 and 0.5.");

		this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
		this.threshold = threshold;
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
		var newElement = new Element(item);
		if (firstElement is null)
		{
			firstElement = newElement;
			lastElement = newElement;
			Count = 1;
			return;
		}

		var position = FindElementPosition(item);
		if (position.ElementBefore is null)
		{
			// add the new element before the first element
			newElement.Next = position.ElementAfter;

			// make it the first element in the object hierarchy
			for (var levelElement = position.ElementAfter?.Up; levelElement != null; levelElement = levelElement.Up)
			{
				//create and attach a new first element
				Element newFirst = new Element(item);
				newFirst.Next = levelElement.Next;
				newFirst.Sub = newElement;
				newElement.Up = newFirst;

				//detach the previous first element
				levelElement.Sub = null;

				//prepare for next level
				newElement = newFirst;
			}
			firstElement = newElement;
		}
		else if (position.ElementAfter is null)
		{
			//add the new element after the last element
			newElement.Previous = position.ElementBefore;
			position.ElementBefore.Next = newElement;
			// make it the last element in the object hierarchy
			for (var levelElement = position.ElementBefore?.Up; levelElement != null; levelElement = levelElement.Up)
			{
				//create and attach a new last element
				Element newLast = new Element(item);
				newLast.Previous = levelElement.Previous;
				newLast.Previous.Next = newLast;
				newLast.Sub = newElement;
				newElement.Up = newLast;

				//detach the previous first element
				levelElement.Sub.Up = null;
				levelElement.Sub = null;

				//prepare for next level
				newElement = newLast;
			}
			lastElement = newElement;
		}
		else
		{
			newElement.Next = position.ElementBefore.Next;
			newElement.Previous = position.ElementBefore;
			position.ElementBefore.Next = newElement;
		}
		Count++;
	}

	/// <summary>
	/// Removes all elements from the skip list.
	/// </summary>
	public void Clear()
	{
		firstElement = null;
		lastElement = null;
		Count = 0;
	}

	/// <summary>
	/// Determines whether the skip list contains a specific element.
	/// </summary>
	/// <param name="item">The element to locate in the skip list.</param>
	/// <returns><see langword="true"/> if the element is found; otherwise, <see langword="false"/>.</returns>
	public bool Contains(T item)
	{
		var elementPosition = FindElementPosition(item);
		return elementPosition.ElementBefore is not null && elementPosition.ElementAfter is not null && elementPosition.ElementBefore == elementPosition.ElementAfter;
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
		var elementPosition = FindElementPosition(item);
		if (elementPosition.ElementBefore != elementPosition.ElementAfter) return false;

		for (var element = elementPosition.ElementBefore; element != null; element = element.Up)
		{
			Element previous = element.Previous;
			Element next = element.Next;

			if (element == firstElement) firstElement = element.Next;
			if (element == lastElement) lastElement = element.Previous;

			if (previous is not null) previous.Next = next;
			if (next is not null) next.Previous = previous;
		}

		Count--;
		return true;
	}

	public IEnumerator<T> GetEnumerator() => Enumerate().GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	private (Element ElementBefore, Element ElementAfter) FindElementPosition(T value)
	{
		Element startElement = firstElement;
		Element endElement = lastElement;

		while(true)
		{
			(startElement, endElement) = FindElementPositionAtLevel(startElement, endElement, value);
			if (startElement?.Sub == null && endElement?.Sub == null) return (startElement, endElement);
			startElement = startElement?.Sub;
			endElement = endElement?.Sub;
		}

	}

	private (Element ElementBefore, Element ElementAfter) FindElementPositionAtLevel(Element startElement, Element endElement, T value)
	{
		if (startElement == null) return (startElement, endElement);

		Element currentElement;
		Element previousElement = startElement?.Previous;
		int counter = 0;

		for (currentElement = startElement; currentElement != null; currentElement = currentElement.Next)
		{
			if (currentElement.Up is not null) counter = 0;
			if (counter > threshold && currentElement.Next is not null && currentElement.Next?.Up is null)
			{
				Element previousUp, nextUp;

				Element newElement = new Element(currentElement.Value)
				{
					Sub = currentElement
				};
				if (startElement.Up is null)
				{
					// create new upper level
					previousUp = new Element(firstElement.Value) { Sub = firstElement };
					firstElement = previousUp;
					nextUp = new Element(lastElement.Value) { Sub = lastElement };
					lastElement = nextUp;
					firstElement.Next = lastElement;
				}
				else
				{
					// insert into existing upper level
					previousUp = startElement.Up;
					nextUp = endElement.Up;
				}
				newElement.Previous = previousUp;
				newElement.Next = nextUp;
				counter = 0;
			}
			int comparison = comparer.Compare(value, currentElement.Value);
			if (comparison == 0) return (currentElement, currentElement);
			if (comparison < 0)
			{
				return (currentElement.Previous, currentElement);
			}
			previousElement = currentElement;
			counter++;
		}
		return (previousElement, null);
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

	private sealed class Element(T value)
	{
		private Element _previous, _next;
		private Element _up, _sub;


		public T Value { get; } = value;
		public Element Next {
			get => _next;
			set {
				if (_next is not null) _next._previous = null;
				_next = value;
				if (value is not null) value._previous = this;
			}
		}

		public Element Previous {
			get => _previous;
			set {
				if (_previous is not null) _previous.Next = null;
				_previous = value;
				if (value is not null) value._next = this;
			}
		}
		public Element Sub {
			get => _sub;
			set {
				if (_sub is not null) _sub._up = null;
				_sub = value;
				if (value is not null) value._up = this;
			}
		}
		public Element Up {
			get => _up;
			set {
				if (_up is not null) _up._sub = null;
				_up = value;
				if (value is not null) value._sub = this;
			}
		}

		public override string ToString() => $"{{ Value = {Value}, Up = {(Up == null ? "null" : Up.Value)}, Sub = {(Sub == null ? "null" : Sub.Value)}, Prev = {(Previous == null ? "null" : Previous.Value)}, Next = {(Next == null ? "null" : Next.Value)}}}";
	}
}
