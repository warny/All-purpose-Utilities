using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Threading;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Records the calls made into an impersonating reflection object and, once <see cref="Armed"/>, makes every call throw: the
/// engine must neither trust nor read such an object (S5 A-10).
/// </summary>
internal sealed class ReflectionTrap
{
    /// <summary>The member names called, in any order (thread-safe).</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>The number of calls made while armed.</summary>
    private int _armedCalls;

    /// <summary>Gets or sets whether every call throws.</summary>
    public bool Armed { get; set; }

    /// <summary>Gets the number of calls made while armed.</summary>
    public int ArmedCalls => Volatile.Read(ref _armedCalls);

    /// <summary>Gets the number of evaluations of the impersonated member (its <c>Invoke</c> or <c>GetValue</c>).</summary>
    public int Evaluations => Volatile.Read(ref _evaluations);

    /// <summary>Backing field of <see cref="Evaluations"/>.</summary>
    private int _evaluations;

    /// <summary>Clears the recorded calls.</summary>
    public void Reset()
    {
        Calls.Clear();
        Interlocked.Exchange(ref _armedCalls, 0);
        Interlocked.Exchange(ref _typeReads, 0);
        Interlocked.Exchange(ref _evaluations, 0);
    }

    /// <summary>
    /// Gets the number of <c>ReturnType</c>/<c>FieldType</c>/<c>PropertyType</c> reads made while armed. The framework getter
    /// <see cref="System.Linq.Expressions.Expression.Type"/> of a call or member node delegates to them, so every engine walk
    /// that reads a node's type reaches them; they are counted, not refused (residual limitation L-1 of the A-10 note).
    /// </summary>
    public int TypeReads => Volatile.Read(ref _typeReads);

    /// <summary>Backing field of <see cref="TypeReads"/>.</summary>
    private int _typeReads;

    /// <summary>Records a call and throws when armed.</summary>
    /// <typeparam name="T">The forwarded value type.</typeparam>
    /// <param name="member">The member name.</param>
    /// <param name="value">The value forwarded from the impersonated member.</param>
    /// <returns><paramref name="value"/>.</returns>
    public T Hit<T>(string member, T value)
    {
        Calls.Enqueue(member);
        if (Armed && member is "ReturnType" or "FieldType" or "PropertyType")
        {
            Interlocked.Increment(ref _typeReads);
            return value;
        }

        if (Armed)
        {
            Interlocked.Increment(ref _armedCalls);
            throw new InvalidOperationException(member + " was called on an impersonating reflection object.");
        }

        return value;
    }

    /// <summary>Records one evaluation of the impersonated member.</summary>
    public void Evaluated() => Interlocked.Increment(ref _evaluations);
}

/// <summary>
/// A user <see cref="MethodInfo"/> that impersonates a real method: it forwards every member to it, answers
/// <see langword="true"/> when compared with it, and shares its hash code. Its <c>Invoke</c> runs the real method and counts
/// the evaluation. Every member goes through the <see cref="ReflectionTrap"/>.
/// </summary>
internal sealed class ImpersonatingMethod : MethodInfo
{
    /// <summary>The impersonated method.</summary>
    private readonly MethodInfo _real;

    /// <summary>Initializes an impersonation of <paramref name="real"/>.</summary>
    /// <param name="real">The impersonated runtime method.</param>
    public ImpersonatingMethod(MethodInfo real) => _real = real;

    /// <summary>Gets the trap recording the calls.</summary>
    public ReflectionTrap Trap { get; } = new();

    /// <inheritdoc/>
    public override string Name => Trap.Hit(nameof(Name), _real.Name);

    /// <inheritdoc/>
    public override Type? DeclaringType => Trap.Hit(nameof(DeclaringType), _real.DeclaringType);

    /// <inheritdoc/>
    public override Type? ReflectedType => Trap.Hit(nameof(ReflectedType), _real.ReflectedType);

    /// <inheritdoc/>
    public override MethodAttributes Attributes => Trap.Hit(nameof(Attributes), _real.Attributes);

    /// <inheritdoc/>
    public override RuntimeMethodHandle MethodHandle => Trap.Hit(nameof(MethodHandle), _real.MethodHandle);

    /// <inheritdoc/>
    public override ICustomAttributeProvider ReturnTypeCustomAttributes => Trap.Hit(nameof(ReturnTypeCustomAttributes), _real.ReturnTypeCustomAttributes);

    /// <inheritdoc/>
    public override Type ReturnType => Trap.Hit(nameof(ReturnType), _real.ReturnType);

    /// <inheritdoc/>
    public override ParameterInfo ReturnParameter => Trap.Hit(nameof(ReturnParameter), _real.ReturnParameter);

    /// <inheritdoc/>
    public override CallingConventions CallingConvention => Trap.Hit(nameof(CallingConvention), _real.CallingConvention);

    /// <inheritdoc/>
    public override bool IsGenericMethod => Trap.Hit(nameof(IsGenericMethod), _real.IsGenericMethod);

    /// <inheritdoc/>
    public override bool IsGenericMethodDefinition => Trap.Hit(nameof(IsGenericMethodDefinition), _real.IsGenericMethodDefinition);

    /// <inheritdoc/>
    public override bool ContainsGenericParameters => Trap.Hit(nameof(ContainsGenericParameters), _real.ContainsGenericParameters);

    /// <inheritdoc/>
    public override int MetadataToken => Trap.Hit(nameof(MetadataToken), _real.MetadataToken);

    /// <inheritdoc/>
    public override Module Module => Trap.Hit(nameof(Module), _real.Module);

    /// <inheritdoc/>
    public override Type[] GetGenericArguments() => Trap.Hit(nameof(GetGenericArguments), _real.GetGenericArguments());

    /// <inheritdoc/>
    public override MethodInfo GetBaseDefinition() => Trap.Hit(nameof(GetBaseDefinition), (MethodInfo)this);

    /// <inheritdoc/>
    public override object[] GetCustomAttributes(bool inherit) => Trap.Hit(nameof(GetCustomAttributes), _real.GetCustomAttributes(inherit));

    /// <inheritdoc/>
    public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Trap.Hit(nameof(GetCustomAttributes), _real.GetCustomAttributes(attributeType, inherit));

    /// <inheritdoc/>
    public override bool IsDefined(Type attributeType, bool inherit) => Trap.Hit(nameof(IsDefined), _real.IsDefined(attributeType, inherit));

    /// <inheritdoc/>
    public override MethodImplAttributes GetMethodImplementationFlags() => Trap.Hit(nameof(GetMethodImplementationFlags), _real.GetMethodImplementationFlags());

    /// <inheritdoc/>
    public override ParameterInfo[] GetParameters() => Trap.Hit(nameof(GetParameters), _real.GetParameters());

    /// <inheritdoc/>
    public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
    {
        Trap.Hit(nameof(Invoke), 0);
        Trap.Evaluated();
        return _real.Invoke(obj, invokeAttr, binder, parameters, culture);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Trap.Hit(nameof(Equals), ReferenceEquals(this, obj) || ReferenceEquals(obj, _real));

    /// <inheritdoc/>
    public override int GetHashCode() => Trap.Hit(nameof(GetHashCode), _real.GetHashCode());

    /// <inheritdoc/>
    public override string ToString() => Trap.Hit(nameof(ToString), _real.ToString() ?? "");
}

/// <summary>
/// A user <see cref="FieldInfo"/> that impersonates a real mutable field and claims to be read-only. Its <c>GetValue</c> reads
/// the real field and counts the evaluation.
/// </summary>
internal sealed class ImpersonatingField : FieldInfo
{
    /// <summary>The impersonated field.</summary>
    private readonly FieldInfo _real;

    /// <summary>Initializes an impersonation of <paramref name="real"/>.</summary>
    /// <param name="real">The impersonated runtime field.</param>
    public ImpersonatingField(FieldInfo real) => _real = real;

    /// <summary>Gets the trap recording the calls.</summary>
    public ReflectionTrap Trap { get; } = new();

    /// <inheritdoc/>
    public override FieldAttributes Attributes => Trap.Hit(nameof(Attributes), _real.Attributes | FieldAttributes.InitOnly);

    /// <inheritdoc/>
    public override RuntimeFieldHandle FieldHandle => Trap.Hit(nameof(FieldHandle), _real.FieldHandle);

    /// <inheritdoc/>
    public override Type FieldType => Trap.Hit(nameof(FieldType), _real.FieldType);

    /// <inheritdoc/>
    public override Type? DeclaringType => Trap.Hit(nameof(DeclaringType), _real.DeclaringType);

    /// <inheritdoc/>
    public override string Name => Trap.Hit(nameof(Name), _real.Name);

    /// <inheritdoc/>
    public override Type? ReflectedType => Trap.Hit(nameof(ReflectedType), _real.ReflectedType);

    /// <inheritdoc/>
    public override int MetadataToken => Trap.Hit(nameof(MetadataToken), _real.MetadataToken);

    /// <inheritdoc/>
    public override Module Module => Trap.Hit(nameof(Module), _real.Module);

    /// <inheritdoc/>
    public override object[] GetCustomAttributes(bool inherit) => Trap.Hit(nameof(GetCustomAttributes), _real.GetCustomAttributes(inherit));

    /// <inheritdoc/>
    public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Trap.Hit(nameof(GetCustomAttributes), _real.GetCustomAttributes(attributeType, inherit));

    /// <inheritdoc/>
    public override bool IsDefined(Type attributeType, bool inherit) => Trap.Hit(nameof(IsDefined), _real.IsDefined(attributeType, inherit));

    /// <inheritdoc/>
    public override object? GetValue(object? obj)
    {
        Trap.Hit(nameof(GetValue), 0);
        Trap.Evaluated();
        return _real.GetValue(obj);
    }

    /// <inheritdoc/>
    public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, CultureInfo? culture)
    {
        Trap.Hit(nameof(SetValue), 0);
        _real.SetValue(obj, value, invokeAttr, binder, culture);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Trap.Hit(nameof(Equals), ReferenceEquals(this, obj) || ReferenceEquals(obj, _real));

    /// <inheritdoc/>
    public override int GetHashCode() => Trap.Hit(nameof(GetHashCode), _real.GetHashCode());

    /// <inheritdoc/>
    public override string ToString() => Trap.Hit(nameof(ToString), _real.ToString() ?? "");
}

/// <summary>A user <see cref="PropertyInfo"/> that impersonates a real property and hands out its real (runtime) getter.</summary>
internal sealed class ImpersonatingProperty : PropertyInfo
{
    /// <summary>The impersonated property.</summary>
    private readonly PropertyInfo _real;

    /// <summary>Initializes an impersonation of <paramref name="real"/>.</summary>
    /// <param name="real">The impersonated runtime property.</param>
    public ImpersonatingProperty(PropertyInfo real) => _real = real;

    /// <summary>Gets the trap recording the calls.</summary>
    public ReflectionTrap Trap { get; } = new();

    /// <inheritdoc/>
    public override PropertyAttributes Attributes => Trap.Hit(nameof(Attributes), _real.Attributes);

    /// <inheritdoc/>
    public override bool CanRead => Trap.Hit(nameof(CanRead), _real.CanRead);

    /// <inheritdoc/>
    public override bool CanWrite => Trap.Hit(nameof(CanWrite), _real.CanWrite);

    /// <inheritdoc/>
    public override Type PropertyType => Trap.Hit(nameof(PropertyType), _real.PropertyType);

    /// <inheritdoc/>
    public override Type? DeclaringType => Trap.Hit(nameof(DeclaringType), _real.DeclaringType);

    /// <inheritdoc/>
    public override string Name => Trap.Hit(nameof(Name), _real.Name);

    /// <inheritdoc/>
    public override Type? ReflectedType => Trap.Hit(nameof(ReflectedType), _real.ReflectedType);

    /// <inheritdoc/>
    public override MethodInfo[] GetAccessors(bool nonPublic) => Trap.Hit(nameof(GetAccessors), _real.GetAccessors(nonPublic));

    /// <inheritdoc/>
    public override object[] GetCustomAttributes(bool inherit) => Trap.Hit(nameof(GetCustomAttributes), _real.GetCustomAttributes(inherit));

    /// <inheritdoc/>
    public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Trap.Hit(nameof(GetCustomAttributes), _real.GetCustomAttributes(attributeType, inherit));

    /// <inheritdoc/>
    public override MethodInfo? GetGetMethod(bool nonPublic) => Trap.Hit(nameof(GetGetMethod), _real.GetGetMethod(nonPublic));

    /// <inheritdoc/>
    public override ParameterInfo[] GetIndexParameters() => Trap.Hit(nameof(GetIndexParameters), _real.GetIndexParameters());

    /// <inheritdoc/>
    public override MethodInfo? GetSetMethod(bool nonPublic) => Trap.Hit(nameof(GetSetMethod), _real.GetSetMethod(nonPublic));

    /// <inheritdoc/>
    public override object? GetValue(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture)
    {
        Trap.Hit(nameof(GetValue), 0);
        Trap.Evaluated();
        return _real.GetValue(obj, invokeAttr, binder, index, culture);
    }

    /// <inheritdoc/>
    public override bool IsDefined(Type attributeType, bool inherit) => Trap.Hit(nameof(IsDefined), _real.IsDefined(attributeType, inherit));

    /// <inheritdoc/>
    public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture)
    {
        Trap.Hit(nameof(SetValue), 0);
        _real.SetValue(obj, value, invokeAttr, binder, index, culture);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Trap.Hit(nameof(Equals), ReferenceEquals(this, obj) || ReferenceEquals(obj, _real));

    /// <inheritdoc/>
    public override int GetHashCode() => Trap.Hit(nameof(GetHashCode), _real.GetHashCode());

    /// <inheritdoc/>
    public override string ToString() => Trap.Hit(nameof(ToString), _real.ToString() ?? "");
}
