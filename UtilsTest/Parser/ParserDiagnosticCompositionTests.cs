using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using Utils.Parser.Diagnostics;
using Utils.Parser.Source;

namespace UtilsTest.Parser;

/// <summary>
/// Tests immutable composed parser diagnostic source/location contracts.
/// </summary>
[TestClass]
public class ParserDiagnosticCompositionTests
{
    [TestMethod]
    public void SourceCodeLocation_ValidatesConstructorArguments_AndFormatsDisplayText()
    {
        Assert.ThrowsException<ArgumentException>(() => new SourceCodeLocation(" ", 1, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SourceCodeLocation("file.ext", 0, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SourceCodeLocation("file.ext", 1, 0));

        var location = new SourceCodeLocation("file.ext", 12, 5);
        Assert.AreEqual("file.ext", location.FilePath);
        Assert.AreEqual(12, location.Line);
        Assert.AreEqual(5, location.Column);
        Assert.AreEqual("file.ext(12,5)", location.ToString());
    }

    [TestMethod]
    public void SourceCodeRange_InheritsLocation_ValidatesLength_AndFormatsDisplayText()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SourceCodeRange("file.ext", 1, 1, -1));

        SourceCodeLocation rangeAsLocation = new SourceCodeRange("file.ext", 12, 5, 3);
        var range = (SourceCodeRange)rangeAsLocation;
        Assert.AreEqual("file.ext", range.FilePath);
        Assert.AreEqual(12, range.Line);
        Assert.AreEqual(5, range.Column);
        Assert.AreEqual(3, range.Length);
        Assert.AreEqual("file.ext(12,5,3)", range.ToString());
    }

    [TestMethod]
    public void DiagnosticSpan_ValidatesStartAndLength()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DiagnosticSpan(-1, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DiagnosticSpan(0, -1));

        var span = new DiagnosticSpan(10, 4);
        Assert.AreEqual(10, span.Start);
        Assert.AreEqual(4, span.Length);
    }

    [TestMethod]
    public void ParserDiagnostic_UsesComposedDetailsSpanAndLocation_WithReadOnlyFacadeProperties()
    {
        var descriptor = ParserDiagnostics.SemanticPredicateNotEnforced;
        var details = new DiagnosticDetails(descriptor, "Message", "rule", new InvalidOperationException("boom"));
        var span = new DiagnosticSpan(8, 2);
        var location = new SourceCodeRange("file.ext", 3, 9, 2);

        var diagnostic = new ParserDiagnostic(details, span, location);

        Assert.AreSame(details, diagnostic.Details);
        Assert.AreSame(span, diagnostic.Span);
        Assert.AreSame(location, diagnostic.Location);
        Assert.AreEqual(descriptor, diagnostic.Descriptor);
        Assert.AreEqual(descriptor.Code, diagnostic.Code);
        Assert.AreEqual(descriptor.Severity, diagnostic.Severity);
        Assert.AreEqual("Message", diagnostic.Message);
        Assert.AreEqual(8, diagnostic.SpanStart);
        Assert.AreEqual(2, diagnostic.SpanLength);
        Assert.AreEqual("rule", diagnostic.RuleName);
        Assert.IsInstanceOfType<InvalidOperationException>(diagnostic.Exception);

        Assert.AreEqual("file.ext", diagnostic.FilePath);
        Assert.AreEqual(3, diagnostic.Line);
        Assert.AreEqual(9, diagnostic.Column);

        Assert.IsFalse(typeof(ParserDiagnostic).GetProperty(nameof(ParserDiagnostic.FilePath))?.CanWrite ?? true);
        Assert.IsFalse(typeof(ParserDiagnostic).GetProperty(nameof(ParserDiagnostic.Line))?.CanWrite ?? true);
        Assert.IsFalse(typeof(ParserDiagnostic).GetProperty(nameof(ParserDiagnostic.Column))?.CanWrite ?? true);
    }

    [TestMethod]
    public void ParserDiagnostic_ToDisplayString_UsesLocationWhenAvailable()
    {
        var descriptor = ParserDiagnostics.SemanticPredicateNotEnforced;
        var withoutLocation = new ParserDiagnostic(descriptor, "Message");
        var withLocation = new ParserDiagnostic(
            new DiagnosticDetails(descriptor, "Message"),
            new DiagnosticSpan(0, 0),
            new SourceCodeLocation("file.ext", 12, 5));

        Assert.AreEqual($"{descriptor.Code}: Message", withoutLocation.ToDisplayString());
        Assert.AreEqual($"file.ext(12,5): warning {descriptor.Code}: Message", withLocation.ToDisplayString());
    }

    [TestMethod]
    public void DiagnosticBag_AddWithContext_WithLegacySpanParameters_BuildsDiagnosticSpan()
    {
        var bag = new DiagnosticBag();
        var descriptor = ParserDiagnostics.SemanticPredicateNotEnforced;

        var diagnostic = bag.AddWithContext(descriptor, 4, 2, "rule", null);

        Assert.AreEqual(1, bag.Count);
        Assert.IsNotNull(diagnostic.Span);
        Assert.AreEqual(4, diagnostic.Span?.Start);
        Assert.AreEqual(2, diagnostic.Span?.Length);
        Assert.IsNull(diagnostic.Location);
        Assert.AreEqual(descriptor.Code, diagnostic.Code);
    }
}
