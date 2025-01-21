using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Collections;

public static class DictionaryExtensions
{
	public static TValue GetOrAdd<Tkey, TValue>(this Dictionary<Tkey, TValue> dictionary, Tkey key, TValue value)
		where Tkey : notnull
	{
		ref var val = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, key, out bool exists);
		if (!exists)
		{
			val = value;
		}
		return val;
	}

	public static TValue GetOrAdd<Tkey, TValue>(this Dictionary<Tkey, TValue> dictionary, Tkey key, Func<TValue> func)
	where Tkey : notnull
	{
		ref var val = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, key, out bool exists);
		if (!exists)
		{
			val = func();
		}
		return val;
	}

	public static bool TryUpdate<Tkey, TValue>(this Dictionary<Tkey, TValue> dictionary, Tkey key, TValue value)
		where Tkey : notnull
	{
		ref var val = ref CollectionsMarshal.GetValueRefOrNullRef(dictionary, key);
		if (Unsafe.IsNullRef(ref val)) return false;
		val = value;
		return true;	
	}
}
