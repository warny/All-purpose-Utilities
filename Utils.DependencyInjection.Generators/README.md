# Utils.DependencyInjection.Generators

`Utils.DependencyInjection.Generators` provides a Roslyn source generator that implements `IServiceConfigurator` classes marked
with the `[StaticAuto]` attribute. It targets **.NET 9** and complements the `Utils.DependencyInjection` runtime library by wiring
attribute-based registrations at build time.

## Install
```bash
dotnet add package omy.Utils.DependencyInjection.Generators
```

## Supported frameworks
- netstandard2.0 (analyzer)

## Features

- Scans the compilation for `IServiceConfigurator` implementations decorated with `[StaticAuto]`.
- Detects types annotated with `[Singleton]`, `[Scoped]`, or `[Transient]` and generates the matching registration calls.
- Supports keyed registrations when a domain is supplied to the lifetime attributes.
- Registers interfaces marked with `[Injectable]` against their attributed implementations.
- Emits partial class implementations that remain editable alongside generated code.

## Getting started

1. Add a project reference to **Utils.DependencyInjection.Generators** (as an analyzer) and to **Utils.DependencyInjection** (as a library).
2. Decorate your services with lifetime attributes and mark exposed interfaces with `[Injectable]`.
3. Create an `IServiceConfigurator` implementation and mark it with `[StaticAuto]`.

## Examples

The following scenarios highlight the generator's capabilities. They can be mixed and
matched inside the same project because the generated partial classes never overwrite
hand-written logic.

### Basic registration example

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

[Injectable]
public interface IMessageFormatter
{
    string Format(string message);
}

[Singleton]
public class UppercaseFormatter : IMessageFormatter
{
    public string Format(string message) => message.ToUpperInvariant();
}

[StaticAuto]
public partial class FormatterConfigurator : IServiceConfigurator { }

var services = new ServiceCollection();
new FormatterConfigurator().ConfigureServices(services);
var provider = services.BuildServiceProvider();
var formatter = provider.GetRequiredService<IMessageFormatter>();
string result = formatter.Format("hello");
```

The generated `ConfigureServices` method automatically registers `UppercaseFormatter` as the
implementation for `IMessageFormatter`.

### Multi-tenant keyed registrations

You can scope services to tenants or domains by supplying a key when decorating the service with lifetime attributes.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

[Injectable]
public interface ICalculator
{
    int Add(int left, int right);
}

[Singleton("europe")]
public class VatCalculator : ICalculator
{
    public int Add(int left, int right) => (int)Math.Round((left + right) * 1.2, MidpointRounding.AwayFromZero);
}

[Singleton("us")]
public class SalesTaxCalculator : ICalculator
{
    public int Add(int left, int right) => (int)Math.Round((left + right) * 1.07, MidpointRounding.AwayFromZero);
}

[StaticAuto]
public partial class CalculatorConfigurator : IServiceConfigurator { }

var services = new ServiceCollection();
new CalculatorConfigurator().ConfigureServices(services);
var provider = services.BuildServiceProvider();
var europeanCalculator = provider.GetRequiredKeyedService<ICalculator>("europe");
var usCalculator = provider.GetRequiredKeyedService<ICalculator>("us");
```

The generator emits `AddKeyedSingleton` calls so that each calculator is registered with its
domain. Consumers resolve the correct implementation by providing the key.

### Partial methods for fine-grained control

Because the generator emits partial classes, you can extend the configurator with custom logic without losing the automatic registrations.

```csharp
using Microsoft.Extensions.DependencyInjection;

[StaticAuto]
public partial class AdvancedConfigurator : IServiceConfigurator
{
    partial void OnAfterConfigureServices(IServiceCollection services)
    {
        services.AddLogging();
    }
}
```

When the generator is executed it creates a matching partial class and invokes
`OnAfterConfigureServices` after wiring the detected services, allowing manual tweaks.

### Module-based composition

Large solutions commonly split registrations into domain-specific modules. The generator can
aggregate all of them without manual wiring.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

[StaticAuto]
public partial class AccountingModule : IServiceConfigurator { }

[StaticAuto]
public partial class NotificationsModule : IServiceConfigurator { }

[StaticAuto]
public partial class ApplicationConfigurator : IServiceConfigurator
{
    partial void OnAfterConfigureServices(IServiceCollection services)
    {
        services.AddLogging();
    }
}

var services = new ServiceCollection();
new ApplicationConfigurator().ConfigureServices(services);
```

Each generated partial class invokes the others through `IServiceConfigurator` so the final
`ApplicationConfigurator` can remain focused on cross-cutting concerns such as logging or
metrics.

### Open generics and interfaces

The generator handles open-generic registrations when the implementation type declares a
generic constraint.

```csharp
using Utils.DependencyInjection;

[Injectable]
public interface IRepository<T>
    where T : class
{
    ValueTask<T?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}

[Scoped]
public class SqlRepository<T> : IRepository<T>
    where T : class
{
    public ValueTask<T?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // database lookup
        return ValueTask.FromResult<T?>(default);
    }
}

[StaticAuto]
public partial class DataConfigurator : IServiceConfigurator { }
```

At build time the generator emits `services.AddScoped(typeof(IRepository<>), typeof(SqlRepository<>));`
while preserving the generic constraints.

### Conditional environments

Partial methods are also ideal for environment-based tweaks. The generator still outputs the
default registrations while giving developers an override point.

```csharp
using Microsoft.Extensions.DependencyInjection;

[StaticAuto]
public partial class EnvironmentConfigurator : IServiceConfigurator
{
    partial void OnBeforeConfigureServices(IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IPathService, WindowsPathService>();
        }
    }
}
```

`OnBeforeConfigureServices` is emitted alongside `ConfigureServices`. It executes before the
automatic registrations which allows conditional replacements without disabling generation.

### Mixing manual and generated registrations

Source generation remains opt-in for each configurator. Manual entries can remain untouched
while the generator handles the repetitive parts.

```csharp
using Microsoft.Extensions.DependencyInjection;

[StaticAuto]
public partial class MessagingConfigurator : IServiceConfigurator
{
    partial void OnAfterConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMessageBus>(_ => new RabbitMqBus("amqp://localhost"));
    }
}
```

Because the generator never rewrites the partial file, developers can evolve their manual
registrations at their own pace while still benefiting from automated discovery for routine
services.
