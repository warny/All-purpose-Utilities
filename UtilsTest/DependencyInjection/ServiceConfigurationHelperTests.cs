using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using Utils.DependencyInjection;

namespace UtilsTest.DependencyInjection;

[TestClass]
public class ServiceConfigurationHelperTests
{
        [TestMethod]
        public void ConfigureServices_RegistersAttributedTypes()
        {
                var services = new ServiceCollection();
                Assembly.GetExecutingAssembly().ConfigureServices(services);
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

        [Injectable]
        public interface ISingletonService { }

        [Singleton]
        private class SingletonService : ISingletonService { }

        [Singleton("domain")]
        private class DomainSingletonService : ISingletonService { }

        [Transient]
        private class TransientService { }
}

