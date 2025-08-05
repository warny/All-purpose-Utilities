# Utils.DependencyInjection Library

**Utils.DependencyInjection** enables attribute-based registration of services with
the `Microsoft.Extensions.DependencyInjection` framework. Types annotated with the
provided attributes are automatically added to an `IServiceCollection`.

## Usage example

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

[Injectable]
public interface IGreetingService { }

[Singleton]
public class GreetingService : IGreetingService { }

var services = new ServiceCollection();
Assembly.GetExecutingAssembly().ConfigureServices(services);
var provider = services.BuildServiceProvider();
var service = provider.GetRequiredService<IGreetingService>();
```


## Static configuration

The library provides a source generator that implements <code>IServiceConfigurator</code> for classes marked with <code>[StaticAuto]</code>.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

[Injectable]
public interface IGreetingService { }

[Singleton]
public class GreetingService : IGreetingService { }

[StaticAuto]
public partial class GreetingConfigurator : IServiceConfigurator { }

var services = new ServiceCollection();
new GreetingConfigurator().ConfigureServices(services);
var provider = services.BuildServiceProvider();
var service = provider.GetRequiredService<IGreetingService>();
```
