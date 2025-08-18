using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.DependencyInjection;

namespace UtilsTest.DependencyInjection;

/// <summary>
/// Tests for the StaticAuto source generator.
/// </summary>
[TestClass]
public class StaticAutoGeneratorTests
{
        /// <summary>
        /// Verifies that the generated configurator registers attributed types.
        /// </summary>
        [TestMethod]
        public void ConfigureServices_GeneratedRegistersTypes()
        {
                var services = new ServiceCollection();
                new AutoConfigurator().ConfigureServices(services);
                var provider = services.BuildServiceProvider();
                var singleton1 = provider.GetRequiredService<ISingletonService>();
                var singleton2 = provider.GetRequiredService<ISingletonService>();
                Assert.AreSame(singleton1, singleton2);
                var transient1 = provider.GetRequiredService<TransientService>();
                var transient2 = provider.GetRequiredService<TransientService>();
                Assert.AreNotSame(transient1, transient2);
                var keyed = provider.GetRequiredKeyedService<ISingletonService>("domain");
                Assert.AreNotSame(singleton1, keyed);
        }
}

/// <summary>
/// Marker interface for singleton services used in tests.
/// </summary>
[Injectable]
public interface ISingletonService { }

/// <summary>
/// Default singleton implementation.
/// </summary>
[Singleton]
public class SingletonService : ISingletonService { }

/// <summary>
/// Singleton implementation registered for a domain.
/// </summary>
[Singleton("domain")]
public class DomainSingletonService : ISingletonService { }

/// <summary>
/// Transient service used to verify lifetime handling.
/// </summary>
[Transient]
public class TransientService { }

/// <summary>
/// Configurator used to exercise source generation.
/// </summary>
[StaticAuto]
public partial class AutoConfigurator : IServiceConfigurator { }
