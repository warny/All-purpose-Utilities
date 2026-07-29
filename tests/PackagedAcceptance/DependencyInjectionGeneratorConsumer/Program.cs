using Microsoft.Extensions.DependencyInjection;
using Utils.DependencyInjection;

var services = new ServiceCollection();
new ProductConfigurator().ConfigureServices(services);
using var provider = services.BuildServiceProvider();
if (provider.GetRequiredService<IMessage>().Value != "generated") throw new InvalidOperationException("Generated registration failed.");
Console.WriteLine("dependency-injection-generator-executed");

/// <summary>Exposes the value resolved through generated registration.</summary>
[Injectable]
public interface IMessage
{
    /// <summary>Gets the acceptance value.</summary>
    string Value { get; }
}

/// <summary>Implements the generated-registration acceptance service.</summary>
[Singleton]
public sealed class Message : IMessage
{
    /// <inheritdoc />
    public string Value => "generated";
}

/// <summary>Receives the generated service registration implementation.</summary>
[StaticAuto]
public partial class ProductConfigurator : IServiceConfigurator { }
