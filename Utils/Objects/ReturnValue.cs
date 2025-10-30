using System;

namespace Utils.Objects;

/// <summary>
/// Represents an operation result that can contain either a value or an error object.
/// </summary>
/// <typeparam name="T">Type of the returned value.</typeparam>
/// <typeparam name="E">Type of the returned error.</typeparam>
public class ReturnValue<T, E>
        where E : class
{
    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess => this.Error is null;

    /// <summary>
    /// Gets a value indicating whether the operation captured an error.
    /// </summary>
    public bool IsError => this.Error is not null;

    /// <summary>
    /// Gets the value returned by the operation.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Gets the error associated with the operation when <see cref="IsError"/> is <see langword="true"/>.
    /// </summary>
    public E Error { get; }

    /// <summary>
    /// Initializes a successful <see cref="ReturnValue{T, E}"/> instance that carries a value.
    /// </summary>
    /// <param name="value">The value produced by the operation.</param>
    public ReturnValue(T value)
    {
        Value = value;
        Error = null;
    }

    /// <summary>
    /// Initializes an error <see cref="ReturnValue{T, E}"/> instance that carries an error object.
    /// </summary>
    /// <param name="error">The error produced by the operation.</param>
    public ReturnValue(E error)
    {
        Value = default;
        Error = error;
    }

    /// <summary>
    /// Executes the appropriate callback depending on whether the instance represents success or failure.
    /// </summary>
    /// <param name="onSuccess">Callback executed when <see cref="IsSuccess"/> is <see langword="true"/>.</param>
    /// <param name="onError">Callback executed when <see cref="IsError"/> is <see langword="true"/>.</param>
    public void Do(
            Action<T> onSuccess,
            Action<E> onError)
    {
        if (IsSuccess)
            onSuccess(Value);
        else
            onError(Error);
    }

    /// <summary>
    /// Creates a successful <see cref="ReturnValue{T, E}"/> from a value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    public static implicit operator ReturnValue<T, E>(T value) => new(value);

    /// <summary>
    /// Extracts the value stored inside a <see cref="ReturnValue{T, E}"/>.
    /// </summary>
    /// <param name="rv">The <see cref="ReturnValue{T, E}"/> to extract the value from.</param>
    /// <returns>The contained value.</returns>
    public static implicit operator T(ReturnValue<T, E> rv) => rv.Value;

    /// <summary>
    /// Extracts the error stored inside a <see cref="ReturnValue{T, E}"/>.
    /// </summary>
    /// <param name="rv">The <see cref="ReturnValue{T, E}"/> to extract the error from.</param>
    /// <returns>The contained error.</returns>
    public static implicit operator E(ReturnValue<T, E> rv) => rv.Error;

    /// <inheritdoc />
    public override string ToString() => this.Error?.ToString() ?? this.Value?.ToString() ?? string.Empty;

    /// <inheritdoc />
    public override bool Equals(object obj)
            => this.Error is null ? Equals(Value, obj) : ReferenceEquals(Error, obj);

    /// <inheritdoc />
    public override int GetHashCode() => this.Error?.GetHashCode() ?? this.Value?.GetHashCode() ?? 0;
}

/// <summary>
/// Specialized <see cref="ReturnValue{T, E}"/> that stores errors as strings.
/// </summary>
/// <typeparam name="T">Type of the returned value.</typeparam>
public class ReturnValue<T> : ReturnValue<T, string>
{
    /// <summary>
    /// Initializes a successful <see cref="ReturnValue{T}"/> with the provided value.
    /// </summary>
    /// <param name="value">The value produced by the operation.</param>
    public ReturnValue(T value) : base(value) { }

    /// <summary>
    /// Initializes a failed <see cref="ReturnValue{T}"/> with the provided error message.
    /// </summary>
    /// <param name="error">The error description.</param>
    public ReturnValue(string error) : base(error) { }

    /// <summary>
    /// Creates a successful <see cref="ReturnValue{T}"/> from a value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    public static implicit operator ReturnValue<T>(T value) => new(value);
}
