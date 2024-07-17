using System;
using System.Linq;
using System.Numerics;
using Utils.Mathematics;
using Utils.Reflection;

namespace Utils.Objects;

public static class NumberUtils
{
	/// <summary>
	/// Indique si un objet est une valeur numérique
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	public static bool IsNumeric(object value)
	{
		Type t = value.GetType();
		return t.IsDefinedBy(typeof(INumber<>));
	}

    /// <summary>
    /// Indique si un objet est une valeur numérique de base
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsBaseNumeric(object value)
    { 
		return value.GetType().In(Types.Number);
    }

}
