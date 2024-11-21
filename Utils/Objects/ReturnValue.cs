using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Utils.Objects;

/// <summary>
/// Return value with error management
/// </summary>
/// <typeparam name="T">Type of value returned</typeparam>
/// <typeparam name="E">Type of error returned</typeparam>
public class ReturnValue<T, E> where E : class
{
    /// <summary>
    /// Indique si le retour est un succès
    /// </summary>
    public bool IsSuccess => this.Error is null;
    public bool IsError => this.Error is not null;

    T Value { get; }
    E Error { get; }

    public ReturnValue(T value)
    {
        Value = value;
        Error = null;
    }

    public ReturnValue(E error)
    {
        Value = default;
        Error = error;
    }

    public void Do(
        Action<T> onSuccess,
        Action<E> onError
    )
    {
        if (IsSuccess) { onSuccess(Value); }
        else { onError(Error); }
    }

    public static implicit operator ReturnValue<T, E>(T value) => new (value);

	public static implicit operator T(ReturnValue<T, E> rv) => rv.Value;
	public static implicit operator E(ReturnValue<T, E> rv) => rv.Error;

	public override string ToString() => this.Error?.ToString() ?? this.Value.ToString();
    public override bool Equals(object obj) => this.Error is null ? this.Value.Equals(obj) : false;
	public override int GetHashCode() => this.Error?.GetHashCode() ?? this.Value.GetHashCode();

}

/// <summary>
/// Return value with error management as <see cref="string"/>
/// </summary>
/// <typeparam name="T">Type of value returned</typeparam>
public class ReturnValue<T> : ReturnValue<T, string>
{
    public ReturnValue(T value) : base(value) { }

    public ReturnValue(string error) : base(error) { }

    public static implicit operator ReturnValue<T>(T value) => new ReturnValue<T>(value);
}
