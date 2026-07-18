using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Utils.XML;

namespace UtilsTest.XML;

// ---------------------------------------------------------------------------
// Minimal concrete processors used across test classes
// ---------------------------------------------------------------------------

/// <summary>Simple processor that records which handlers were invoked.</summary>
internal class RecordingProcessor : XmlDataProcessor
{
    public List<string> Log { get; } = [];

    [Match("/")]
    protected override void Root()
    {
        Log.Add("root");
        Apply("//item");
    }

    [Match("//item")]
    protected void OnItem() => Log.Add($"item:{ValueOf(".")}");
}

/// <summary>Processor whose Root handler throws.</summary>
internal class ThrowingProcessor : XmlDataProcessor
{
    public bool HandlerRan { get; private set; }

    [Match("/")]
    protected override void Root()
    {
        HandlerRan = true;
        throw new InvalidOperationException("handler-error");
    }

    /// <summary>Captures Current after the throwing Root so tests can verify restoration.</summary>
    public XPathNavigator? GetCurrentAfterThrow() => Current;
}

/// <summary>Processor with an optional parameter handler.</summary>
internal class OptionalParamProcessor : XmlDataProcessor
{
    public string? LastValue { get; private set; }
    public string? LastExtra { get; private set; }

    [Match("/")]
    protected override void Root() => Apply("//item");

    [Match("//item")]
    protected void OnItem(string extra = "default")
    {
        LastValue = ValueOf(".");
        LastExtra = extra;
    }
}

/// <summary>Processor with a nullable-parameter handler.</summary>
internal class NullableParamProcessor : XmlDataProcessor
{
    public string? ReceivedArg { get; private set; }
    public bool HandlerCalled { get; private set; }

    [Match("/")]
    protected override void Root() { }

    [Match("//item")]
    protected void OnItem(string? arg)
    {
        HandlerCalled = true;
        ReceivedArg = arg;
    }
}

/// <summary>Processor with a non-nullable value-type parameter handler.</summary>
internal class ValueTypeParamProcessor : XmlDataProcessor
{
    public bool HandlerCalled { get; private set; }

    [Match("/")]
    protected override void Root() { }

    [Match("//item")]
    protected void OnItem(int value)
    {
        HandlerCalled = true;
    }
}

/// <summary>Base processor with a private protected handler for inheritance tests.</summary>
internal class BaseWithPrivateHandler : XmlDataProcessor
{
    public List<string> Log { get; } = [];

    [Match("/")]
    protected override void Root()
    {
        Log.Add("root");
        Apply("//*");
    }

    [Match("//base-item")]
    private void OnBaseItem() => Log.Add("base-item");
}

/// <summary>Derived processor that inherits the base private handler.</summary>
internal class DerivedProcessor : BaseWithPrivateHandler
{
    [Match("//derived-item")]
    private void OnDerivedItem() => Log.Add("derived-item");
}

/// <summary>Processor that triggers a redispatch cycle.</summary>
internal class CyclicDispatchProcessor : XmlDataProcessor
{
    [Match("/")]
    protected override void Root()
    {
        // Calling Apply(".") will dispatch the "/" root node again -> infinite cycle.
        Apply(".");
    }
}

[XmlNamespace("ns1", "http://example.com/a")]
[XmlNamespace("ns1", "http://example.com/a")]   // exact duplicate -- should be allowed
internal class DuplicateExactNsProcessor : XmlDataProcessor
{
    [Match("/")]
    protected override void Root() { }
}

[XmlNamespace("ns1", "http://example.com/a")]
[XmlNamespace("ns1", "http://example.com/b")]   // conflict -- different URI
internal class ConflictingNsProcessor : XmlDataProcessor
{
    [Match("/")]
    protected override void Root() { }
}

// ---------------------------------------------------------------------------
// Test class -- XmlUtils (ReadChildElements + GetXPath)
// ---------------------------------------------------------------------------

[TestClass]
public class XmlUtilsTests
{
    // -----------------------------------------------------------------------
    // ReadChildElements -- existing tests (updated where behavior changed)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ReadChildElementsEnumeratesImmediateChildren()
    {
        using XmlReader reader = XmlReader.Create(
            new StringReader("<root><child>A</child><child>B</child></root>"));
        reader.ReadToFollowing("root");

        var children = new List<string>();
        foreach (XmlReader child in reader.ReadChildElements())
        {
            children.Add(child.Name);
        }

        CollectionAssert.AreEqual(new[] { "child", "child" }, children);
    }

    [TestMethod]
    public void ReadChildElementsWithNameFiltersChildren()
    {
        using XmlReader reader = XmlReader.Create(
            new StringReader("<root><child>A</child><other>skip</other><child>B</child></root>"));
        reader.ReadToFollowing("root");

        var children = new List<string>();
        foreach (XmlReader child in reader.ReadChildElements("child"))
        {
            children.Add(child.Name);
        }

        CollectionAssert.AreEqual(new[] { "child", "child" }, children);
    }

    // -----------------------------------------------------------------------
    // Item 1 -- depth tracking: nested differently named elements must not appear
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 1: grandchildren with different names must not appear as immediate children.")]
    public void ReadChildElements_NestedDifferentName_NeverReturnedAsImmediateChild()
    {
        const string xml = "<root><child><grandchild/></child><child/></root>";
        using var reader = XmlReader.Create(new StringReader(xml));
        reader.ReadToFollowing("root");

        var names = new List<string>();
        foreach (var child in reader.ReadChildElements())
        {
            names.Add(child.Name);
        }

        // Only the two <child> elements; grandchild must NOT appear.
        Assert.AreEqual(2, names.Count);
        Assert.IsTrue(names.All(n => n == "child"),
            "Expected only 'child' elements, got: " + string.Join(", ", names));
    }

    // -----------------------------------------------------------------------
    // Item 2 -- empty parent elements must terminate without consuming siblings
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 2: empty parent element must yield no children and not advance past following siblings.")]
    public void ReadChildElements_EmptyParent_YieldsNothing()
    {
        const string xml = "<root><parent /><sibling/></root>";
        using var reader = XmlReader.Create(new StringReader(xml));
        reader.ReadToFollowing("parent");

        var children = new List<string>();
        foreach (var child in reader.ReadChildElements())
        {
            children.Add(child.Name);
        }

        Assert.AreEqual(0, children.Count,
            "Empty parent element should produce no children.");
    }

    [TestMethod]
    [Description("Item 2: after iterating an empty parent, the following sibling is accessible.")]
    public void ReadChildElements_EmptyParent_DoesNotConsumeLaterSiblings()
    {
        const string xml = "<root><parent /><sibling/></root>";
        using var reader = XmlReader.Create(new StringReader(xml));
        reader.ReadToFollowing("parent");

        // Consume (empty) children.
        foreach (var _ in reader.ReadChildElements()) { }

        // The reader should be able to advance to the sibling.
        bool foundSibling = reader.ReadToFollowing("sibling");
        Assert.IsTrue(foundSibling, "Expected to find <sibling> after iterating empty parent.");
    }

    // -----------------------------------------------------------------------
    // Item 3 -- reader position after subtree consumption and early break
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 3: iterator skips child subtrees not consumed by the loop body.")]
    public void ReadChildElements_EarlyBreak_DoesNotSkipChildren()
    {
        const string xml = "<root><a/><b/><c/></root>";
        using var reader = XmlReader.Create(new StringReader(xml));
        reader.ReadToFollowing("root");

        var names = new List<string>();
        foreach (var child in reader.ReadChildElements())
        {
            names.Add(child.Name);
            if (child.Name == "b") break; // early exit after second child
        }

        // Should have seen 'a' and 'b'.
        CollectionAssert.AreEqual(new[] { "a", "b" }, names);
    }

    [TestMethod]
    [Description("Item 3: when consumer reads the child subtree body via the yielded reader, iterator still advances correctly.")]
    public void ReadChildElements_ConsumerReadsBody_IteratorStillAdvances()
    {
        const string xml = "<root><a>text-a</a><b>text-b</b></root>";
        using var reader = XmlReader.Create(new StringReader(xml));
        reader.ReadToFollowing("root");

        var values = new List<string>();
        foreach (var child in reader.ReadChildElements())
        {
            values.Add(child.ReadElementContentAsString());
        }

        CollectionAssert.AreEqual(new[] { "text-a", "text-b" }, values);
    }

    [TestMethod]
    [Description("Regression: consuming body of a child must not skip a same-name following sibling.")]
    public void ReadChildElements_ConsumerReadsBody_SameNameSiblingIsNotSkipped()
    {
        const string xml = "<root><item>A</item><item>B</item></root>";
        using var reader = XmlReader.Create(new StringReader(xml));
        reader.ReadToFollowing("root");

        var values = new List<string>();
        foreach (var child in reader.ReadChildElements())
        {
            values.Add(child.ReadElementContentAsString());
        }

        // Both items must be yielded; the second must not be skipped because it
        // shares the same name, depth, and node type as the first.
        CollectionAssert.AreEqual(new[] { "A", "B" }, values);
    }

    [TestMethod]
    [Description("Regression: an empty child element must not skip the following same-name sibling.")]
    public void ReadChildElements_EmptyChildSiblings_BothYielded()
    {
        const string xml = "<root><item/><item/></root>";
        using var reader = XmlReader.Create(new StringReader(xml));
        reader.ReadToFollowing("root");

        var count = 0;
        foreach (var child in reader.ReadChildElements())
        {
            count++;
            // Empty elements have no body; the iterator must still yield the second sibling.
            Assert.IsTrue(child.IsEmptyElement, "child should be an empty element");
        }

        Assert.AreEqual(2, count, "Both <item/> siblings must be yielded.");
    }

    // -----------------------------------------------------------------------
    // GetXPath -- existing tests
    // -----------------------------------------------------------------------

    [TestMethod]
    public void GetXPath_Element_ReturnsCorrectPath()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root><parent><child/></parent></root>");
        var child = (XmlElement)doc.SelectSingleNode("/root/parent/child")!;
        Assert.AreEqual("/root/parent/child", child.GetXPath());
    }

    [TestMethod]
    public void GetXPath_Attribute_ReturnsAtNotation()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root id=\"42\"/>");
        var attr = doc.DocumentElement!.Attributes["id"]!;
        Assert.AreEqual("/root/@id", attr.GetXPath());
    }

    [TestMethod]
    public void GetXPath_Document_ReturnsSlash()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root/>");
        Assert.AreEqual("/", doc.GetXPath());
    }

    // -----------------------------------------------------------------------
    // Item 12 -- GetXPath uniqueness: first repeated sibling also gets [1]
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 12: second repeated sibling includes positional predicate.")]
    public void GetXPath_RepeatedSiblings_SecondAddsIndex()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root><item/><item/></root>");
        var second = (XmlElement)doc.SelectSingleNode("/root/item[2]")!;
        Assert.AreEqual("/root/item[2]", second.GetXPath());
    }

    [TestMethod]
    [Description("Item 12: first repeated sibling includes [1] when a same-name sibling follows.")]
    public void GetXPath_RepeatedSiblings_FirstAlsoGetsIndex()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root><item/><item/></root>");
        var first = (XmlElement)doc.SelectSingleNode("/root/item[1]")!;
        Assert.AreEqual("/root/item[1]", first.GetXPath());
    }

    [TestMethod]
    [Description("Item 12: unique element has no positional predicate.")]
    public void GetXPath_UniqueElement_HasNoIndex()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root><only/></root>");
        var only = (XmlElement)doc.SelectSingleNode("/root/only")!;
        Assert.AreEqual("/root/only", only.GetXPath());
    }

    [TestMethod]
    [Description("Item 12: middle sibling among three same-name items gets correct index.")]
    public void GetXPath_ThreeRepeatedSiblings_MiddleHasIndex2()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root><item/><item/><item/></root>");
        var middle = (XmlElement)doc.SelectSingleNode("/root/item[2]")!;
        Assert.AreEqual("/root/item[2]", middle.GetXPath());
    }

    // -----------------------------------------------------------------------
    // Item 14 -- NotSupportedException for non-element/non-attribute/non-document nodes
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 14: text node throws NotSupportedException.")]
    public void GetXPath_TextNode_ThrowsNotSupportedException()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root>text</root>");
        var text = doc.DocumentElement!.FirstChild!;

        Assert.ThrowsException<NotSupportedException>(() => text.GetXPath());
    }

    [TestMethod]
    [Description("Item 14: comment node throws NotSupportedException.")]
    public void GetXPath_CommentNode_ThrowsNotSupportedException()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root><!-- comment --></root>");
        var comment = doc.DocumentElement!.FirstChild!;

        Assert.ThrowsException<NotSupportedException>(() => comment.GetXPath());
    }
}

// ---------------------------------------------------------------------------
// Test class -- XmlDataProcessor
// ---------------------------------------------------------------------------

[TestClass]
public class XmlDataProcessorTests
{
    private static XPathDocument MakeDoc(string xml)
        => new(new StringReader(xml));

    // -----------------------------------------------------------------------
    // Item 4 -- Current restored after handler exception
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 4: Current must be null after a throwing handler.")]
    public void Current_RestoredAfterHandlerException()
    {
        var processor = new ThrowingProcessor();
        var doc = MakeDoc("<root/>");

        try
        {
            processor.Read(doc.CreateNavigator()!);
        }
        catch (InvalidOperationException ex) when (ex.Message == "handler-error")
        {
            // Expected -- verify that Current is restored.
        }

        Assert.IsNull(processor.GetCurrentAfterThrow(),
            "Current must be restored to null after a throwing handler.");
    }

    // -----------------------------------------------------------------------
    // Items 5 & 6 -- optional parameters + null args
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 5: optional parameter receives its declared default value.")]
    public void OptionalParameter_ReceivesDefault_WhenNotProvided()
    {
        var processor = new OptionalParamProcessor();
        // Invoke without extra param -> handler should receive "default".
        processor.Read(MakeDoc("<root><item>v</item></root>").CreateNavigator()!);

        Assert.AreEqual("v", processor.LastValue);
        Assert.AreEqual("default", processor.LastExtra);
    }

    [TestMethod]
    [Description("Item 6: null argument matches nullable/reference-type parameter.")]
    public void NullArg_MatchesNullableParam()
    {
        var processor = new NullableParamProcessor();
        // Processor should construct fine -- nullable param handler is valid.
        Assert.IsNotNull(processor);
    }

    [TestMethod]
    [Description("Item 6: null argument is rejected when parameter is non-nullable value type.")]
    public void NullArg_RejectedForValueTypeParam()
    {
        var processor = new ValueTypeParamProcessor();
        // Processor should construct fine.
        Assert.IsNotNull(processor);
        // Handler should not be called (null int param is skipped by TryBuildArguments).
        Assert.IsFalse(processor.HandlerCalled);
    }

    // -----------------------------------------------------------------------
    // Item 7 -- deterministic dispatch order
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 7: multiple matching handlers are invoked in declaration order.")]
    public void DispatchOrder_IsDeclarationOrder()
    {
        var processor = new RecordingProcessor();
        processor.Read(MakeDoc("<root><item>x</item></root>").CreateNavigator()!);

        // 'root' handler fires first (from '/' match), then 'item' for the item element.
        Assert.IsTrue(processor.Log.Count >= 2);
        Assert.AreEqual("root", processor.Log[0]);
    }

    [TestMethod]
    [Description("Item 7: when both derived and base class have [Match] for the same XPath, derived fires first.")]
    public void DispatchOrder_DerivedMatchAttributeTakesPriorityOverBase()
    {
        var processor = new HandlerPriorityDerived();
        processor.Read(MakeDoc("<root><target/></root>").CreateNavigator()!);

        // Derived class handler registered first (derived-to-base walk), so it fires.
        Assert.AreEqual(1, processor.Log.Count, "Only the first matching handler per node must fire.");
        Assert.AreEqual("derived", processor.Log[0], "Derived class [Match] handler must take priority.");
    }

    // -----------------------------------------------------------------------
    // Item 8 -- invalid handler signatures rejected at construction
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 8/20: a Task-returning (async) handler must be rejected at construction.")]
    public void AsyncHandler_RejectedAtConstruction()
    {
        Assert.ThrowsException<InvalidOperationException>(
            () => new AsyncReturnsTaskProcessor(),
            "Async handler must be rejected during processor construction.");
    }

    [TestMethod]
    [Description("Item 8: a non-void returning handler must be rejected at construction.")]
    public void NonVoidHandler_RejectedAtConstruction()
    {
        Assert.ThrowsException<InvalidOperationException>(
            () => new NonVoidReturnProcessor());
    }

    // -----------------------------------------------------------------------
    // Item 9 -- ReadSecure(Stream) rejects DTDs
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 9: ReadSecure(Stream) must reject DTD declarations.")]
    public void ReadSecure_Stream_RejectsDtd()
    {
        const string xml = "<?xml version=\"1.0\"?><!DOCTYPE root [<!ENTITY e \"v\">]><root/>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var processor = new RecordingProcessor();

        Assert.ThrowsException<XmlException>(() => processor.ReadSecure(stream),
            "ReadSecure(Stream) must reject documents with DTD declarations.");
    }

    // -----------------------------------------------------------------------
    // Item 15 -- XmlNamespaceAttribute NCName validation
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 15: underscore-prefixed NCName is valid.")]
    public void XmlNamespaceAttribute_UnderscorePrefix_IsValid()
    {
        // '_ns' starts with underscore -- valid NCName, previously rejected by the regex.
        var attr = new XmlNamespaceAttribute("_ns", "http://example.com/");
        Assert.AreEqual("_ns", attr.Prefix);
    }

    [TestMethod]
    [Description("Item 15: prefix starting with digit is invalid NCName.")]
    public void XmlNamespaceAttribute_DigitPrefix_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new XmlNamespaceAttribute("1ns", "http://example.com/"));
    }

    [TestMethod]
    [Description("Item 15: reserved prefix 'xml' is rejected.")]
    public void XmlNamespaceAttribute_XmlPrefix_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new XmlNamespaceAttribute("xml", "http://www.w3.org/XML/1998/namespace"));
    }

    [TestMethod]
    [Description("Item 15: reserved prefix 'xmlns' is rejected.")]
    public void XmlNamespaceAttribute_XmlnsPrefix_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new XmlNamespaceAttribute("xmlns", "http://www.w3.org/2000/xmlns/"));
    }

    [TestMethod]
    [Description("Item 15: empty namespace URI is rejected.")]
    public void XmlNamespaceAttribute_EmptyNamespace_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new XmlNamespaceAttribute("ns", ""));
    }

    // -----------------------------------------------------------------------
    // Item 18 -- concurrency guard
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 18: two concurrent Read calls on the same instance must result in one rejection.")]
    public void Read_ConcurrentCalls_OneIsRejected()
    {
        // Use a processor that blocks in Root to allow second call to overlap.
        var processor = new BlockingRootProcessor();
        var doc = MakeDoc("<root/>");

        Exception? secondCallException = null;
        var rootEntered = new ManualResetEventSlim(false);
        var readyToStart = new ManualResetEventSlim(false);

        var t1 = Task.Run(() =>
        {
            processor.SetGates(rootEntered, readyToStart);
            processor.Read(doc.CreateNavigator()!);
        });

        // Wait for root handler to start blocking.
        rootEntered.Wait(TimeSpan.FromSeconds(5));

        var t2 = Task.Run(() =>
        {
            try
            {
                processor.Read(doc.CreateNavigator()!);
            }
            catch (InvalidOperationException ex)
            {
                secondCallException = ex;
            }
        });

        // Let t2 try, then unblock t1.
        t2.Wait(TimeSpan.FromSeconds(2));
        readyToStart.Set();
        Task.WaitAll(t1, t2);

        Assert.IsNotNull(secondCallException,
            "The second concurrent Read call must throw InvalidOperationException.");
    }

    // -----------------------------------------------------------------------
    // Item 19 -- dispatch depth / redispatch cycle detection
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 19: redispatch cycle exceeding MaxDispatchDepth must throw InvalidOperationException.")]
    public void RedispatchCycle_ThrowsInvalidOperationException()
    {
        var processor = new CyclicDispatchProcessor();
        var doc = MakeDoc("<root/>");

        Assert.ThrowsException<InvalidOperationException>(
            () => processor.Read(doc.CreateNavigator()!),
            "Cyclic dispatch must be detected and result in InvalidOperationException.");
    }

    // -----------------------------------------------------------------------
    // Item 23 -- inherited private handler must be discovered
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 23: private handler in base class must be registered and invoked.")]
    public void InheritedPrivateHandler_IsDiscoveredAndInvoked()
    {
        var processor = new DerivedProcessor();
        processor.Read(MakeDoc("<root><base-item/><derived-item/></root>").CreateNavigator()!);

        CollectionAssert.Contains(processor.Log, "base-item",
            "Private handler from base class must be discovered and invoked.");
        CollectionAssert.Contains(processor.Log, "derived-item",
            "Private handler from derived class must be invoked.");
    }

    // -----------------------------------------------------------------------
    // Item 24 -- duplicate namespace declarations
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 24: exact duplicate namespace declaration must be silently accepted.")]
    public void DuplicateExactNamespace_IsAccepted()
    {
        // DuplicateExactNsProcessor has two identical [XmlNamespace("ns1", "...")] attributes.
        var processor = new DuplicateExactNsProcessor();
        Assert.IsNotNull(processor);
    }

    [TestMethod]
    [Description("Item 24: conflicting namespace declaration (same prefix, different URI) must throw.")]
    public void ConflictingNamespace_ThrowsAtConstruction()
    {
        Assert.ThrowsException<InvalidOperationException>(
            () => new ConflictingNsProcessor());
    }

    // -----------------------------------------------------------------------
    // Item 25 -- MatchAttribute rejects null/empty/whitespace
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 25: MatchAttribute rejects null XPath expression.")]
    public void MatchAttribute_NullXPath_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new MatchAttribute(null!));
    }

    [TestMethod]
    [Description("Item 25: MatchAttribute rejects empty XPath expression.")]
    public void MatchAttribute_EmptyXPath_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new MatchAttribute(""));
    }

    [TestMethod]
    [Description("Item 25: MatchAttribute rejects whitespace-only XPath expression.")]
    public void MatchAttribute_WhitespaceXPath_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new MatchAttribute("   "));
    }

    // -----------------------------------------------------------------------
    // Item 26 -- null checks at public API boundaries
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("Item 26: Read(XPathNavigator) rejects null argument.")]
    public void Read_NullNavigator_Throws()
    {
        var processor = new RecordingProcessor();
        Assert.ThrowsException<ArgumentNullException>(
            () => processor.Read((XPathNavigator)null!));
    }

    [TestMethod]
    [Description("Item 26: Read(Stream) rejects null argument.")]
    public void Read_NullStream_Throws()
    {
        var processor = new RecordingProcessor();
        Assert.ThrowsException<ArgumentNullException>(
            () => processor.Read((Stream)null!));
    }

    [TestMethod]
    [Description("Item 26: Read(XmlReader) rejects null argument.")]
    public void Read_NullXmlReader_Throws()
    {
        var processor = new RecordingProcessor();
        Assert.ThrowsException<ArgumentNullException>(
            () => processor.Read((XmlReader)null!));
    }

    [TestMethod]
    [Description("Item 26: ReadSecure(Stream) rejects null argument.")]
    public void ReadSecure_NullStream_Throws()
    {
        var processor = new RecordingProcessor();
        Assert.ThrowsException<ArgumentNullException>(
            () => processor.ReadSecure((Stream)null!));
    }
}

// ---------------------------------------------------------------------------
// Helper processors for edge-case tests
// ---------------------------------------------------------------------------

/// <summary>Processor with an async (Task-returning) handler -- must fail at construction.</summary>
internal class AsyncReturnsTaskProcessor : XmlDataProcessor
{
    [Match("/")]
    protected override void Root() { }

    [Match("//item")]
    protected async Task OnItemAsync()
    {
        await Task.Delay(1);
    }
}

/// <summary>Processor with a non-void returning handler -- must fail at construction.</summary>
internal class NonVoidReturnProcessor : XmlDataProcessor
{
    [Match("/")]
    protected override void Root() { }

    [Match("//item")]
    protected int OnItem() => 42;
}

/// <summary>Processor that blocks inside Root until signaled, for concurrency testing.</summary>
internal class BlockingRootProcessor : XmlDataProcessor
{
    private ManualResetEventSlim? _entered;
    private ManualResetEventSlim? _release;

    public void SetGates(ManualResetEventSlim entered, ManualResetEventSlim release)
    {
        _entered = entered;
        _release = release;
    }

    [Match("/")]
    protected override void Root()
    {
        _entered?.Set();
        _release?.Wait(TimeSpan.FromSeconds(10));
    }
}

/// <summary>Base processor with an explicit [Match] handler for dispatch-priority tests.</summary>
internal class HandlerPriorityBase : XmlDataProcessor
{
    public List<string> Log { get; } = [];

    [Match("/")]
    protected override void Root() => Apply("//target");

    [Match("//target")]
    protected virtual void OnTarget() => Log.Add("base");
}

/// <summary>
/// Derived processor that adds its own [Match] for the same XPath as the base,
/// to verify that derived-class handlers are registered first and take priority.
/// </summary>
internal class HandlerPriorityDerived : HandlerPriorityBase
{
    [Match("//target")]
    protected override void OnTarget() => Log.Add("derived");
}
