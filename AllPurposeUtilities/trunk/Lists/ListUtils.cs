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

		public static List<T> Copy<T>(this List<T> list)
		{
			var result = new List<T>(list.Count);
			result.AddRange(list);
			return result;
		}

		public static List<T> Copy<T>(this List<T> list, Func<T, T> copyElement)
		{
			var result = new List<T>(list.Count);
			result.AddRange(list.Select(e => copyElement(e)));
			return result;
		}
	}
}
