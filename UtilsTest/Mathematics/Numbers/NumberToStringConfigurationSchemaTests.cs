using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Xml;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>Verifies secure schema handling for number-to-string configuration documents.</summary>
[TestClass]
public class NumberToStringConfigurationSchemaTests
{
    private const string ValidConfiguration = """
        <?xml version="1.0" encoding="utf-8"?>
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
          <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999">
            <Culture>SCHEMA-TEST</Culture>
            <Groups><Group level="1"><Digit digit="0" string=""/><Digit digit="1" string="one"/><Digit digit="2" string="two"/><Digit digit="3" string="three"/><Digit digit="4" string="four"/><Digit digit="5" string="five"/><Digit digit="6" string="six"/><Digit digit="7" string="seven"/><Digit digit="8" string="eight"/><Digit digit="9" string="nine"/></Group></Groups>
            <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string=""/></StaticNames><Suffixes><Suffix>on</Suffix></Suffixes></NumberScale>
          </Language>
        </Numbers>
        """;

    /// <summary>Ensures every configuration shipped by the package conforms to the embedded XSD.</summary>
    [TestMethod]
    public void BuiltInConfigurations_AreSchemaValid()
    {
        foreach (string configuration in NumberToStringConverter.BuiltInConfigurations)
            NumberToStringConverter.ValidateConfigurationSchemaForTesting(configuration);
    }

    /// <summary>Ensures unknown elements are rejected by external configuration parsing.</summary>
    [TestMethod]
    public void ExternalConfiguration_UnknownElement_IsRejected()
        => AssertSchemaFailure(ValidConfiguration.Replace("<Groups>", "<Unknown/><Groups>", StringComparison.Ordinal));

    /// <summary>Ensures unknown attributes are rejected by external configuration parsing.</summary>
    [TestMethod]
    public void ExternalConfiguration_UnknownAttribute_IsRejected()
        => AssertSchemaFailure(ValidConfiguration.Replace("groupSize=", "typoAttribute=\"value\" groupSize=", StringComparison.Ordinal));

    /// <summary>Ensures required attributes are enforced by external configuration parsing.</summary>
    [TestMethod]
    public void ExternalConfiguration_MissingRequiredAttribute_IsRejected()
        => AssertSchemaFailure(ValidConfiguration.Replace(" digit=\"1\"", "", StringComparison.Ordinal));

    /// <summary>Ensures restricted digit values are enforced by external configuration parsing.</summary>
    [TestMethod]
    public void ExternalConfiguration_OutOfRangeValue_IsRejected()
        => AssertSchemaFailure(ValidConfiguration.Replace("digit=\"1\"", "digit=\"10\"", StringComparison.Ordinal));

    /// <summary>Ensures schema enumerations are enforced by external configuration parsing.</summary>
    [TestMethod]
    public void ExternalConfiguration_InvalidEnumeration_IsRejected()
        => AssertSchemaFailure(ValidConfiguration.Replace(
            "</NumberScale>",
            "</NumberScale><Replacements><Replacement oldValue=\"one\" newValue=\"uno\" scope=\"InvalidScope\"/></Replacements>",
            StringComparison.Ordinal));

    /// <summary>Ensures sequence ordering is enforced by external configuration parsing.</summary>
    [TestMethod]
    public void ExternalConfiguration_InvalidElementOrder_IsRejected()
    {
        string groups = "<Groups><Group level=\"1\"><Digit digit=\"0\" string=\"\"/><Digit digit=\"1\" string=\"one\"/></Group></Groups>";
        AssertSchemaFailure(ValidConfiguration.Replace(groups, "", StringComparison.Ordinal).Replace("</NumberScale>", $"</NumberScale>{groups}", StringComparison.Ordinal));
    }

    /// <summary>Ensures skipping XSD validation retains secure DTD processing.</summary>
    [TestMethod]
    public void SkipSchemaValidation_Dtd_IsRejected()
    {
        string document = ValidConfiguration.Replace(
            "<Numbers",
            "<!DOCTYPE Numbers [<!ENTITY xxe SYSTEM \"file:///does-not-exist\">]><Numbers",
            StringComparison.Ordinal);
        Assert.ThrowsException<InvalidOperationException>(() =>
            NumberToStringConverter.BuildConfigurationForTesting(document, NumberToStringConverter.ConfigurationSchemaValidation.Skip));
    }

    /// <summary>Ensures Skip disables only XSD validation while retaining the semantic pipeline.</summary>
    [TestMethod]
    public void SchemaPolicies_DifferOnlyAtSchemaValidation()
    {
        string document = ValidConfiguration.Replace("groupSize=", "typoAttribute=\"value\" groupSize=", StringComparison.Ordinal);
        AssertSchemaFailure(document);
        NumberToStringConverter.BuildConfigurationForTesting(document, NumberToStringConverter.ConfigurationSchemaValidation.Skip);
    }

    /// <summary>Ensures a schema-invalid batch does not publish preceding valid documents.</summary>
    [TestMethod]
    public void RegisterConfigurations_SchemaFailure_IsAtomic()
    {
        string culture = $"ATOMIC-{Guid.NewGuid():N}";
        string valid = ValidConfiguration.Replace("SCHEMA-TEST", culture, StringComparison.Ordinal);
        string invalid = ValidConfiguration.Replace("digit=\"1\"", "digit=\"10\"", StringComparison.Ordinal);

        Assert.ThrowsException<XmlSchemaValidationException>(() => NumberToStringConverter.RegisterConfigurations([valid, invalid]));
        Assert.IsFalse(NumberToStringConverter.TryGetConverter(culture, out _));
    }

    /// <summary>Asserts that external parsing reports a schema error with source coordinates.</summary>
    private static void AssertSchemaFailure(string configuration)
    {
        XmlSchemaValidationException exception = Assert.ThrowsException<XmlSchemaValidationException>(
            () => NumberToStringConverter.ReadConfiguration(configuration));
        Assert.IsTrue(exception.LineNumber > 0);
        Assert.IsTrue(exception.LinePosition > 0);
    }
}
