using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.VisualStudio;

namespace UtilsTest.Functional.Parser;

[TestClass]
public class VisualStudioSyntaxColorizationDescriptorApiTests
{
    [TestMethod]
    public void Descriptor_CollectionsExposeReadOnlyContracts()
    {
        Assert.AreEqual(typeof(IReadOnlyList<string>), typeof(SyntaxColorizationDescriptor).GetProperty(nameof(SyntaxColorizationDescriptor.FileExtensions))!.PropertyType);
        Assert.AreEqual(typeof(IReadOnlyList<string>), typeof(SyntaxColorizationDescriptor).GetProperty(nameof(SyntaxColorizationDescriptor.StringSyntaxExtensions))!.PropertyType);
        Assert.AreEqual(typeof(IReadOnlyList<SyntaxColorizationDescriptorEntry>), typeof(SyntaxColorizationDescriptor).GetProperty(nameof(SyntaxColorizationDescriptor.Entries))!.PropertyType);
        Assert.AreEqual(typeof(IReadOnlyList<string>), typeof(SyntaxColorizationDescriptorEntry).GetProperty(nameof(SyntaxColorizationDescriptorEntry.Rules))!.PropertyType);
    }

    [TestMethod]
    public void Descriptor_CollectionsRejectConsumerMutation()
    {
        var descriptor = new SyntaxColorizationDescriptor();
        var entry = new SyntaxColorizationDescriptorEntry("Keyword");

        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)descriptor.FileExtensions).Add(".sql"));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)descriptor.StringSyntaxExtensions).Add("SQL"));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<SyntaxColorizationDescriptorEntry>)descriptor.Entries).Add(entry));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)entry.Rules).Add("RULE"));
    }
}
