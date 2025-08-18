using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Utils.Reflection;

namespace Utils.DependencyInjection;

/// <summary>
/// Helper methods to configure services based on injectable attributes.
/// </summary>
public static class ServiceConfigurationHelper
{
	/// <summary>
	/// Configures services for all provided assemblies.
	/// </summary>
	/// <param name="assemblies">Assemblies to scan for injectable types.</param>
	/// <param name="services">Service collection to populate.</param>
	public static void ConfigureServices(this IEnumerable<Assembly> assemblies, IServiceCollection services)
	{
		foreach (var assembly in assemblies)
		{
			ConfigureServices(assembly, services);
		}
	}

	/// <summary>
	/// Configures services from a single assembly.
	/// </summary>
	/// <param name="assembly">Assembly containing the types to register.</param>
	/// <param name="serviceCollection">Collection receiving the registrations.</param>
	public static void ConfigureServices(this Assembly assembly, IServiceCollection serviceCollection)
	{
		var assemblyTypes = assembly.GetTypes();

		var serviceConfiguratorType = assemblyTypes.FirstOrDefault(t => typeof(IServiceConfigurator).IsAssignableFrom(t));
		if (serviceConfiguratorType != null)
		{
			IServiceConfigurator? serviceConfigurator = (IServiceConfigurator?)Activator.CreateInstance(serviceConfiguratorType);
			if (serviceConfigurator != null)
			{
				serviceConfigurator.ConfigureServices(serviceCollection);
				return;
			}
		}

		ConfigureServices(assemblyTypes.Where(t => t.GetCustomAttributes(true).OfType<Attribute>().Any(a => a is InjectableClassAttribute)), serviceCollection);
	}

	/// <summary>
	/// Configures services using the assembly of the service collection type.
	/// </summary>
	/// <param name="serviceConfiguration">Unused parameter allowing generic invocation.</param>
	/// <param name="serviceCollection">Collection to populate.</param>
	public static void ConfigureServices(this IServiceConfigurator serviceConfiguration, IServiceCollection serviceCollection)
	{
		Assembly assembly = serviceCollection.GetType().Assembly;
		var types = assembly.GetTypes(t => t.GetCustomAttributes(true).OfType<Attribute>().Any(a => a is InjectableClassAttribute));
		ConfigureServices(types, serviceCollection);
	}

	/// <summary>
	/// Configures services using the assembly of the service collection type.
	/// </summary>
	/// <param name="serviceCollection">Collection to populate.</param>
	/// <param name="types">types to use in services.</param>
	public static void ConfigureServices(this IEnumerable<Type> types, IServiceCollection serviceCollection)
	{
		foreach (var type in types)
		{
			IEnumerable<Type> interfaces = type.GetInterfaces().Where(i => i.GetCustomAttribute<InjectableAttribute>() != null);
			if (interfaces.Any())
			{
				foreach (var @interface in interfaces)
				{
					AddInjection(serviceCollection, @interface, type);
				}
			}
			else
			{
				AddInjection(serviceCollection, type, type);
			}
		}
	}

	/// <summary>
	/// Adds a service registration with the appropriate lifetime and domain.
	/// </summary>
	/// <param name="serviceCollection">Collection to populate.</param>
	/// <param name="interface">Interface representing the service.</param>
	/// <param name="type">Implementation type.</param>
	public static void AddInjection(this IServiceCollection serviceCollection, Type @interface, Type type)
	{
		switch (type.GetCustomAttribute<InjectableClassAttribute>())
		{
			case SingletonAttribute singleton:
				if (singleton.Domain is null)
				{
					serviceCollection.AddSingleton(@interface, type);
				}
				else
				{
					serviceCollection.AddKeyedSingleton(@interface, singleton.Domain, type);
				}
				break;
			case ScopedAttribute scoped:
				if (scoped.Domain is null)
				{
					serviceCollection.AddScoped(@interface, type);
				}
				else
				{
					serviceCollection.AddKeyedScoped(@interface, scoped.Domain, type);
				}
				break;
			case TransientAttribute transient:
				if (transient.Domain is null)
				{
					serviceCollection.AddTransient(@interface, type);
				}
				else
				{
					serviceCollection.AddKeyedTransient(@interface, transient.Domain, type);
				}
				break;
		}
	}
}

