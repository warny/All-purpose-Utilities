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

		AutomaticConfigureServices(assembly, serviceCollection);
	}

	/// <summary>
	/// Configures services using the assembly of the service collection type.
	/// </summary>
	/// <param name="serviceCollection">Collection to populate.</param>
	/// <param name="serviceConfiguration">Unused parameter allowing generic invocation.</param>
	public static void ConfigureServices(this IServiceCollection serviceCollection, IServiceConfigurator serviceConfiguration)
	{
		Assembly assembly = serviceCollection.GetType().Assembly;
		AutomaticConfigureServices(assembly, serviceCollection);
	}

	/// <summary>
	/// Scans the assembly and registers all injectable types.
	/// </summary>
	/// <param name="assembly">Assembly to scan.</param>
	/// <param name="serviceCollection">Collection to populate.</param>
	private static void AutomaticConfigureServices(Assembly assembly, IServiceCollection serviceCollection)
	{
		var types = ReflectionEx.GetTypes(assembly, t => t.GetCustomAttributes(true).OfType<Attribute>().Any(a => a is InjectableClassAttribute));

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
	private static void AddInjection(IServiceCollection serviceCollection, Type @interface, Type type)
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

