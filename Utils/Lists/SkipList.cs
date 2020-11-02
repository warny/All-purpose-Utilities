using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Transactions;
using Utils.Mathematics;

namespace Utils.Lists
{
	public class SkipList<T> : ICollection<T>
	{
		private readonly Random rng = new Random();
		private readonly IComparer<T> comparer;
		private readonly double density;

		private Element firstElement;

		public SkipList() {
			this.comparer = Comparer<T>.Default;
			this.density = 0.01;
		}

		public SkipList(double density)
		{
			if (!density.Between(0.001, 0.2)) throw new ArgumentOutOfRangeException(nameof(density), "density doit être compris entre 0.001 et 0.2");
			this.comparer = Comparer<T>.Default;
			this.density = density;
		}

		public SkipList(IComparer<T> comparer, double density = 0.01)
		{
			if (!density.Between(0.001, 0.2)) throw new ArgumentOutOfRangeException(nameof(density), "density doit être compris entre 0.001 et 0.2");
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
			while (element != null)
			{
				var comparison = comparer.Compare(value, element.Value);
				if (comparison < 0)
				{
					if (lastElement == null) return null; // cas où on est sur le premier élément
					result.Push(lastElement);
					if (lastElement.Sub == null) return result;
					element = lastElement.Sub;
					lastElement = element;
				}
				else if (element.Next == null) {
					result.Push(element);
					if (element.Sub == null) return result;
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
			if (firstElement == null)
			{
				firstElement = new Element(item);
				return;
			}

			var position = FindInsertingPosition(item);
			if (position == null)
			{
				position = new Stack<Element>();
				for (var element = firstElement; element != null; element = element.Sub )
				{
					position.Push(element);
				}
				Element subElement = null;
				bool indexOldElement = true;
				while(position.Count > 0) 
				{
					var element = position.Pop();
					Element newElement = new Element(item);
					newElement.Sub = subElement;
					if (indexOldElement)
					{
						newElement.Next = element;
					}
					else
					{
						newElement.Next = element.Next;
					}
					indexOldElement = indexOldElement && rng.NextDouble() <= density;
					subElement = newElement;
				}
				firstElement = subElement;
			}
			else
			{
				var previousElement = position.Pop();
				var newElement = new Element(item);
				newElement.Next = previousElement.Next;
				previousElement.Next = newElement;
				while (rng.NextDouble() <= density)
				{
					var subElement = newElement;
					newElement = new Element(item);
					newElement.Sub = subElement;
					previousElement = position.Count == 0 ? null : position.Pop();
					if (previousElement == null)
					{
						var newFirstElement = new Element(firstElement.Value);
						newFirstElement.Sub = firstElement;
						newFirstElement.Next = newElement;
						firstElement = newFirstElement;
					}
					else
					{
						newElement.Next = previousElement.Next;
						previousElement.Next = newElement;
					}
				}
			}
		}

		public void Clear() => firstElement = null;

		public bool Contains(T item)
		{
			Element previousElement = null;
			Element currentElement = firstElement;

			while (currentElement != null)
			{
				int comparison = comparer.Compare(item, currentElement.Value);
				if (comparison == 0) return true;
				if (comparison == -1)
				{
					previousElement = currentElement;
					currentElement = currentElement.Next;
				}
				else if (previousElement == null)
				{
					return false;
				}
				else
				{
					currentElement = previousElement.Sub;
					previousElement = null;
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

		public IEnumerator<T> GetEnumerator() => new InnerEnumerator(firstElement);

		public bool Remove(T item)
		{
			throw new NotImplementedException();
		}
		IEnumerator IEnumerable.GetEnumerator() => new InnerEnumerator(firstElement);

		private class Element
		{
			public readonly T Value;
			public Element Next;
			public Element Sub;

			public Element(T value)
			{
				Value = value;
			}
		}

		private class InnerEnumerator : IEnumerator<T>
		{
			Element firstElement;
			Element currentElement;

			public InnerEnumerator(Element element)
			{
				while (element.Sub != null) element = element.Sub;
				firstElement = element;
				Reset();
			}

			public T Current => currentElement == null ? default : currentElement.Value;
			object IEnumerator.Current { get; }

			public void Dispose()
			{
				currentElement = null;
			}

			public bool MoveNext()
			{
				currentElement = currentElement == null ? firstElement : currentElement.Next;
				return currentElement != null;
			}

			public void Reset()
			{
				currentElement = null;
			}
		}
	}
}
