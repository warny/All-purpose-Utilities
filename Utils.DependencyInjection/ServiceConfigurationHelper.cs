using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.CompilerServices;
using Utils.Reflection;

namespace QueryOData.Injection;

public static class ServiceConfigurationHelper
{
	public static void ConfigureServices(this IEnumerable<Assembly> assemblies, IServiceCollection services)
	{
		foreach (var assembly in assemblies)
		{
			ConfigureServices(assembly, services);
		}
	}

	public static void ConfigureServices(this Assembly assembly, IServiceCollection serviceCollection)
	{
		var assemblyTypes = assembly.GetTypes();

		var serviceConfiguratorType = assemblyTypes.FirstOrDefault(t => typeof(IServiceConfigurator).IsAssignableFrom(t));
		if (serviceConfiguratorType != null)
		{
			IServiceConfigurator serviceConfigurator = (IServiceConfigurator)Activator.CreateInstance(serviceConfiguratorType);
			if (serviceConfigurator != null)
			{
				serviceConfigurator.ConfigureServices(serviceCollection);
				return;
			}
		}

		AutomaticConfigureServices(assembly, serviceCollection);
	}

	public static void ConfigureServices(this IServiceCollection serviceCollection, IServiceConfigurator serviceConfiguration)
	{
		Assembly assembly = serviceCollection.GetType().Assembly;
		AutomaticConfigureServices(assembly, serviceCollection);
	}

	private static void AutomaticConfigureServices(Assembly assembly, IServiceCollection serviceCollection)
	{
		var types = ReflectionEx.GetTypes(assembly, t => ((Attribute[])t.GetCustomAttributes(true)).Any(a => a is InjectableClassAttribute));

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
