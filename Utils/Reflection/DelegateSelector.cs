using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Utils.Reflection
{
	public class DelegateSelector<TArg, TResult> : IEnumerable<Delegate>
	{
		private readonly Dictionary<Type, Delegate> delegates = new Dictionary<Type, Delegate>();

		public void Add<T>(Func<T, TResult> function) where T : TArg
		{
			delegates.Add(typeof(T), function);
		}

		public bool TryInvoke(TArg arg, out TResult result)
		{
			Type argType = arg.GetType();
			Delegate @delegate = null;
			while (!delegates.TryGetValue(argType, out @delegate))
			{
				argType = argType.BaseType;
			}
			if (!(@delegate is null))
			{
				result = (TResult)@delegate.DynamicInvoke(arg);
				return true;
			}
			result = default(TResult);
			return false;
		}

		public TResult Invoke(TArg arg)
		{
			if (TryInvoke(arg, out var result))
			{
				return result;
			}
			throw new MissingMethodException($"Le type {arg.GetType()} n'est pas supporté");
		}

		public IEnumerator<Delegate> GetEnumerator() => delegates.Values.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => delegates.Values.GetEnumerator();
	}
}
