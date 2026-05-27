# omy.Utils.DependencyInjection.Generators

`omy.Utils.DependencyInjection.Generators` provides a Roslyn source generator that implements `IServiceConfigurator` classes marked with `[StaticAuto]`. It complements `omy.Utils.DependencyInjection` by wiring attribute-based registrations at compile time.

## Install
```bash
dotnet add package omy.Utils.DependencyInjection.Generators
```

## Supported frameworks
- netstandard2.0 (analyzer)

## Features

- Scans the compilation for `IServiceConfigurator` implementations decorated with `[StaticAuto]`.
- Detects types annotated with `[Singleton]`, `[Scoped]`, or `[Transient]` and generates matching `AddSingleton`/`AddScoped`/`AddTransient` calls.
- Supports keyed registrations when a domain string is passed to the lifetime attribute.
- Registers interfaces marked with `[Injectable]` against their attributed implementations.
- Emits a `partial` class so generated code coexists with hand-written logic.

## Getting started

1. Add `omy.Utils.DependencyInjection.Generators` as an analyzer reference.
2. Add `omy.Utils.DependencyInjection` as a library reference.
3. Decorate services with lifetime attributes and expose interfaces with `[Injectable]`.
4. Create a `partial` `IServiceConfigurator` implementation and mark it `[StaticAuto]`.

## Examples

### Basic registration

```csharp
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

string result = provider.GetRequiredService<IMessageFormatter>().Format("hello");
// → "HELLO"
```

The generator emits a `ConfigureServices` implementation that calls `services.AddSingleton<IMessageFormatter, UppercaseFormatter>()`.

### Keyed registrations

Supply a domain key to the lifetime attribute for keyed (multi-tenant) scenarios:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

[Injectable]
public interface ICalculator { int Add(int a, int b); }

[Singleton("europe")]
public class VatCalculator : ICalculator
{
    public int Add(int a, int b) => (int)Math.Round((a + b) * 1.2);
}

[Singleton("us")]
public class SalesTaxCalculator : ICalculator
{
    public int Add(int a, int b) => (int)Math.Round((a + b) * 1.07);
}

[StaticAuto]
public partial class CalculatorConfigurator : IServiceConfigurator { }

var services = new ServiceCollection();
new CalculatorConfigurator().ConfigureServices(services);
var provider = services.BuildServiceProvider();

var eu = provider.GetRequiredKeyedService<ICalculator>("europe");
var us = provider.GetRequiredKeyedService<ICalculator>("us");
```

The generator emits `AddKeyedSingleton<ICalculator, VatCalculator>("europe")` and the corresponding call for `SalesTaxCalculator`.

### Open-generic registrations

The generator handles open-generic implementations:

```csharp
using Utils.DependencyInjection;

[Injectable]
public interface IRepository<T> where T : class
{
    ValueTask<T?> GetAsync(Guid id, CancellationToken ct = default);
}

[Scoped]
public class SqlRepository<T> : IRepository<T> where T : class
{
    public ValueTask<T?> GetAsync(Guid id, CancellationToken ct = default)
        => ValueTask.FromResult<T?>(default);
}

[StaticAuto]
public partial class DataConfigurator : IServiceConfigurator { }
```

At build time the generator emits `services.AddScoped(typeof(IRepository<>), typeof(SqlRepository<>))`.

### Manual registrations alongside generated ones

Because the generator only emits the `ConfigureServices` method body, you can add other members to the `partial` class freely:

```csharp
using Microsoft.Extensions.DependencyInjection;

[StaticAuto]
public partial class MessagingConfigurator : IServiceConfigurator
{
    public void RegisterExtraServices(IServiceCollection services)
    {
        services.AddSingleton<IMessageBus>(_ => new RabbitMqBus("amqp://localhost"));
    }
}
```

Call both `ConfigureServices` (generated) and `RegisterExtraServices` (manual) during application startup.

## Related packages
- `omy.Utils.DependencyInjection` – runtime attributes, assembly scanning, and handler pipeline.
- `omy.Utils` – shared helpers.
