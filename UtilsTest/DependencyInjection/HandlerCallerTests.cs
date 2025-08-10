using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.DependencyInjection;

namespace UtilsTest.DependencyInjection;

/// <summary>
/// Tests for the handler dispatching system.
/// </summary>
[TestClass]
public class HandlerCallerTests
{
        /// <summary>
        /// Ensures that <see cref="HandlerCaller"/> invokes the message check before the handler and dispatches when valid.
        /// </summary>
        [TestMethod]
        public void Handle_CallsCheckAndHandler()
        {
                var services = new ServiceCollection();
                Type[] types = [typeof(SampleHandler), typeof(SampleCheck), typeof(HandlerCaller)];
                types.ConfigureServices(services);
                var provider = services.BuildServiceProvider();

                var caller = provider.GetRequiredService<IHandlerCaller>();
                var message = new SampleMessage { Valid = true };
                List<string> errors = new();
                var handled = caller.Handle<string>(message, errors);

                Assert.IsTrue(handled);
                Assert.AreEqual(0, errors.Count);
                var handler = (SampleHandler)provider.GetRequiredService<IHandler<SampleMessage>>();
                Assert.AreSame(message, handler.LastMessage);
        }

        /// <summary>
        /// Ensures that when the check fails the handler is not invoked.
        /// </summary>
        [TestMethod]
        public void Handle_CheckFailurePreventsHandling()
        {
                var services = new ServiceCollection();
                Type[] types = [typeof(SampleHandler), typeof(SampleCheck), typeof(HandlerCaller)];
                types.ConfigureServices(services);
                var provider = services.BuildServiceProvider();

                var caller = provider.GetRequiredService<IHandlerCaller>();
                var message = new SampleMessage { Valid = false };
                List<string> errors = new();
                var handled = caller.Handle<string>(message, errors);

                Assert.IsFalse(handled);
                CollectionAssert.AreEqual(new[] { "invalid", "still invalid" }, errors);
                var handler = (SampleHandler)provider.GetRequiredService<IHandler<SampleMessage>>();
                Assert.IsNull(handler.LastMessage);
        }

        /// <summary>
        /// Represents a message used for testing.
        /// </summary>
        private class SampleMessage
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
        private class SampleHandler : IHandler<SampleMessage>
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
        private class SampleCheck : ICheck<SampleMessage, string>
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
}

