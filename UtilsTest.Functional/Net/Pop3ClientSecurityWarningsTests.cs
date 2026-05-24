using System;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace Utils.Tests.Net;

/// <summary>
/// Verifies compile-time warning metadata applied to potentially insecure network operations.
/// </summary>
[TestClass]
public class Pop3ClientSecurityWarningsTests
{
    /// <summary>
    /// Ensures POP3 USER/PASS authentication is annotated with an obsolete warning message.
    /// </summary>
    [TestMethod]
    public void AuthenticateAsync_ShouldExposeObsoleteWarningAttribute()
    {
        MethodInfo? method = typeof(Pop3Client).GetMethod(nameof(Pop3Client.AuthenticateAsync));
        Assert.IsNotNull(method);

        ObsoleteAttribute? attribute = method.GetCustomAttribute<ObsoleteAttribute>();
        Assert.IsNotNull(attribute);
        StringAssert.Contains(attribute.Message, "unencrypted");
        Assert.IsFalse(attribute.IsError);
    }

    /// <summary>
    /// Ensures SMTP authentication overload with explicit mechanism is annotated with an obsolete warning.
    /// </summary>
    [TestMethod]
    public void SmtpAuthenticateWithMechanism_ShouldExposeObsoleteWarningAttribute()
    {
        MethodInfo? method = typeof(SmtpClient).GetMethod(
            nameof(SmtpClient.AuthenticateAsync),
            new[] { typeof(string), typeof(string), typeof(SmtpAuthenticationMechanism), typeof(CancellationToken) });

        Assert.IsNotNull(method);

        ObsoleteAttribute? attribute = method.GetCustomAttribute<ObsoleteAttribute>();
        Assert.IsNotNull(attribute);
        StringAssert.Contains(attribute.Message, "unencrypted");
        Assert.IsFalse(attribute.IsError);
    }

    /// <summary>
    /// Ensures SMTP authentication overload using AUTH PLAIN shortcut is annotated with an obsolete warning.
    /// </summary>
    [TestMethod]
    public void SmtpAuthenticatePlainOverload_ShouldExposeObsoleteWarningAttribute()
    {
        MethodInfo? method = typeof(SmtpClient).GetMethod(
            nameof(SmtpClient.AuthenticateAsync),
            new[] { typeof(string), typeof(string), typeof(CancellationToken) });

        Assert.IsNotNull(method);

        ObsoleteAttribute? attribute = method.GetCustomAttribute<ObsoleteAttribute>();
        Assert.IsNotNull(attribute);
        StringAssert.Contains(attribute.Message, "unencrypted");
        Assert.IsFalse(attribute.IsError);
    }
}
