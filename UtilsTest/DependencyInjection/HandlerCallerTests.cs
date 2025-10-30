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
        List<CheckError<string>> errors = new();
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
        List<CheckError<string>> errors = new();
        var handled = caller.Handle<string>(message, errors);

        Assert.IsFalse(handled);
        CollectionAssert.AreEqual(new[] { new CheckError<string>(typeof(SampleCheck), "invalid"), new CheckError<string>(typeof(SampleCheck), "still invalid") }, errors);
        var handler = (SampleHandler)provider.GetRequiredService<IHandler<SampleMessage>>();
        Assert.IsNull(handler.LastMessage);
    }

    /// <summary>
    /// Ensures that all registered checks and handlers are invoked.
    /// </summary>
    [TestMethod]
    public void Handle_MultipleChecksAndHandlers()
    {
        var services = new ServiceCollection();
        Type[] types = [typeof(MultiHandlerA), typeof(MultiHandlerB), typeof(MultiCheckA), typeof(MultiCheckB), typeof(HandlerCaller)];
        types.ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var caller = provider.GetRequiredService<IHandlerCaller>();
        var message = new MultiMessage();
        List<CheckError<string>> errors = new();
        var handled = caller.Handle<string>(message, errors);

        Assert.IsTrue(handled);
        Assert.AreEqual(0, errors.Count);

        MultiHandlerA handlerA = null!;
        MultiHandlerB handlerB = null!;
        foreach (var handler in provider.GetServices<IHandler<MultiMessage>>())
        {
            if (handler is MultiHandlerA a)
            {
                handlerA = a;
            }
            else if (handler is MultiHandlerB b)
            {
                handlerB = b;
            }
        }

        Assert.IsNotNull(handlerA);
        Assert.AreSame(message, handlerA.LastMessage);
        Assert.IsNotNull(handlerB);
        Assert.AreSame(message, handlerB.LastMessage);
    }

    /// <summary>
    /// Ensures that failing checks aggregate errors and prevent handler execution.
    /// </summary>
    [TestMethod]
    public void Handle_MultipleCheckFailuresPreventHandling()
    {
        var services = new ServiceCollection();
        Type[] types = [typeof(MultiHandlerA), typeof(MultiHandlerB), typeof(MultiFailCheckA), typeof(MultiFailCheckB), typeof(HandlerCaller)];
        types.ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var caller = provider.GetRequiredService<IHandlerCaller>();
        var message = new MultiMessage();
        List<CheckError<string>> errors = new();
        var handled = caller.Handle<string>(message, errors);

        Assert.IsFalse(handled);
        CollectionAssert.AreEquivalent(new[] { new CheckError<string>(typeof(MultiFailCheckA), "errorA"), new CheckError<string>(typeof(MultiFailCheckB), "errorB") }, errors);

        foreach (var handler in provider.GetServices<IHandler<MultiMessage>>())
        {
            if (handler is MultiHandlerA a)
            {
                Assert.IsNull(a.LastMessage);
            }
            else if (handler is MultiHandlerB b)
            {
                Assert.IsNull(b.LastMessage);
            }
        }
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
        public SampleMessage LastMessage { get; private set; } = null!;

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

    /// <summary>
    /// Message used to test multiple handlers and checks.
    /// </summary>
    private class MultiMessage
    {
    }

    /// <summary>
    /// First handler capturing <see cref="MultiMessage"/> instances.
    /// </summary>
    [Singleton]
    private class MultiHandlerA : IHandler<MultiMessage>
    {
        /// <summary>
        /// Gets the last message handled by this instance.
        /// </summary>
        public MultiMessage LastMessage { get; private set; } = null!;

        /// <summary>
        /// Stores the handled <paramref name="message"/>.
        /// </summary>
        /// <param name="message">Message instance to handle.</param>
        public void Handle(MultiMessage message) => LastMessage = message;
    }

    /// <summary>
    /// Second handler capturing <see cref="MultiMessage"/> instances.
    /// </summary>
    [Singleton]
    private class MultiHandlerB : IHandler<MultiMessage>
    {
        /// <summary>
        /// Gets the last message handled by this instance.
        /// </summary>
        public MultiMessage LastMessage { get; private set; } = null!;

        /// <summary>
        /// Stores the handled <paramref name="message"/>.
        /// </summary>
        /// <param name="message">Message instance to handle.</param>
        public void Handle(MultiMessage message) => LastMessage = message;
    }

    /// <summary>
    /// Check always succeeding for <see cref="MultiMessage"/>.
    /// </summary>
    [Singleton]
    private class MultiCheckA : ICheck<MultiMessage, string>
    {
        /// <inheritdoc />
        public bool Check(MultiMessage message, List<string> errors) => true;
    }

    /// <summary>
    /// Additional successful check for <see cref="MultiMessage"/>.
    /// </summary>
    [Singleton]
    private class MultiCheckB : ICheck<MultiMessage, string>
    {
        /// <inheritdoc />
        public bool Check(MultiMessage message, List<string> errors) => true;
    }

    /// <summary>
    /// Failing check adding its own error message.
    /// </summary>
    [Singleton]
    private class MultiFailCheckA : ICheck<MultiMessage, string>
    {
        /// <inheritdoc />
        public bool Check(MultiMessage message, List<string> errors)
        {
            errors.Add("errorA");
            return false;
        }
    }

    /// <summary>
    /// Second failing check adding its error.
    /// </summary>
    [Singleton]
    private class MultiFailCheckB : ICheck<MultiMessage, string>
    {
        /// <inheritdoc />
        public bool Check(MultiMessage message, List<string> errors)
        {
            errors.Add("errorB");
            return false;
        }
    }
}

