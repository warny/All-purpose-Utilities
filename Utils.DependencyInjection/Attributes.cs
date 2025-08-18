namespace Utils.DependencyInjection;

/// <summary>
/// Marks an interface as available for dependency injection.
/// Implementations of interfaces decorated with this attribute can be
/// automatically registered in the service container.
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
public class InjectableAttribute : Attribute
{
}

/// <summary>
/// Base attribute for injectable classes specifying the registration domain.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public abstract class InjectableClassAttribute : Attribute
{
	/// <summary>
	/// Optional domain used as a key for keyed registrations.
	/// </summary>
	public string? Domain { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="InjectableClassAttribute"/> class.
	/// </summary>
	/// <param name="domain">Optional domain key for the service.</param>
	public InjectableClassAttribute(string? domain)
	{
		this.Domain = domain;
	}
}

/// <summary>
/// Indicates that a class should be registered as a singleton service.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class SingletonAttribute : InjectableClassAttribute
{
	/// <summary>
	/// Initializes a new <see cref="SingletonAttribute"/> with the default domain.
	/// </summary>
	public SingletonAttribute() : base(null) { }

	/// <summary>
	/// Initializes a new <see cref="SingletonAttribute"/> with the specified domain.
	/// </summary>
	/// <param name="domain">Domain key for the service.</param>
	public SingletonAttribute(string domain) : base(domain) { }
}

/// <summary>
/// Indicates that a class should be registered with a scoped lifetime.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ScopedAttribute : InjectableClassAttribute
{
	/// <summary>
	/// Initializes a new <see cref="ScopedAttribute"/> with the default domain.
	/// </summary>
	public ScopedAttribute() : base(null) { }

	/// <summary>
	/// Initializes a new <see cref="ScopedAttribute"/> with the specified domain.
	/// </summary>
	/// <param name="domain">Domain key for the service.</param>
	public ScopedAttribute(string domain) : base(domain) { }
}

/// <summary>
/// Indicates that a class should be registered with a transient lifetime.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class TransientAttribute : InjectableClassAttribute
{
	/// <summary>
	/// Initializes a new <see cref="TransientAttribute"/> with the default domain.
	/// </summary>
	public TransientAttribute() : base(null) { }

	/// <summary>
	/// Initializes a new <see cref="TransientAttribute"/> with the specified domain.
	/// </summary>
	/// <param name="domain">Domain key for the service.</param>
	public TransientAttribute(string domain) : base(domain) { }
}


/// <summary>
/// Applies to an <see cref="IServiceConfigurator"/> implementation to trigger compile-time generation of service registrations.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class StaticAutoAttribute : Attribute
{
}
