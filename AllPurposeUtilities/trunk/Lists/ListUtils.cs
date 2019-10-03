using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Lists
{
	public static class ListUtils
	{
		public static void RemoveRange<T>(this List<T> list, params T[] elements) => RemoveRange(list, (IEnumerable<T>)elements);
		public static void RemoveRange<T>(this List<T> list, IEnumerable<T> elements)
		{
			foreach (var element in elements)
			{
				list.Remove(element);
			}
		}

		public static List<T> Copy<T>(this List<T> list, Func<T, T> copyElement = null)
		{
			var result = new List<T>(list.Count);
			result.AddRange(list.Select(e => copyElement(e) ?? e));
			return result;
		}

		public static L CopyTo<L, T>(this IEnumerable<T> list, Func<T, T> copyElement = null) 
			where L : IList<T>, new()
			where T : class
		{
			L result = new L();
			foreach (T element in list)
			{
				result.Add(copyElement?.Invoke(element) ?? element);
			}
			return result;
		}

		public static L Copy<L, T>(this L list, Func<T, T> copyElement = null)
			where L : IList<T>, new()
			where T : class
		{
			L result = new L();
			foreach (T element in list)
			{
				result.Add(copyElement?.Invoke(element) ?? element);
			}
			return result;
		}

		public static Queue<T> Copy<T>(this Queue<T> list, Func<T, T> copyElement = null)
			where T : class
		{
			Queue<T> result = new Queue<T>();
			foreach (T element in list)
			{
				result.Enqueue(copyElement?.Invoke(element) ?? element);
			}
			return result;
		}

		public static Queue<T> ToQueue<T>(this IEnumerable<T> list, Func<T, T> copyElement = null)
			where T : class
		{
			var result = new Queue<T>();
			foreach (T element in list)
			{
				result.Enqueue(copyElement?.Invoke(element) ?? element);
			}
			return result;
		}

	}
}
