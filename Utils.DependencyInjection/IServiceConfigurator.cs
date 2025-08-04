using Microsoft.Extensions.DependencyInjection;

namespace QueryOData.Injection;

public interface IServiceConfigurator
{
	void ConfigureServices(IServiceCollection serviceCollection);
}
