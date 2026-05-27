# omy.Utils.DependencyInjection (attribute-based registration)

`omy.Utils.DependencyInjection` extends `Microsoft.Extensions.DependencyInjection` with attributes and helpers to register services, handlers, and validators automatically by scanning assemblies.

## Install
```bash
dotnet add package omy.Utils.DependencyInjection
```

## Supported frameworks
- net9.0

## Features
- `[Injectable]` — marks an interface as a registrable contract.
- `[Singleton]` / `[Scoped]` / `[Transient]` — marks a class with a DI lifetime; supports an optional `domain` key for keyed services.
- `Assembly.ConfigureServices(services)` — scans an assembly and registers all attributed types.
- `IServiceConfigurator` — custom registration hook discovered automatically during assembly scanning.
- `[StaticAuto]` — triggers compile-time source generation of service registrations.
- `IHandler<T>` + `ICheck<T,E>` + `IHandlerCaller` — message dispatch pipeline with pre-validation.

## Quick usage

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

[Injectable]
public interface IGreetingService
{
    string Greet(string name);
}

[Singleton]
public class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello, {name}!";
}

var services = new ServiceCollection();
Assembly.GetExecutingAssembly().ConfigureServices(services);

var provider = services.BuildServiceProvider();
var svc = provider.GetRequiredService<IGreetingService>();
Console.WriteLine(svc.Greet("World")); // Hello, World!
```

## Service registration attributes

### Lifetimes

```csharp
using Utils.DependencyInjection;

// Interface contract — marks the service type as injectable
[Injectable]
public interface IEmailSender { void Send(string to, string body); }

// Concrete implementation — choose one lifetime attribute
[Singleton]   public class SmtpEmailSender  : IEmailSender { /* ... */ }
[Scoped]      public class ScopedEmailSender : IEmailSender { /* ... */ }
[Transient]   public class LogEmailSender   : IEmailSender { /* ... */ }
```

### Keyed (domain) registrations

Pass a `domain` string to route multiple implementations of the same interface:

```csharp
using Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[Injectable]
public interface IStorageBackend { void Write(string data); }

[Singleton("disk")]  public class DiskStorage  : IStorageBackend { /* ... */ }
[Singleton("cloud")] public class CloudStorage : IStorageBackend { /* ... */ }

var services = new ServiceCollection();
Assembly.GetExecutingAssembly().ConfigureServices(services);
var provider = services.BuildServiceProvider();

var disk  = provider.GetRequiredKeyedService<IStorageBackend>("disk");
var cloud = provider.GetRequiredKeyedService<IStorageBackend>("cloud");
```

### Class without an `[Injectable]` interface

If the class has no `[Injectable]` interface, it is registered as its own service type:

```csharp
using Utils.DependencyInjection;

[Singleton]
public class MetricsCollector { /* registered as MetricsCollector → MetricsCollector */ }
```

## Assembly scanning

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

var services = new ServiceCollection();

// Scan a single assembly
Assembly.GetExecutingAssembly().ConfigureServices(services);

// Scan multiple assemblies at once
new[] { typeof(MyService).Assembly, typeof(OtherService).Assembly }
    .ConfigureServices(services);
```

## IServiceConfigurator — custom registration hook

If an assembly contains a class that implements `IServiceConfigurator`, `ConfigureServices` delegates entirely to it instead of scanning for attributes:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

public class AppServiceConfigurator : IServiceConfigurator
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMyService, MyService>();
        services.AddScoped<IOtherService, OtherService>();
        // ... any custom registrations
    }
}

// Assembly scan will find AppServiceConfigurator and call it
Assembly.GetExecutingAssembly().ConfigureServices(new ServiceCollection());
```

### [StaticAuto] — source-generated registrations

Annotate your `IServiceConfigurator` with `[StaticAuto]` to have the `Utils.DependencyInjection.Generators` source generator emit the registration code at compile time:

```csharp
using Utils.DependencyInjection;

[StaticAuto]
public partial class GeneratedConfigurator : IServiceConfigurator
{
    // Registration body generated at compile time from [Singleton]/[Scoped]/[Transient] attributes
}
```

## Handler and validation pipeline

`IHandler<T>`, `ICheck<T,E>`, and `IHandlerCaller` form a lightweight message dispatch pipeline. Register everything via attributes and let `HandlerCaller` orchestrate validation and dispatch.

### Define a message, a validator, and a handler

```csharp
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

public record CreateOrderCommand(string CustomerId, decimal Amount);

// Validator — runs before any handler
[Transient]
public class CreateOrderValidator : ICheck<CreateOrderCommand, string>
{
    public bool Check(CreateOrderCommand cmd, List<string> errors)
    {
        if (string.IsNullOrEmpty(cmd.CustomerId))
            errors.Add("CustomerId is required.");
        if (cmd.Amount <= 0)
            errors.Add("Amount must be positive.");
        return errors.Count == 0;
    }
}

// Handler — invoked only when all checks pass
[Transient]
public class CreateOrderHandler : IHandler<CreateOrderCommand>
{
    public void Handle(CreateOrderCommand cmd)
        => Console.WriteLine($"Order for {cmd.CustomerId}: {cmd.Amount:C}");
}
```

### Wire up and dispatch

```csharp
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

var services = new ServiceCollection();
Assembly.GetExecutingAssembly().ConfigureServices(services);
var provider = services.BuildServiceProvider();

var caller = provider.GetRequiredService<IHandlerCaller>();
var errors = new List<CheckError<string>>();

var cmd = new CreateOrderCommand("CUST-1", 99.50m);
bool ok = caller.Handle(cmd, errors);

if (!ok)
{
    foreach (var e in errors)
        Console.WriteLine($"[{e.Source.Name}] {e.Error}");
}
```

### CheckError — validation error with source

`CheckError<E>` is a `readonly record struct` that pairs the originating validator type with the error value:

```csharp
// CheckError<string> carries:
//   e.Source  — Type of the ICheck implementation that produced the error
//   e.Error   — The error value (string, enum, custom type, etc.)
foreach (CheckError<string> e in errors)
    Console.WriteLine($"{e.Source.Name}: {e.Error}");
```

## Related packages
- `omy.Utils.DependencyInjection.Generators` – source generator that wires registrations at compile time.
- `omy.Utils` – shared helpers consumed by the DI components.
