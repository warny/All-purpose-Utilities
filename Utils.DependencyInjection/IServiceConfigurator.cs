using Microsoft.Extensions.DependencyInjection;

namespace Utils.DependencyInjection;

/// <summary>
/// Configures dependency injection services for an application.
/// Implementations can customize registrations for a given
/// <see cref="IServiceCollection"/>.
/// </summary>
public interface IServiceConfigurator
{
	/// <summary>
	/// Adds services to the provided collection.
	/// </summary>
	/// <param name="serviceCollection">The service collection to populate.</param>
	void ConfigureServices(IServiceCollection serviceCollection);
}

