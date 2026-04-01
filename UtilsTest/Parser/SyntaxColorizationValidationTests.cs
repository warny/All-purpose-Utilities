using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Generators.Internal;

namespace UtilsTest.Parser;

/// <summary>
/// Tests validation helpers used by the syntax colorization source generator.
/// </summary>
[TestClass]
public class SyntaxColorizationValidationTests
{
    [TestMethod]
    public void TryValidateTypeMetadata_ReturnsFalseForInvalidClassName()
    {
        bool isValid = SyntaxColorizationValidation.TryValidateTypeMetadata(
            "MyApp.Syntax",
            "Invalid-Name",
            out string errorMessage);

        Assert.IsFalse(isValid);
        StringAssert.Contains(errorMessage, "ClassName");
    }

    [TestMethod]
    public void TryValidateTypeMetadata_ReturnsFalseForInvalidNamespace()
    {
        bool isValid = SyntaxColorizationValidation.TryValidateTypeMetadata(
            "MyApp..Syntax",
            "ValidName",
            out string errorMessage);

        Assert.IsFalse(isValid);
        StringAssert.Contains(errorMessage, "Namespace");
    }

    [TestMethod]
    public void TryValidateDescriptor_ReturnsFalseWhenExtensionsAreMissing()
    {
        var descriptor = new SyntaxColorizationDescriptor();
        descriptor.Entries.Add(new SyntaxColorizationEntry("Keyword"));

        bool isValid = SyntaxColorizationValidation.TryValidateDescriptor(descriptor, out string errorMessage);

        Assert.IsFalse(isValid);
        StringAssert.Contains(errorMessage, "@FileExtension");
    }

    [TestMethod]
    public void TryValidateDescriptor_ReturnsFalseWhenSectionsAreMissing()
    {
        var descriptor = new SyntaxColorizationDescriptor();
        descriptor.FileExtensions.Add(".sql");

        bool isValid = SyntaxColorizationValidation.TryValidateDescriptor(descriptor, out string errorMessage);

        Assert.IsFalse(isValid);
        StringAssert.Contains(errorMessage, "classification section");
    }
}
