using System;
using System.Numerics;

namespace Utils.Objects;

public readonly struct OneOf<T1, T2> :
	IEqualityOperators<OneOf<T1, T2>, OneOf<T1, T2>, bool>
{
	private readonly int _h;
	private readonly T1 _o1;
	private readonly T2 _o2;

	private OneOf(T1 value) { _h = 1; _o1 = value; }
	private OneOf(T2 value) { _h = 2; _o2 = value; }

	public static implicit operator OneOf<T1, T2>(T1 o) => new (o);
	public static implicit operator OneOf<T1, T2>(T2 o) => new (o);

	public static implicit operator T1(OneOf<T1, T2> oo) => oo._o1;
	public static implicit operator T2(OneOf<T1, T2> oo) => oo._o2;

	public readonly void Switch(
		Action<T1> action1,
		Action<T2> action2
	)
	{
		switch (_h)
		{
			case 1: action1?.Invoke(_o1); break;
			case 2: action2?.Invoke(_o2); break;
		}
	}

	public readonly T Switch<T>(
		Func<T1, T> func1,
		Func<T2, T> func2
	)
	{
		return _h switch
		{
			1 => func1 is null ? default : func1(_o1),
			2 => func2 is null ? default : func2(_o2),
			_ => default,
		};
	}

	public readonly override string ToString()
	{
		return _h switch
		{
			1 => _o1.ToString(),
			2 => _o2.ToString(),
			_ => string.Empty,
		};
	}

	public readonly override bool Equals(object obj)
	{
		return _h switch
		{
			1 => _o1.Equals(obj),
			2 => _o2.Equals(obj),
			_ => false,
		};
	}

	public readonly override int GetHashCode()
	{
		return _h switch
		{
			1 => _o1.GetHashCode(),
			2 => _o2.GetHashCode(),
			_ => 0,
		};
	}
	public static bool operator ==(OneOf<T1, T2> left, OneOf<T1, T2> right) => left.Equals(right);

	public static bool operator !=(OneOf<T1, T2> left, OneOf<T1, T2> right) => !(left == right);
}

public readonly struct OneOf<T1, T2, T3> :
	IEqualityOperators<OneOf<T1, T2, T3>, OneOf<T1, T2, T3>, bool>
{
	private readonly int _h;
	private readonly T1 _o1;
	private readonly T2 _o2;
	private readonly T3 _o3;

	private OneOf(T1 value) { _h = 1; _o1 = value; }
	private OneOf(T2 value) { _h = 2; _o2 = value; }
	private OneOf(T3 value) { _h = 3; _o3 = value; }

	public static implicit operator OneOf<T1, T2, T3>(T1 o) => new (o);
	public static implicit operator OneOf<T1, T2, T3>(T2 o) => new (o);
	public static implicit operator OneOf<T1, T2, T3>(T3 o) => new (o);

	public static implicit operator T1(OneOf<T1, T2, T3> oo) => oo._o1;
	public static implicit operator T2(OneOf<T1, T2, T3> oo) => oo._o2;
	public static implicit operator T3(OneOf<T1, T2, T3> oo) => oo._o3;

	public readonly void Switch(
		Action<T1> action1,
		Action<T2> action2,
		Action<T3> action3
	)
	{
		switch (_h)
		{
			case 1: action1?.Invoke(_o1); break;
			case 2: action2?.Invoke(_o2); break;
			case 3: action3?.Invoke(_o3); break;
		}
	}

	public readonly T Switch<T>(
		Func<T1, T> func1,
		Func<T2, T> func2,
		Func<T3, T> func3
	)
	{
		return _h switch
		{
			1 => func1 is null ? default : func1(_o1),
			2 => func2 is null ? default : func2(_o2),
			3 => func3 is null ? default : func3(_o3),
			_ => default,
		};
	}

	public readonly override string ToString()
	{
		return _h switch
		{
			1 => _o1.ToString(),
			2 => _o2.ToString(),
			3 => _o3.ToString(),
			_ => string.Empty,
		};
	}

	public readonly override bool Equals(object obj)
	{
		return _h switch
		{
			1 => _o1.Equals(obj),
			2 => _o2.Equals(obj),
			3 => _o3.Equals(obj),
			_ => false,
		};
	}

	public readonly override int GetHashCode()
	{
		return _h switch
		{
			1 => _o1.GetHashCode(),
			2 => _o2.GetHashCode(),
			3 => _o3.GetHashCode(),
			_ => 0,
		};
	}
	public static bool operator ==(OneOf<T1, T2, T3> left, OneOf<T1, T2, T3> right) => left.Equals(right);

	public static bool operator !=(OneOf<T1, T2, T3> left, OneOf<T1, T2, T3> right) => !(left == right);
}

public readonly struct OneOf<T1, T2, T3, T4> :
	IEqualityOperators<OneOf<T1, T2, T3, T4>, OneOf<T1, T2, T3, T4>, bool>
{
	private readonly int _h;
	private readonly T1 _o1;
	private readonly T2 _o2;
	private readonly T3 _o3;
	private readonly T4 _o4;

	private OneOf(T1 value) { _h = 1; _o1 = value; }
	private OneOf(T2 value) { _h = 2; _o2 = value; }
	private OneOf(T3 value) { _h = 3; _o3 = value; }
	private OneOf(T4 value) { _h = 4; _o4 = value; }

	public static implicit operator OneOf<T1, T2, T3, T4>(T1 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4>(T2 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4>(T3 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4>(T4 o) => new(o);

	public static implicit operator T1(OneOf<T1, T2, T3, T4> oo) => oo._o1;
	public static implicit operator T2(OneOf<T1, T2, T3, T4> oo) => oo._o2;
	public static implicit operator T3(OneOf<T1, T2, T3, T4> oo) => oo._o3;
	public static implicit operator T4(OneOf<T1, T2, T3, T4> oo) => oo._o4;

	public readonly void Switch(
		Action<T1> action1,
		Action<T2> action2,
		Action<T3> action3,
		Action<T4> action4
	)
	{
		switch (_h)
		{
			case 1: action1?.Invoke(_o1); break;
			case 2: action2?.Invoke(_o2); break;
			case 3: action3?.Invoke(_o3); break;
			case 4: action4?.Invoke(_o4); break;
		}
	}

	public readonly T Switch<T>(
		Func<T1, T> func1,
		Func<T2, T> func2,
		Func<T3, T> func3,
		Func<T4, T> func4
	)
	{
		return _h switch
		{
			1 => func1 is null ? default : func1(_o1),
			2 => func2 is null ? default : func2(_o2),
			3 => func3 is null ? default : func3(_o3),
			4 => func4 is null ? default : func4(_o4),
			_ => default,
		};
	}

	public readonly override string ToString()
	{
		return _h switch
		{
			1 => _o1.ToString(),
			2 => _o2.ToString(),
			3 => _o3.ToString(),
			4 => _o4.ToString(),
			_ => string.Empty,
		};
	}

	public readonly override bool Equals(object obj)
	{
		return _h switch
		{
			1 => _o1.Equals(obj),
			2 => _o2.Equals(obj),
			3 => _o3.Equals(obj),
			4 => _o4.Equals(obj),
			_ => false,
		};
	}

	public readonly override int GetHashCode()
	{
		return _h switch
		{
			1 => _o1.GetHashCode(),
			2 => _o2.GetHashCode(),
			3 => _o3.GetHashCode(),
			4 => _o4.GetHashCode(),
			_ => 0,
		};
	}
	public static bool operator ==(OneOf<T1, T2, T3, T4> left, OneOf<T1, T2, T3, T4> right) => left.Equals(right);

	public static bool operator !=(OneOf<T1, T2, T3, T4> left, OneOf<T1, T2, T3, T4> right) => !(left == right);
}

public readonly struct OneOf<T1, T2, T3, T4, T5> :
	IEqualityOperators<OneOf<T1, T2, T3, T4, T5>, OneOf<T1, T2, T3, T4, T5>, bool>
{
	private readonly int _h;
	private readonly T1 _o1;
	private readonly T2 _o2;
	private readonly T3 _o3;
	private readonly T4 _o4;
	private readonly T5 _o5;

	private OneOf(T1 value) { _h = 1; _o1 = value; }
	private OneOf(T2 value) { _h = 2; _o2 = value; }
	private OneOf(T3 value) { _h = 3; _o3 = value; }
	private OneOf(T4 value) { _h = 4; _o4 = value; }
	private OneOf(T5 value) { _h = 1; _o5 = value; }

	public static implicit operator OneOf<T1, T2, T3, T4, T5>(T1 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5>(T2 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5>(T3 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5>(T4 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5>(T5 o) => new(o);

	public static implicit operator T1(OneOf<T1, T2, T3, T4, T5> oo) => oo._o1;
	public static implicit operator T2(OneOf<T1, T2, T3, T4, T5> oo) => oo._o2;
	public static implicit operator T3(OneOf<T1, T2, T3, T4, T5> oo) => oo._o3;
	public static implicit operator T4(OneOf<T1, T2, T3, T4, T5> oo) => oo._o4;
	public static implicit operator T5(OneOf<T1, T2, T3, T4, T5> oo) => oo._o5;

	public readonly void Switch(
		Action<T1> action1,
		Action<T2> action2,
		Action<T3> action3,
		Action<T4> action4,
		Action<T5> action5
	)
	{
		switch (_h)
		{
			case 1: action1?.Invoke(_o1); break;
			case 2: action2?.Invoke(_o2); break;
			case 3: action3?.Invoke(_o3); break;
			case 4: action4?.Invoke(_o4); break;
			case 5: action5?.Invoke(_o5); break;
		}
	}

	public readonly T Switch<T>(
		Func<T1, T> func1,
		Func<T2, T> func2,
		Func<T3, T> func3,
		Func<T4, T> func4,
		Func<T5, T> func5
	)
	{
		return _h switch
		{
			1 => func1 is null ? default : func1(_o1),
			2 => func2 is null ? default : func2(_o2),
			3 => func3 is null ? default : func3(_o3),
			4 => func4 is null ? default : func4(_o4),
			5 => func5 is null ? default : func5(_o5),
			_ => default,
		};
	}

	public readonly override string ToString()
	{
		return _h switch
		{
			1 => _o1.ToString(),
			2 => _o2.ToString(),
			3 => _o3.ToString(),
			4 => _o4.ToString(),
			5 => _o5.ToString(),
			_ => string.Empty,
		};
	}

	public readonly override bool Equals(object obj)
	{
		return _h switch
		{
			1 => _o1.Equals(obj),
			2 => _o2.Equals(obj),
			3 => _o3.Equals(obj),
			4 => _o4.Equals(obj),
			5 => _o5.Equals(obj),
			_ => false,
		};
	}

	public readonly override int GetHashCode()
	{
		return _h switch
		{
			1 => _o1.GetHashCode(),
			2 => _o2.GetHashCode(),
			3 => _o3.GetHashCode(),
			4 => _o4.GetHashCode(),
			5 => _o5.GetHashCode(),
			_ => 0,
		};
	}
	public static bool operator ==(OneOf<T1, T2, T3, T4, T5> left, OneOf<T1, T2, T3, T4, T5> right) => left.Equals(right);

	public static bool operator !=(OneOf<T1, T2, T3, T4, T5> left, OneOf<T1, T2, T3, T4, T5> right) => !(left == right);
}
public readonly struct OneOf<T1, T2, T3, T4, T5, T6> :
	IEqualityOperators<OneOf<T1, T2, T3, T4, T5, T6>, OneOf<T1, T2, T3, T4, T5, T6>, bool>
{
	private readonly int _h;
	private readonly T1 _o1;
	private readonly T2 _o2;
	private readonly T3 _o3;
	private readonly T4 _o4;
	private readonly T5 _o5;
	private readonly T6 _o6;

	private OneOf(T1 value) { _h = 1; _o1 = value; }
	private OneOf(T2 value) { _h = 2; _o2 = value; }
	private OneOf(T3 value) { _h = 3; _o3 = value; }
	private OneOf(T4 value) { _h = 4; _o4 = value; }
	private OneOf(T5 value) { _h = 1; _o5 = value; }
	private OneOf(T6 value) { _h = 2; _o6 = value; }

	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6>(T1 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6>(T2 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6>(T3 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6>(T4 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6>(T5 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6>(T6 o) => new(o);

	public static implicit operator T1(OneOf<T1, T2, T3, T4, T5, T6> oo) => oo._o1;
	public static implicit operator T2(OneOf<T1, T2, T3, T4, T5, T6> oo) => oo._o2;
	public static implicit operator T3(OneOf<T1, T2, T3, T4, T5, T6> oo) => oo._o3;
	public static implicit operator T4(OneOf<T1, T2, T3, T4, T5, T6> oo) => oo._o4;
	public static implicit operator T5(OneOf<T1, T2, T3, T4, T5, T6> oo) => oo._o5;
	public static implicit operator T6(OneOf<T1, T2, T3, T4, T5, T6> oo) => oo._o6;

	public readonly void Switch(
		Action<T1> action1,
		Action<T2> action2,
		Action<T3> action3,
		Action<T4> action4,
		Action<T5> action5,
		Action<T6> action6
	)
	{
		switch (_h)
		{
			case 1: action1?.Invoke(_o1); break;
			case 2: action2?.Invoke(_o2); break;
			case 3: action3?.Invoke(_o3); break;
			case 4: action4?.Invoke(_o4); break;
			case 5: action5?.Invoke(_o5); break;
			case 6: action6?.Invoke(_o6); break;
		}
	}

	public readonly T Switch<T>(
		Func<T1, T> func1,
		Func<T2, T> func2,
		Func<T3, T> func3,
		Func<T4, T> func4,
		Func<T5, T> func5,
		Func<T6, T> func6
	)
	{
		if (_h == 1) return func1 is null ? default : func1(_o1);
		else if (_h == 2) return func2 is null ? default : func2(_o2);
		else if (_h == 3) return func3 is null ? default : func3(_o3);
		else if (_h == 4) return func4 is null ? default : func4(_o4);
		else if (_h == 5) return func5 is null ? default : func5(_o5);
		else if (_h == 6) return func6 is null ? default : func6(_o6);
		return default;
	}

	public readonly override string ToString()
	{
		if (_h == 1) return _o1.ToString();
		else if (_h == 2) return _o2.ToString();
		else if (_h == 3) return _o3.ToString();
		else if (_h == 4) return _o4.ToString();
		else if (_h == 5) return _o5.ToString();
		else if (_h == 6) return _o6.ToString();
		return string.Empty;
	}

	public readonly override bool Equals(object obj)
	{
		if (_h == 1) return _o1.Equals(obj);
		else if (_h == 2) return _o2.Equals(obj);
		else if (_h == 3) return _o3.Equals(obj);
		else if (_h == 4) return _o4.Equals(obj);
		else if (_h == 5) return _o5.Equals(obj);
		else if (_h == 6) return _o6.Equals(obj);
		return false;
	}

	public readonly override int GetHashCode()
	{
		if (_h == 1) return _o1.GetHashCode();
		else if (_h == 2) return _o2.GetHashCode();
		else if (_h == 3) return _o3.GetHashCode();
		else if (_h == 4) return _o4.GetHashCode();
		else if (_h == 5) return _o5.GetHashCode();
		else if (_h == 6) return _o6.GetHashCode();
		return 0;
	}
	public static bool operator ==(OneOf<T1, T2, T3, T4, T5, T6> left, OneOf<T1, T2, T3, T4, T5, T6> right) => left.Equals(right);

	public static bool operator !=(OneOf<T1, T2, T3, T4, T5, T6> left, OneOf<T1, T2, T3, T4, T5, T6> right) => !(left == right);
}

public readonly struct OneOf<T1, T2, T3, T4, T5, T6, T7> :
	IEqualityOperators<OneOf<T1, T2, T3, T4, T5, T6, T7>, OneOf<T1, T2, T3, T4, T5, T6, T7>, bool>
{
	private readonly int _h;
	
	private readonly T1 _o1;
	private readonly T2 _o2;
	private readonly T3 _o3;
	private readonly T4 _o4;
	private readonly T5 _o5;
	private readonly T6 _o6;
	private readonly T7 _o7;


	private OneOf(T1 value) { _h = 1; _o1 = value; }
	private OneOf(T2 value) { _h = 2; _o2 = value; }
	private OneOf(T3 value) { _h = 3; _o3 = value; }
	private OneOf(T4 value) { _h = 4; _o4 = value; }
	private OneOf(T5 value) { _h = 5; _o5 = value; }
	private OneOf(T6 value) { _h = 6; _o6 = value; }
	private OneOf(T7 value) { _h = 7; _o7 = value; }

	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7>(T1 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7>(T2 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7>(T3 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7>(T4 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7>(T5 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7>(T6 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7>(T7 o) => new(o);

	public static implicit operator T1(OneOf<T1, T2, T3, T4, T5, T6, T7> oo) => oo._o1;
	public static implicit operator T2(OneOf<T1, T2, T3, T4, T5, T6, T7> oo) => oo._o2;
	public static implicit operator T3(OneOf<T1, T2, T3, T4, T5, T6, T7> oo) => oo._o3;
	public static implicit operator T4(OneOf<T1, T2, T3, T4, T5, T6, T7> oo) => oo._o4;
	public static implicit operator T5(OneOf<T1, T2, T3, T4, T5, T6, T7> oo) => oo._o5;
	public static implicit operator T6(OneOf<T1, T2, T3, T4, T5, T6, T7> oo) => oo._o6;
	public static implicit operator T7(OneOf<T1, T2, T3, T4, T5, T6, T7> oo) => oo._o7;

	public readonly void Switch(
		Action<T1> action1,
		Action<T2> action2,
		Action<T3> action3,
		Action<T4> action4,
		Action<T5> action5,
		Action<T6> action6,
		Action<T7> action7
	)
	{
		switch (_h)
		{
			case 1: action1?.Invoke(_o1); break;
			case 2: action2?.Invoke(_o2); break;
			case 3: action3?.Invoke(_o3); break;
			case 4: action4?.Invoke(_o4); break;
			case 5: action5?.Invoke(_o5); break;
			case 6: action6?.Invoke(_o6); break;
			case 7: action7?.Invoke(_o7); break;
		}
	}

	public readonly T Switch<T>(
		Func<T1, T> func1,
		Func<T2, T> func2,
		Func<T3, T> func3,
		Func<T4, T> func4,
		Func<T5, T> func5,
		Func<T6, T> func6,
		Func<T7, T> func7
	)
	{
		return _h switch
		{
			1 => func1 is null ? default : func1(_o1),
			2 => func2 is null ? default : func2(_o2),
			3 => func3 is null ? default : func3(_o3),
			4 => func4 is null ? default : func4(_o4),
			5 => func5 is null ? default : func5(_o5),
			6 => func6 is null ? default : func6(_o6),
			7 => func7 is null ? default : func7(_o7),
			_ => default,
		};
	}

	public readonly override string ToString()
	{
		return _h switch
		{
			1 => _o1.ToString(),
			2 => _o2.ToString(),
			3 => _o3.ToString(),
			4 => _o4.ToString(),
			5 => _o5.ToString(),
			6 => _o6.ToString(),
			7 => _o7.ToString(),
			_ => string.Empty,
		};
	}

	public readonly override bool Equals(object obj)
	{
		return _h switch
		{
			1 => _o1.Equals(obj),
			2 => _o2.Equals(obj),
			3 => _o3.Equals(obj),
			4 => _o4.Equals(obj),
			5 => _o5.Equals(obj),
			6 => _o6.Equals(obj),
			7 => _o7.Equals(obj),
			_ => false,
		};
	}

	public readonly override int GetHashCode()
	{
		return _h switch
		{
			1 => _o1.GetHashCode(),
			2 => _o2.GetHashCode(),
			3 => _o3.GetHashCode(),
			4 => _o4.GetHashCode(),
			5 => _o5.GetHashCode(),
			6 => _o6.GetHashCode(),
			7 => _o7.GetHashCode(),
			_ => 0,
		};
	}
	public static bool operator ==(OneOf<T1, T2, T3, T4, T5, T6, T7> left, OneOf<T1, T2, T3, T4, T5, T6, T7> right) => left.Equals(right);

	public static bool operator !=(OneOf<T1, T2, T3, T4, T5, T6, T7> left, OneOf<T1, T2, T3, T4, T5, T6, T7> right) => !(left == right);
}
public readonly struct OneOf<T1, T2, T3, T4, T5, T6, T7, T8> :
	IEqualityOperators<OneOf<T1, T2, T3, T4, T5, T6, T7, T8>, OneOf<T1, T2, T3, T4, T5, T6, T7, T8>, bool>
{
	private readonly int _h;

	private readonly T1 _o1;
	private readonly T2 _o2;
	private readonly T3 _o3;
	private readonly T4 _o4;
	private readonly T5 _o5;
	private readonly T6 _o6;
	private readonly T7 _o7;
	private readonly T8 _o8;


	private OneOf(T1 value) { _h = 1; _o1 = value; }
	private OneOf(T2 value) { _h = 2; _o2 = value; }
	private OneOf(T3 value) { _h = 3; _o3 = value; }
	private OneOf(T4 value) { _h = 4; _o4 = value; }
	private OneOf(T5 value) { _h = 5; _o5 = value; }
	private OneOf(T6 value) { _h = 6; _o6 = value; }
	private OneOf(T7 value) { _h = 7; _o7 = value; }
	private OneOf(T8 value) { _h = 8; _o8 = value; }

	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T1 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T2 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T3 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T4 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T5 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T6 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T7 o) => new(o);
	public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T8 o) => new(o);

	public static implicit operator T1(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> oo) => oo._o1;
	public static implicit operator T2(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> oo) => oo._o2;
	public static implicit operator T3(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> oo) => oo._o3;
	public static implicit operator T4(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> oo) => oo._o4;
	public static implicit operator T5(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> oo) => oo._o5;
	public static implicit operator T6(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> oo) => oo._o6;
	public static implicit operator T7(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> oo) => oo._o7;
	public static implicit operator T8(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> oo) => oo._o8;

	public readonly void Switch(
		Action<T1> action1,
		Action<T2> action2,
		Action<T3> action3,
		Action<T4> action4,
		Action<T5> action5,
		Action<T6> action6,
		Action<T7> action7,
		Action<T8> action8
	)
	{
		switch (_h)
		{
			case 1: action1?.Invoke(_o1); break;
			case 2: action2?.Invoke(_o2); break;
			case 3: action3?.Invoke(_o3); break;
			case 4: action4?.Invoke(_o4); break;
			case 5: action5?.Invoke(_o5); break;
			case 6: action6?.Invoke(_o6); break;
			case 7: action7?.Invoke(_o7); break;
			case 8: action8?.Invoke(_o8); break;
		}
	}

	public readonly T Switch<T>(
		Func<T1, T> func1,
		Func<T2, T> func2,
		Func<T3, T> func3,
		Func<T4, T> func4,
		Func<T5, T> func5,
		Func<T6, T> func6,
		Func<T7, T> func7,
		Func<T8, T> func8
	)
	{
		return _h switch
		{
			1 => func1 is null ? default : func1(_o1),
			2 => func2 is null ? default : func2(_o2),
			3 => func3 is null ? default : func3(_o3),
			4 => func4 is null ? default : func4(_o4),
			5 => func5 is null ? default : func5(_o5),
			6 => func6 is null ? default : func6(_o6),
			7 => func7 is null ? default : func7(_o7),
			8 => func8 is null ? default : func8(_o8),
			_ => default,
		};
	}

	public readonly override string ToString()
	{
		return _h switch
		{
			1 => _o1.ToString(),
			2 => _o2.ToString(),
			3 => _o3.ToString(),
			4 => _o4.ToString(),
			5 => _o5.ToString(),
			6 => _o6.ToString(),
			7 => _o7.ToString(),
			8 => _o8.ToString(),
			_ => string.Empty,
		};
	}

	public readonly override bool Equals(object obj)
	{
		return _h switch
		{
			1 => _o1.Equals(obj),
			2 => _o2.Equals(obj),
			3 => _o3.Equals(obj),
			4 => _o4.Equals(obj),
			5 => _o5.Equals(obj),
			6 => _o6.Equals(obj),
			7 => _o7.Equals(obj),
			8 => _o8.Equals(obj),
			_ => false,
		};
	}

	public readonly override int GetHashCode()
	{
		return _h switch
		{
			1 => _o1.GetHashCode(),
			2 => _o2.GetHashCode(),
			3 => _o3.GetHashCode(),
			4 => _o4.GetHashCode(),
			5 => _o5.GetHashCode(),
			6 => _o6.GetHashCode(),
			7 => _o7.GetHashCode(),
			8 => _o8.GetHashCode(),
			_ => 0,
		};
	}
	public static bool operator ==(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> left, OneOf<T1, T2, T3, T4, T5, T6, T7, T8> right) => left.Equals(right);

	public static bool operator !=(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> left, OneOf<T1, T2, T3, T4, T5, T6, T7, T8> right) => !(left == right);
}