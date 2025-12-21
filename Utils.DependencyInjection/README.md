# omy.Utils.DependencyInjection (attribute-based registration)

`omy.Utils.DependencyInjection` extends `Microsoft.Extensions.DependencyInjection` with attributes to register services, handlers, and checks automatically.

## Install
```bash
dotnet add package omy.Utils.DependencyInjection
```

## Supported frameworks
- net9.0

## Features
- `[Injectable]`, `[Singleton]`, `[Scoped]`, and `[Transient]` attributes for service discovery.
- Handler and check abstractions for pipeline-style processing.
- Integration point for the `Utils.DependencyInjection.Generators` source generator.

## Quick usage
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

## Related packages
- `omy.Utils.DependencyInjection.Generators` – source generator that wires registrations at compile time.
- `omy.Utils` – shared helpers consumed by the DI components.
