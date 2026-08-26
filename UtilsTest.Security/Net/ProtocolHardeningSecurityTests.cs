using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Security.Net;

/// <summary>
/// Verifies strict protocol models introduced for the pass-seven fixes: rejection of SMTP
/// envelope syntax that could escape or extend a command line (including CRLF injection),
/// explicit opt-in for SMTPUTF8, rejection of the SASL PLAIN delimiter byte inside credential
/// fields, rejection of negative payload limits, and redaction of command arguments in
/// structured protocol failures.
/// </summary>
[TestClass]
public class ProtocolHardeningSecurityTests
{
    /// <summary>Verifies syntax that could escape or extend an SMTP envelope is rejected.</summary>
    [TestMethod]
    [DataRow("user@example.com> SIZE=1")]
    [DataRow("<user@example.com")]
    [DataRow("user @example.com")]
    [DataRow("@route:user@example.com")]
    [DataRow("user@example.com\rRCPT TO:<evil@example.com>")]
    public void SmtpPath_Parse_RejectsUnsafeSyntax(string value) => Assert.ThrowsExactly<FormatException>(() => SmtpPath.Parse(value));

    /// <summary>Verifies SMTPUTF8 is opt-in and never silently substituted.</summary>
    [TestMethod]
    public void SmtpPath_Parse_Utf8RequiresExplicitOptIn()
    {
        Assert.ThrowsExactly<FormatException>(() => SmtpPath.Parse("tést@example.com"));
        Assert.AreEqual("tést@example.com", SmtpPath.Parse("tést@example.com", true).Value);
    }

    /// <summary>Verifies each SASL PLAIN field rejects the delimiter byte.</summary>
    [TestMethod]
    public void SmtpPlainCredentials_RejectNulInEveryField()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SmtpPlainCredentials("a\0b", "password"));
        Assert.ThrowsExactly<ArgumentException>(() => new SmtpPlainCredentials("user", "pass\0word"));
        Assert.ThrowsExactly<ArgumentException>(() => new SmtpPlainCredentials("user", "password", "a\0z"));
    }

    /// <summary>Verifies payload limits reject negative values at assignment.</summary>
    [TestMethod]
    public void ProtocolPayloadLimits_RejectNegativeValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProtocolPayloadLimits { MaximumLines = -1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProtocolPayloadLimits { MaximumCharacters = -1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProtocolPayloadLimits { MaximumBytes = -1 });
    }

    /// <summary>Verifies structured failures copy responses and redact command arguments.</summary>
    [TestMethod]
    public void ProtocolResponseException_ProvidesStructuredRedactedDiagnostics()
    {
        ServerResponse[] responses = [new("535", ResponseSeverity.PermanentNegative, "authentication failed")];
        ProtocolResponseException error = new("SMTP", "AUTH secret", responses);
        responses[0] = new ServerResponse("250", ResponseSeverity.Completion, "changed");
        Assert.AreEqual("AUTH", error.Command);
        Assert.AreEqual("535", error.ResponseCode);
        Assert.AreEqual(ResponseSeverity.PermanentNegative, error.Severity);
        Assert.AreEqual("535", error.Responses[0].Code);
        Assert.IsFalse(error.Message.Contains("secret", StringComparison.Ordinal));
    }
}
