using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Utils.Expressions.ExpressionBuilders;

namespace Utils.Collections;

/// <summary>
/// Represents a skip list, which is a probabilistic data structure that allows for fast search, insertion, and deletion operations.
/// </summary>
/// <typeparam name="T">The type of elements in the skip list.</typeparam>
public class SkipList<T> : ICollection<T>
{
	private readonly IComparer<T> comparer;

	/// <summary>
	/// Maximum number of nodes we traverse at a given level before forcing a new
	/// structure node to be created in the upper level (if the next node has no Up link).
	/// </summary>
	private readonly int _threshold;

	/// <summary>
	/// Points to the leftmost element in the top level.
	/// Once we get to the bottom level (following Sub links), we can traverse horizontally
	/// to enumerate all items in ascending order.
	/// </summary>
	private Element _firstElement;

	/// <summary>
	/// Points to the rightmost element in the top level.
	/// Once we get to the bottom level (following Sub links), we can traverse horizontally
	/// leftwards or do other operations if needed.
	/// </summary>
	private Element _lastElement;

	/// <summary>
	/// Initializes a new instance of the <see cref="SkipList{T}"/> class
	/// using the default comparer and a threshold of 10.
	/// </summary>
	public SkipList() : this(Comparer<T>.Default, 10) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="SkipList{T}"/> class
	/// with the specified threshold.
	/// </summary>
	/// <param name="threshold">
	/// The maximum distance at a given level before forcing the creation of a structure node.
	/// Must be &gt;= 2.
	/// </param>
	public SkipList(int threshold) : this(Comparer<T>.Default, threshold) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="SkipList{T}"/> class
	/// with the specified comparer and threshold.
	/// </summary>
	/// <param name="comparer">The comparer to use when comparing elements.</param>
	/// <param name="threshold">
	/// The maximum number of nodes to traverse at a given level before forcing
	/// the creation of a structure node. Must be &gt;= 2.
	/// </param>
	public SkipList(IComparer<T> comparer, int threshold = 10)
	{
		if (threshold < 2)
			throw new ArgumentOutOfRangeException(nameof(threshold), "Density must be between 0.001 and 0.5.");

		this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
		this._threshold = threshold;
	}

	/// <summary>
	/// Gets the number of elements contained in the skip list.
	/// </summary>
	public int Count { get; private set; }

	/// <summary>
	/// Gets a value indicating whether the skip list is read-only (always <see langword="false"/>).
	/// </summary>
	public bool IsReadOnly => false;

	/// <summary>
	/// Adds an element to the skip list at the appropriate position.
	/// If the list is empty, the element becomes the first and last element.
	/// Otherwise, we locate the insertion point and insert accordingly.
	/// If the element is inserted before <see cref="_firstElement"/>, it becomes the new first.
	/// If it's inserted after <see cref="_lastElement"/>, it becomes the new last.
	/// Otherwise, it is inserted in between two existing nodes at the bottom level.
	/// </summary>
	/// <param name="item">The element to add.</param>
	public void Add(T item)
	{
		var newElement = new Element(item);
		if (_firstElement is null)
		{
			_firstElement = newElement;
			_lastElement = newElement;
			Count = 1;
			return;
		}

		var (elementBefore, elementAfter) = FindElementPosition(item);
		if (elementBefore is null)
		{
			// add the new element before the first element
			 elementAfter.InsertBefore(newElement);

			// make it the first element in the object hierarchy
			for (var levelElement = elementAfter?.Up; levelElement != null; levelElement = levelElement.Up)
			{
				newElement.CreateUp(null, levelElement);
			}
			elementAfter?.RemoveUp();
			_firstElement = newElement;
		}
		else if (elementAfter is null)
		{
			//add the new element after the last element
			elementBefore.InsertAfter(newElement);
			// make it the last element in the object hierarchy
			for (var levelElement = elementBefore?.Up; levelElement != null; levelElement = levelElement.Up)
			{
				newElement = newElement.CreateUp(levelElement, null);
			}
			elementBefore?.RemoveUp();
			_lastElement = newElement;
		}
		else
		{
			elementBefore.InsertAfter(newElement);
		}
		Count++;
	}

	/// <summary>
	/// Removes all elements from the skip list.
	/// </summary>
	public void Clear()
	{
		_firstElement = null;
		_lastElement = null;
		Count = 0;
	}

	/// <summary>
	/// Determines whether the skip list contains a specific element.
	/// </summary>
	/// <param name="item">The element to locate in the skip list.</param>
	/// <returns><see langword="true"/> if the element is found; otherwise, <see langword="false"/>.</returns>
	public bool Contains(T item)
	{
		var (elementBefore, elementAfter) = FindElementPosition(item);
		return elementBefore is not null && elementAfter is not null && elementBefore == elementAfter;
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
		var (elementBefore, elementAfter) = FindElementPosition(item);
		if (elementBefore != elementAfter) return false;
		var element = elementBefore;

		if (element.Previous is null && element.Next is not null)
		{
			var next = element.Next;
			for (var levelElement = element?.Up; levelElement != null; levelElement = levelElement.Up)
			{
				next = next.CreateUp(levelElement, levelElement.Next);
			}
		}
		else if (element.Next is null && element.Previous is not null)
		{
			var previous = element.Previous;
			for (var levelElement = element?.Up; levelElement != null; levelElement = levelElement.Up)
			{
				previous = previous.CreateUp(levelElement.Previous, levelElement);
			}
		}
		if (element == _firstElement) _firstElement = element.Next;
		if (element == _lastElement) _lastElement = element.Previous;
		element.Remove();
		while (_firstElement?.Sub is not null && _lastElement?.Sub is not null && _firstElement.Next == _lastElement)
		{
			_firstElement = _firstElement.Sub;
			_lastElement = _lastElement.Sub;
			_firstElement.Up.Remove();
			_lastElement.Up.Remove();
		}
		Count--;
		if (Count == 0)
		{
			_firstElement = null;
			_lastElement = null;
		}
		return true;
	}

	public IEnumerator<T> GetEnumerator() => Enumerate().GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>
	/// Finds the position where 'value' should be inserted:
	/// (ElementBefore, ElementAfter). If they are the same, it means we've found
	/// a match for 'value'. If 'ElementBefore' is null => insertion is at the front.
	/// If 'ElementAfter' is null => insertion is at the end.
	/// </summary>
	private (Element ElementBefore, Element ElementAfter) FindElementPosition(T value)
	{
		Element startElement = _firstElement;
		Element endElement = _lastElement;

		while(true)
		{
			(startElement, endElement) = FindElementPositionAtLevel(startElement, endElement, value);
			if (startElement?.Sub == null && endElement?.Sub == null) return (startElement, endElement);
			startElement = startElement?.Sub;
			endElement = endElement?.Sub;
		}

	}

	/// <summary>
	/// Finds, at the current level (from 'startElement' to 'endElement'),
	/// the two nodes that sandwich 'value'. If 'value' matches one node's Value,
	/// that node is returned in both 'ElementBefore' and 'ElementAfter'.
	/// 
	/// Along the way, if we traverse more than 'threshold' nodes
	/// without encountering a skip node, we create a new skip node in the upper level.
	/// </summary>
	private (Element ElementBefore, Element ElementAfter) FindElementPositionAtLevel(Element startElement, Element endElement, T value)
	{
		if (startElement == null) return (startElement, endElement);

		Element currentElement;
		Element previousElement = startElement?.Previous;
		int counter = 0;

		for (currentElement = startElement; currentElement != null; currentElement = currentElement.Next)
		{
			if (currentElement.Up is not null) counter = 0;
			if (counter > _threshold && currentElement.Next is not null && currentElement.Next?.Up is null)
			{
				CreateNewSkipNode(startElement, endElement, currentElement);
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

	private void CreateNewSkipNode(Element startElement, Element endElement, Element currentElement)
	{
		Debug.Print($"CreateNewSkipNode({startElement}, {endElement}, {currentElement})");
		Element previousUp, nextUp;

		if (startElement.Up is null)
		{
			// create new upper level
			previousUp = _firstElement.CreateUp(null, null);
			_firstElement = previousUp;
			nextUp = _lastElement.CreateUp(null, null);
			_lastElement = nextUp;
			_firstElement.InsertAfter(_lastElement);
		}
		else
		{
			// insert into existing upper level
			previousUp = startElement.Up;
			nextUp = endElement.Up;
		}
		currentElement.CreateUp(previousUp, nextUp);
	}

	private IEnumerable<T> Enumerate()
	{
		var element = _firstElement;
		while (element?.Sub is not null) element = element.Sub;
		while (element is not null)
		{
			yield return element.Value;
			element = element.Next;
		}
	}

	/// <summary>
	/// Represents a node in the skip list, with horizontal links (Previous, Next)
	/// and vertical links (Up, Sub).
	/// </summary>
	private sealed class Element
	{
		public T Value { get; }
		public Element Next { get; private set; } = null;
		public Element Previous { get; private set; } = null;
		public Element Sub { get; private set; } = null;
		public Element Up { get; private set; } = null;

		public Element(T value)
		{
			this.Value = value;
		}

		private Element(T value, Element sub)
		{
			this.Value = value;
			this.Sub = sub;
		}

		public void Remove()
		{
			Up?.Remove();
			var previous = Previous;
			var next = Next;
			if (Previous is not null) Previous.Next = next;
			if (Next is not null) Next.Previous = previous;
			if (Sub is not null) Sub.Up = null;
		}

		public void RemoveUp()
		{
			if (Previous is not null && Next is not null)
			{
				Up?.Remove();
			}
		}

		public void InsertAfter(Element element)
		{
			element.Next = this.Next;
			if (element.Next is not null) element.Next.Previous = element;
			this.Next = element;
			element.Previous = this;
		}

		public void InsertBefore(Element element)
		{
			element.Previous = this.Previous;
			if (element.Previous is not null) element.Previous.Next = element;
			this.Previous = element;
			element.Next = this;
		}

		public Element CreateUp(Element elementBefore, Element elementAfter)
		{
			if (Up is not null) return Up;
			Element element = new(Value, this );
			this.Up = element;
			element.Previous = elementBefore;
			if (elementBefore is not null) elementBefore.Next = element;
			element.Next = elementAfter;
			if (elementAfter is not null) elementAfter.Previous = element;
			return element;
		}

		public override string ToString() => $"{{ Value = {Value}, Up = {(Up == null ? "null" : Up.Value)}, Sub = {(Sub == null ? "null" : Sub.Value)}, Prev = {(Previous == null ? "null" : Previous.Value)}, Next = {(Next == null ? "null" : Next.Value)}}}";
	}
}
