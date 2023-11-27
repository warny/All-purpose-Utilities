using System;
using System.Collections;
using System.Collections.Generic;
using Utils.Mathematics;

namespace Utils.Collections;

public class SkipList<T> : ICollection<T>
{
	private readonly Random rng = new Random();
	private readonly IComparer<T> comparer;
	private readonly double density;

	private Element firstElement;

	public SkipList()
	{
		this.comparer = Comparer<T>.Default;
		this.density = 0.02;
	}

	public SkipList(double density)
	{
		if (!density.Between(0.001, 0.5)) throw new ArgumentOutOfRangeException(nameof(density), "density doit être compris entre 0.001 et 0.5");
		this.comparer = Comparer<T>.Default;
		this.density = density;
	}

	public SkipList(IComparer<T> comparer, double density = 0.02)
	{
		if (!density.Between(0.001, 0.5)) throw new ArgumentOutOfRangeException(nameof(density), "density doit être compris entre 0.001 et 0.5");
		this.comparer = comparer;
		this.density = density;
	}

	public int Count { get; }
	public bool IsReadOnly { get; }

	private Stack<Element> FindInsertingPosition(T value)
	{
		var result = new Stack<Element>();
		var element = firstElement;
		Element lastElement = null;
		while (element is not null)
		{
			var comparison = comparer.Compare(value, element.Value);
			if (comparison < 0)
			{
				if (lastElement is null) return null; // cas où on est sur le premier élément
				result.Push(lastElement);
				if (lastElement.Sub is null) return result;
				element = lastElement.Sub;
				lastElement = element;
			}
			else if (element.Next is null)
			{
				result.Push(element);
				if (element.Sub is null) return result;
				element = element.Sub;
				lastElement = element;
			}
			else
			{
				lastElement = element;
				element = element.Next;
			}
		}
		return result;
	}

	public void Add(T item)
	{
		if (firstElement is null)
		{
			firstElement = new Element(item);
			return;
		}

		var position = FindInsertingPosition(item);
		if (position is null)
		{
			position = new Stack<Element>();
			for (var element = firstElement; element is not null; element = element.Sub)
			{
				position.Push(element);
			}
			Element subElement = null;
			bool indexOldElement = true;
			while (position.Count > 0)
			{
				var element = position.Pop();
				Element newElement = new Element(item);
				newElement.Sub = subElement;
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
				indexOldElement = indexOldElement && rng.NextDouble() <= density;
				subElement = newElement;
			}
			firstElement = subElement;
		}
		else
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
				newElement = new Element(item);
				newElement.Sub = subElement;
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
	}

	public void Clear() => firstElement = null;

	public bool Contains(T item)
	{
		Element currentElement = firstElement;

		while (currentElement is not null)
		{
			int comparison = comparer.Compare(item, currentElement.Value);
			if (comparison == 0) return true;
			else if (comparison == 1 && currentElement.Next is not null)
			{
				currentElement = currentElement.Next;
			}
			else if (comparison == 1)
			{
				currentElement = currentElement.Sub;
			}
			else if (currentElement.Prev is null)
			{
				return false;
			}
			else
			{
				currentElement = currentElement.Prev.Sub;
			}
		}
		return false;
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		foreach (var item in this)
		{
			array[arrayIndex] = item;
			arrayIndex++;
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public IEnumerator<T> GetEnumerator()
	{
		IEnumerable<T> enumerator()
		{
			var element = this.firstElement;
			while (element.Sub is not null) element = element.Sub;
			while (element is not null)
			{
				yield return element.Value;
				element = element.Next;
			}
		}

		return enumerator().GetEnumerator();
	}

	public bool Remove(T item)
	{
		Element currentElement = firstElement;

		while (currentElement is not null)
		{
			int comparison = comparer.Compare(item, currentElement.Value);
			if (comparison == 0)
			{
				if (currentElement.Prev is null)
				{
					var first = new Stack<Element>();
					for (var element = firstElement; element is not null; element = element.Sub)
					{
						first.Push(element);
					}

					var firstIndex = first.Pop();
					var second = firstIndex.Next;
					Element firstSub = null;

					while (true)
					{
						var next = firstIndex.Next;
						if (next is null) break;
						if (comparer.Compare(next.Value, second.Value) == 0)
						{
							second.Prev = null;
							second.Sub = firstSub;
							firstSub = next;
						}
						else
						{
							var newFirst = new Element(second.Value);
							newFirst.Sub = firstSub;
							newFirst.Next = firstIndex.Next;
							if (firstIndex.Next is not null) firstIndex.Next.Prev = firstIndex;
							firstSub = newFirst;
						}
						if (first.Count == 0) break;
						firstIndex = first.Pop();
					}
					firstElement = firstSub;
					return true;
				}
				currentElement.Prev.Next = currentElement.Next;
				if (currentElement.Next is not null) currentElement.Next.Prev = currentElement.Prev;
				if (currentElement.Sub is null) return true;
				currentElement = currentElement.Sub;
			}
			else if (comparison == 1 && currentElement.Next is not null)
			{
				currentElement = currentElement.Next;
			}
			else if (comparison == 1)
			{
				currentElement = currentElement.Sub;
			}
			else if (currentElement.Prev == null)
			{
				return false;
			}
			else
			{
				currentElement = currentElement.Prev.Sub;
			}
		}
		return false;
	}


	private sealed class Element
	{
		public readonly T Value;
		public Element Next;
		public Element Prev;
		public Element Sub;

		public Element(T value) => Value = value;
		public override string ToString() => Value.ToString();
	}
}
