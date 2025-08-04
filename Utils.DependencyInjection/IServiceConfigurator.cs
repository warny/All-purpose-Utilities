using Microsoft.Extensions.DependencyInjection;

namespace Utils.DependencyInjection;

public interface IServiceConfigurator
{
	void ConfigureServices(IServiceCollection serviceCollection);
}
