using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;
using Utils.Parser.VisualStudio;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for Visual Studio colorization profile loading and built-in G4 profile behavior.
/// </summary>
[TestClass]
public class VisualStudioSyntaxColorisationTests
{
    [TestMethod]
    public void DescriptorFileParser_ParseFile_ReadsDescriptorFromDisk()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"syntax-{Guid.NewGuid():N}.syntaxcolor");

        try
        {
            File.WriteAllText(filePath, """
                @FileExtension : ".demo"
                @StringSyntaxExtension : "DEMO" // inline comment
                # file comment
                Keyword :
                    FOR | IF
                """);

            var parser = new SyntaxColorizationDescriptorFileParser();
            SyntaxColorizationDescriptor descriptor = parser.ParseFile(filePath);

            CollectionAssert.AreEquivalent(new[] { ".demo" }, descriptor.FileExtensions);
            CollectionAssert.AreEquivalent(new[] { "DEMO" }, descriptor.StringSyntaxExtensions);
            Assert.AreEqual("Keyword", descriptor.Entries[0].Classification);
            CollectionAssert.AreEquivalent(new[] { "FOR", "IF" }, descriptor.Entries[0].Rules);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [TestMethod]
    public void Registry_LoadProfiles_DiscoversAssemblyAndDescriptorProfiles()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"syntax-{Guid.NewGuid():N}.syntaxcolor");

        try
        {
            File.WriteAllText(filePath, """
                @FileExtension : ".sqlx"
                Number :
                    NUMBER
                """);

            var registry = new VisualStudioSyntaxColorisationRegistry();
            var profiles = registry.LoadProfiles(new[] { typeof(G4SyntaxColorisation).Assembly }, new[] { filePath });

            Assert.IsTrue(profiles.Any(profile => profile.FileExtensions.Contains(".g4")));
            Assert.AreEqual(VisualStudioClassificationNames.Keyword, profiles[0].GetClassification("grammar"));
            Assert.IsTrue(profiles.Any(profile => profile.FileExtensions.Contains(".sqlx")));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [TestMethod]
    public void G4DescriptorScaffolder_CreateFromGrammarFile_GeneratesDefaultSections()
    {
        string grammarFilePath = Path.Combine(Path.GetTempPath(), $"grammar-{Guid.NewGuid():N}.g4");

        try
        {
            File.WriteAllText(grammarFilePath,
                "grammar DemoGrammar;\n" +
                "root : FOR NUMBER QUOTED_STRING TEXT PLUS LPAREN RPAREN LBRACE RBRACE ;\n" +
                "FOR : 'for' ;\n" +
                "NUMBER : [0-9]+ ;\n" +
                "QUOTED_STRING : '\"' (~[\"\\r\\n])* '\"' ;\n" +
                "TEXT : [a-zA-Z_]+ ;\n" +
                "PLUS : '+' ;\n" +
                "LPAREN : '(' ;\n" +
                "RPAREN : ')' ;\n" +
                "LBRACE : '{' ;\n" +
                "RBRACE : '}' ;\n" +
                "WS : [ \\t\\r\\n]+ -> skip ;\n");

            var scaffolder = new G4ColorizationDescriptorScaffolder();
            string descriptor = scaffolder.CreateFromGrammarFile(grammarFilePath, new[] { ".demo" });

            StringAssert.Contains(descriptor, "@FileExtension : \".demo\"");
            StringAssert.Contains(descriptor, "@StringSyntaxExtension : \"Demo\"");
            StringAssert.Contains(descriptor, "Number :");
            StringAssert.Contains(descriptor, "NUMBER");
            StringAssert.Contains(descriptor, "String :");
            StringAssert.Contains(descriptor, "QUOTED_STRING");
            StringAssert.Contains(descriptor, "Keyword :");
            StringAssert.Contains(descriptor, "FOR");
            StringAssert.Contains(descriptor, "Operator :");
            StringAssert.Contains(descriptor, "PLUS");
            StringAssert.Contains(descriptor, "LPAREN");
            StringAssert.Contains(descriptor, "RBRACE");
            StringAssert.Contains(descriptor, "# Tag rule example:");
            StringAssert.Contains(descriptor, "# Tag : TAG_OPEN | TAG_CLOSE");
            StringAssert.Contains(descriptor, "# Unused standard rules:");
            StringAssert.Contains(descriptor, "\"Raw text\" :");
            StringAssert.Contains(descriptor, "TEXT");
        }
        finally
        {
            if (File.Exists(grammarFilePath))
            {
                File.Delete(grammarFilePath);
            }
        }
    }

    [TestMethod]
    public void G4SyntaxColorisation_ClassifiesStringAndNumbersByConvention()
    {
        var colorisation = G4SyntaxColorisation.Instance;

        Assert.AreEqual(VisualStudioClassificationNames.Keyword, colorisation.GetClassification("grammar"));
        Assert.AreEqual(VisualStudioClassificationNames.Number, colorisation.GetClassification("NUMBER"));
        Assert.AreEqual(VisualStudioClassificationNames.String, colorisation.GetClassification("MULTILINE_STRING"));
        Assert.AreEqual(".g4", colorisation.FileExtensions[0]);
    }

    [TestMethod]
    public void Extension_GetClassificationOrDefault_ReturnsContextDefaultsWhenProfileFails()
    {
        var extension = new VisualStudioSyntaxColorisationExtension();
        var failingProfile = new ThrowingSyntaxColorisation();

        string fileContextClassification = extension.GetClassificationOrDefault(failingProfile, "rule", isStringSyntaxContext: false);
        string stringContextClassification = extension.GetClassificationOrDefault(failingProfile, new[] { "rule" }, isStringSyntaxContext: true);

        Assert.AreEqual(VisualStudioClassificationNames.Text, fileContextClassification);
        Assert.AreEqual(VisualStudioClassificationNames.String, stringContextClassification);
    }

    [TestMethod]
    public void Extension_GetSecondaryProfilesForFileExtension_SkipsProjectProfilesWhenInstalledSupportExists()
    {
        var extension = new VisualStudioSyntaxColorisationExtension();
        ISyntaxColorisation profile = new ConventionSyntaxColorisation(
            new[] { ".sql" },
            new[] { "SQL" },
            new[] { "SELECT" },
            System.Array.Empty<string>(),
            System.Array.Empty<string>());

        IReadOnlyList<ISyntaxColorisation> selectedWithoutInstalledSupport =
            extension.GetSecondaryProfilesForFileExtension(new[] { profile }, ".sql", System.Array.Empty<string>());
        IReadOnlyList<ISyntaxColorisation> selectedWithInstalledSupport =
            extension.GetSecondaryProfilesForFileExtension(new[] { profile }, ".sql", new[] { ".SQL" });

        Assert.AreEqual(1, selectedWithoutInstalledSupport.Count);
        Assert.AreEqual(0, selectedWithInstalledSupport.Count);
    }

    [TestMethod]
    public void Extension_GetSecondaryProfilesForStringSyntax_UsesProjectProfilesWhenNoInstalledSupportExists()
    {
        var extension = new VisualStudioSyntaxColorisationExtension();
        ISyntaxColorisation profile = new ConventionSyntaxColorisation(
            new[] { ".sql" },
            new[] { "SQL" },
            new[] { "SELECT" },
            System.Array.Empty<string>(),
            System.Array.Empty<string>());

        IReadOnlyList<ISyntaxColorisation> selectedWithoutInstalledSupport =
            extension.GetSecondaryProfilesForStringSyntax(new[] { profile }, "SQL", System.Array.Empty<string>());
        IReadOnlyList<ISyntaxColorisation> selectedWithInstalledSupport =
            extension.GetSecondaryProfilesForStringSyntax(new[] { profile }, "SQL", new[] { "sql" });

        Assert.AreEqual(1, selectedWithoutInstalledSupport.Count);
        Assert.AreEqual(0, selectedWithInstalledSupport.Count);
    }

    [TestMethod]
    public void Registry_LoadFromDescriptorFiles_ThrowsWhenDescriptorIsMalformed()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"syntax-{Guid.NewGuid():N}.syntaxcolor");

        try
        {
            File.WriteAllText(filePath, """
                @UnknownDirective : "value"
                """);

            var registry = new VisualStudioSyntaxColorisationRegistry();

            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
                () => registry.LoadFromDescriptorFiles(new[] { filePath }));

            StringAssert.Contains(ex.Message, "Descriptor");
            StringAssert.Contains(ex.Message, "invalid");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [TestMethod]
    public void Registry_DiscoverFromAssemblies_ThrowsWhenProfileInstantiationFails()
    {
        var registry = new VisualStudioSyntaxColorisationRegistry();

        InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
            () => registry.DiscoverFromAssemblies(new[] { typeof(FaultySyntaxColorisation).Assembly }));

        StringAssert.Contains(ex.Message, nameof(FaultySyntaxColorisation));
        StringAssert.Contains(ex.Message, "Failed to create syntax colorization profile");
    }

    /// <summary>
    /// Represents a profile that always throws to validate fallback behavior.
    /// </summary>
    private sealed class ThrowingSyntaxColorisation : ISyntaxColorisation
    {
        /// <summary>
        /// Gets file extensions and always throws.
        /// </summary>
        public IReadOnlyList<string> FileExtensions => throw new InvalidOperationException("Test failure.");

        /// <summary>
        /// Gets StringSyntax extensions and always throws.
        /// </summary>
        public IReadOnlyList<string> StringSyntaxExtensions => throw new InvalidOperationException("Test failure.");

        /// <summary>
        /// Gets a classification by rule path and always throws.
        /// </summary>
        /// <param name="rulePath">Rule path.</param>
        /// <returns>Never returns.</returns>
        public string? GetClassification(IEnumerable<string> rulePath)
        {
            throw new InvalidOperationException("Test failure.");
        }

        /// <summary>
        /// Gets a classification by rule name and always throws.
        /// </summary>
        /// <param name="ruleName">Rule name.</param>
        /// <returns>Never returns.</returns>
        public string? GetClassification(string ruleName)
        {
            throw new InvalidOperationException("Test failure.");
        }
    }

    /// <summary>
    /// Represents a profile with a failing constructor to validate registry error reporting.
    /// </summary>
    public sealed class FaultySyntaxColorisation : ISyntaxColorisation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FaultySyntaxColorisation"/> class.
        /// </summary>
        public FaultySyntaxColorisation()
        {
            throw new InvalidOperationException("Constructor failure.");
        }

        /// <inheritdoc />
        public IReadOnlyList<string> FileExtensions => System.Array.Empty<string>();

        /// <inheritdoc />
        public IReadOnlyList<string> StringSyntaxExtensions => System.Array.Empty<string>();

        /// <inheritdoc />
        public string? GetClassification(IEnumerable<string> rulePath)
        {
            return null;
        }

        /// <inheritdoc />
        public string? GetClassification(string ruleName)
        {
            return null;
        }
    }
}
