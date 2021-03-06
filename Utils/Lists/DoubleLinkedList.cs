﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Lists
{
	class DoubleLinkedList<T> : ICollection, IReadOnlyCollection<T>, ICloneable
	{
		private class InnerItem<T1>
		{
			public InnerItem(T1 value)
			{
				this.Value = value;
			}

			public T1 Value { get; }
			public InnerItem<T1> Previous { get; internal set; }
			public InnerItem<T1> Next { get; internal set; }
		}

		private InnerItem<T> first = null;
		private InnerItem<T> last = null;

		public T First => (first ?? throw new NullReferenceException()).Value;
		public T Last => (last ?? throw new NullReferenceException()).Value;

		public int Count { get; private set; }
		public object SyncRoot { get; }
		public bool IsSynchronized { get; }

		public void CopyTo(Array array, int index)
		{
			foreach (T item in this)
			{
				array.SetValue(item, index++);
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			IEnumerable<T> enumerator()
			{
				var current = this.first;
				while (current != null)
				{
					yield return current.Value;
					current = current.Next;
				}
			}
			return enumerator().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()	=> GetEnumerator();

		public IEnumerable<T> Reverse()
		{
			var current = this.last;
			while (current != null)
			{
				yield return current.Value;
				current = current.Previous;
			}
		}

		public void Head(T item)
		{
			var innerItem = new InnerItem<T>(item);
			innerItem.Next = first;
			first = innerItem;
			Count++;
		}

		public void Tail(T item)
		{
			var innerItem = new InnerItem<T>(item);
			innerItem.Previous = last;
			last = innerItem;
			Count++;
		}

		public T UnHead()
		{
			var item = first;
			first = item.Next;
			Count--;
			first.Previous = null;
			return item.Value;
		}

		public T UnTail()
		{
			var item = last;
			last = item.Previous;
			Count--;
			last.Next = null;
			return item.Value;
		}

		public object Clone()
		{
			var result = new DoubleLinkedList<T>();
			foreach (T item in this) {
				result.Tail(item);
			}
			return result;
		}

	}
}
