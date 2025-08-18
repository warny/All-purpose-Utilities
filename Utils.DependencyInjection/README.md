# Utils.DependencyInjection Library

**Utils.DependencyInjection** enables attribute-based registration of services with
the `Microsoft.Extensions.DependencyInjection` framework. Types annotated with the
provided attributes are automatically added to an `IServiceCollection`.

## Usage example

```csharp
using System.Reflection;
using System;
using System.Collections.Generic;
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

The generator can also register handlers and their checks:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

public class Ping { public bool Valid { get; set; } }

[Singleton]
public class PingCheck : ICheck<Ping, string>
{
    public bool Check(Ping message, List<string> errors)
    {
        if (message.Valid)
        {
            return true;
        }

        errors.Add("invalid");
        return false;
    }
}

[Singleton]
public class PingHandler : IHandler<Ping>
{
    public void Handle(Ping message) => Console.WriteLine("pong");
}

[Singleton]
public class PingCaller : HandlerCaller
{
    public PingCaller(IServiceProvider provider) : base(provider) { }
}

[StaticAuto]
public partial class PingConfigurator : IServiceConfigurator { }

var services = new ServiceCollection();
new PingConfigurator().ConfigureServices(services);
var provider = services.BuildServiceProvider();
var caller = provider.GetRequiredService<IHandlerCaller>();
var errors = new List<CheckError<string>>();
caller.Handle<string>(new Ping { Valid = true }, errors);
```

## Handler system

The library also provides a simple message handler pattern with optional message validation.
Multiple handlers and checks can target the same message; every check runs before all
handlers execute. When validation fails, each error is associated with the type of the check that produced it.

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

public class Ping { }

[Singleton]
public class PingContentCheck : ICheck<Ping, string>
{
    public bool Check(Ping message, List<string> errors)
    {
        return true;
    }
}

[Singleton]
public class PingSecurityCheck : ICheck<Ping, string>
{
    public bool Check(Ping message, List<string> errors)
    {
        return true;
    }
}

[Singleton]
public class PingHandler : IHandler<Ping>
{
    public void Handle(Ping message) => Console.WriteLine("pong");
}

[Singleton]
public class PingLogger : IHandler<Ping>
{
    public void Handle(Ping message) => Console.WriteLine("log");
}

var services = new ServiceCollection();
new Type[] { typeof(PingContentCheck), typeof(PingSecurityCheck), typeof(PingHandler), typeof(PingLogger), typeof(HandlerCaller) }.ConfigureServices(services);
var provider = services.BuildServiceProvider();
var caller = provider.GetRequiredService<IHandlerCaller>();
var errors = new List<CheckError<string>>();
caller.Handle<string>(new Ping(), errors);
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
