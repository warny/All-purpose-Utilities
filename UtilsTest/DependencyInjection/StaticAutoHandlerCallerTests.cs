using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.DependencyInjection;

namespace UtilsTest.DependencyInjection;

/// <summary>
/// Tests the handler system with statically generated configuration.
/// </summary>
[TestClass]
public class StaticAutoHandlerCallerTests
{
    /// <summary>
    /// Ensures that the generated configurator registers handlers and checks, allowing messages to be dispatched.
    /// </summary>
    [TestMethod]
    public void Handle_UsesGeneratedConfiguration()
    {
        var services = new ServiceCollection();
        new HandlerAutoConfigurator().ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var caller = provider.GetRequiredService<IHandlerCaller>();
        var message = new SampleMessage { Valid = true };
        List<CheckError<string>> errors = new();
        var handled = caller.Handle<string>(message, errors);

        Assert.IsTrue(handled);
        Assert.AreEqual(0, errors.Count);
        var handler = (SampleHandler)provider.GetRequiredService<IHandler<SampleMessage>>();
        Assert.AreSame(message, handler.LastMessage);
    }

    /// <summary>
    /// Ensures that when the check fails, the handler is not invoked.
    /// </summary>
    [TestMethod]
    public void Handle_CheckFailurePreventsHandling()
    {
        var services = new ServiceCollection();
        new HandlerAutoConfigurator().ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var caller = provider.GetRequiredService<IHandlerCaller>();
        var message = new SampleMessage { Valid = false };
        List<CheckError<string>> errors = new();
        var handled = caller.Handle<string>(message, errors);

        Assert.IsFalse(handled);
        CollectionAssert.AreEqual(new[] { new CheckError<string>(typeof(SampleCheck), "invalid"), new CheckError<string>(typeof(SampleCheck), "still invalid") }, errors);
        var handler = (SampleHandler)provider.GetRequiredService<IHandler<SampleMessage>>();
        Assert.IsNull(handler.LastMessage);
    }

    /// <summary>
    /// Represents a message used for testing.
    /// </summary>
    internal class SampleMessage
    {
        /// <summary>
        /// Gets or sets a value indicating whether the message is valid.
        /// </summary>
        public bool Valid { get; set; }
    }

    /// <summary>
    /// Handler used to capture messages during tests.
    /// </summary>
    [Singleton]
    internal class SampleHandler : IHandler<SampleMessage>
    {
        /// <summary>
        /// Gets the last message handled by this instance.
        /// </summary>
        public SampleMessage? LastMessage { get; private set; }

        /// <summary>
        /// Handles the provided <paramref name="message"/> by storing it.
        /// </summary>
        /// <param name="message">Message instance to store.</param>
        public void Handle(SampleMessage message) => LastMessage = message;
    }

    /// <summary>
    /// Validation component used to verify <see cref="SampleMessage"/> instances.
    /// </summary>
    [Singleton]
    internal class SampleCheck : ICheck<SampleMessage, string>
    {
        /// <inheritdoc />
        public bool Check(SampleMessage message, List<string> errors)
        {
            if (message.Valid)
            {
                return true;
            }

            errors.Add("invalid");
            errors.Add("still invalid");
            return false;
        }
    }

    /// <summary>
    /// Wrapper to register <see cref="HandlerCaller"/> through the source generator.
    /// </summary>
    [Singleton]
    internal class TestHandlerCaller : HandlerCaller, IHandlerCaller
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestHandlerCaller"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider used to resolve handlers.</param>
        public TestHandlerCaller(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }
    }

}

/// <summary>
/// Configurator used to exercise source generation.
/// </summary>
[StaticAuto]
public partial class HandlerAutoConfigurator : IServiceConfigurator { }

