using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
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
                "SEMI : ';' ;\n" +
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
            StringAssert.Contains(descriptor, "SEMI");
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
    public void SyntaxColorisationDescriptorProfile_ClassifiesDirectivesMethodsAndOperators()
    {
        var registry = new VisualStudioSyntaxColorisationRegistry();
        IReadOnlyList<ISyntaxColorisation> profiles = registry.DiscoverFromAssemblies(new[] { typeof(G4SyntaxColorisation).Assembly });

        ISyntaxColorisation descriptorProfile = profiles.First(profile => profile.FileExtensions.Contains(".syntaxcolor"));

        Assert.AreEqual(VisualStudioClassificationNames.Keyword, descriptorProfile.GetClassification("@FileExtension"));
        Assert.AreEqual(VisualStudioClassificationNames.Keyword, descriptorProfile.GetClassification("Keyword"));
        Assert.AreEqual(VisualStudioClassificationNames.Operator, descriptorProfile.GetClassification(":"));
        Assert.AreEqual(VisualStudioClassificationNames.Operator, descriptorProfile.GetClassification("|"));
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
    public void Registry_LoadFromDescriptorFiles_SkipsMalformedDescriptor()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"syntax-{Guid.NewGuid():N}.syntaxcolor");

        try
        {
            File.WriteAllText(filePath, """
                @UnknownDirective : "value"
                """);

            var registry = new VisualStudioSyntaxColorisationRegistry();

            IReadOnlyList<ISyntaxColorisation> profiles = registry.LoadFromDescriptorFiles(new[] { filePath });
            Assert.AreEqual(0, profiles.Count);
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
    public void Registry_DiscoverFromAssemblies_SkipsProfilesThatFailInstantiation()
    {
        var registry = new VisualStudioSyntaxColorisationRegistry();
        IReadOnlyList<ISyntaxColorisation> profiles = registry.DiscoverFromAssemblies(new[] { typeof(FaultySyntaxColorisation).Assembly });
        Assert.IsNotNull(profiles);
    }

    [TestMethod]
    public void Registry_LoadProfiles_LoadsProfilesFromAssemblyFilePaths()
    {
        var registry = new VisualStudioSyntaxColorisationRegistry();
        MethodInfo? loadProfilesWithFiles = typeof(VisualStudioSyntaxColorisationRegistry)
            .GetMethod("LoadProfiles", BindingFlags.Instance | BindingFlags.NonPublic, null, new[]
            {
                typeof(IEnumerable<Assembly>),
                typeof(IEnumerable<string>),
                typeof(IEnumerable<string>)
            }, null);

        Assert.IsNotNull(loadProfilesWithFiles);

        object? rawResult = loadProfilesWithFiles!.Invoke(
            registry,
            new object[]
            {
                System.Array.Empty<Assembly>(),
                System.Array.Empty<string>(),
                new[] { typeof(G4SyntaxColorisation).Assembly.Location }
            });

        Assert.IsNotNull(rawResult);
        var profiles = (IReadOnlyList<ISyntaxColorisation>)rawResult;
        Assert.IsTrue(profiles.Any(profile => profile.FileExtensions.Contains(".g4")));
    }

    [TestMethod]
    public void Registry_LoadProfiles_DisablesProblematicExternalAssemblyWithoutThrowing()
    {
        var registry = new VisualStudioSyntaxColorisationRegistry();
        MethodInfo? loadProfilesWithFiles = typeof(VisualStudioSyntaxColorisationRegistry)
            .GetMethod("LoadProfiles", BindingFlags.Instance | BindingFlags.NonPublic, null, new[]
            {
                typeof(IEnumerable<Assembly>),
                typeof(IEnumerable<string>),
                typeof(IEnumerable<string>)
            }, null);

        Assert.IsNotNull(loadProfilesWithFiles);

        string directory = Path.Combine(Path.GetTempPath(), $"problematic-{Guid.NewGuid():N}");
        string problematicAssemblyPath = Path.Combine(directory, "BrokenColorisation.dll");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(problematicAssemblyPath, "invalid assembly");

            object[] parameters =
            {
                System.Array.Empty<Assembly>(),
                System.Array.Empty<string>(),
                new[] { problematicAssemblyPath }
            };

            object? firstResult = loadProfilesWithFiles!.Invoke(registry, parameters);
            object? secondResult = loadProfilesWithFiles.Invoke(registry, parameters);

            Assert.IsNotNull(firstResult);
            Assert.IsNotNull(secondResult);

            var firstProfiles = (IReadOnlyList<ISyntaxColorisation>)firstResult;
            var secondProfiles = (IReadOnlyList<ISyntaxColorisation>)secondResult;

            Assert.AreEqual(0, firstProfiles.Count);
            Assert.AreEqual(0, secondProfiles.Count);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // ── AssemblySecurityInspector tests ────────────────────────────────

    [TestMethod]
    public void SecurityInspector_SafeAssembly_PassesInspection()
    {
        // Utils.Parser.Runtime.dll has no dangerous references (no File, Net, Process, P/Invoke).
        string assemblyPath = typeof(G4SyntaxColorisation).Assembly.Location;
        Assert.IsTrue(File.Exists(assemblyPath), "Assembly must exist on disk");

        object?[] args = [assemblyPath, null];
        bool isSafe = InvokeInspector(args);
        var violations = (IReadOnlyList<string>)args[1]!;

        Assert.IsTrue(isSafe, $"Expected safe; violations: {string.Join(", ", violations)}");
        Assert.AreEqual(0, violations.Count);
    }

    [TestMethod]
    public void SecurityInspector_NonExistentFile_FailsGracefully()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.dll");

        object?[] args = [missingPath, null];
        bool isSafe = InvokeInspector(args);
        var violations = (IReadOnlyList<string>)args[1]!;

        Assert.IsFalse(isSafe);
        Assert.IsTrue(violations.Count > 0);
    }

    [TestMethod]
    public void SecurityInspector_InvalidPeContent_FailsGracefully()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"invalid-{Guid.NewGuid():N}.dll");
        try
        {
            File.WriteAllText(filePath, "this is not a PE file");

            object?[] args = [filePath, null];
            bool isSafe = InvokeInspector(args);
            var violations = (IReadOnlyList<string>)args[1]!;

            Assert.IsFalse(isSafe);
            Assert.IsTrue(violations.Count > 0);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [TestMethod]
    public void SecurityInspector_RejectedAssembly_IsNotLoadedByRegistry()
    {
        // The "invalid assembly" bytes file fails inspection (not a valid PE).
        // The registry must mark it as problematic and return 0 profiles on both calls.
        var registry = new VisualStudioSyntaxColorisationRegistry();
        MethodInfo? loadProfilesWithFiles = typeof(VisualStudioSyntaxColorisationRegistry)
            .GetMethod("LoadProfiles", BindingFlags.Instance | BindingFlags.NonPublic, null,
            [
                typeof(IEnumerable<Assembly>),
                typeof(IEnumerable<string>),
                typeof(IEnumerable<string>)
            ], null);

        Assert.IsNotNull(loadProfilesWithFiles);

        string directory = Path.Combine(Path.GetTempPath(), $"rejected-{Guid.NewGuid():N}");
        string assemblyPath = Path.Combine(directory, "Dangerous.dll");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(assemblyPath, "not a PE");

            object[] parameters = [System.Array.Empty<Assembly>(), System.Array.Empty<string>(), new[] { assemblyPath }];

            var first = (IReadOnlyList<ISyntaxColorisation>)loadProfilesWithFiles!.Invoke(registry, parameters)!;
            var second = (IReadOnlyList<ISyntaxColorisation>)loadProfilesWithFiles.Invoke(registry, parameters)!;

            Assert.AreEqual(0, first.Count);
            Assert.AreEqual(0, second.Count);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Invokes <c>AssemblySecurityInspector.IsSafe</c> via reflection (class is internal).
    /// </summary>
    private static bool InvokeInspector(object?[] args)
    {
        Type inspectorType = typeof(VisualStudioSyntaxColorisationRegistry).Assembly
            .GetType("Utils.Parser.VisualStudio.AssemblySecurityInspector")!;
        MethodInfo isSafe = inspectorType.GetMethod("IsSafe", BindingFlags.Public | BindingFlags.Static)!;
        bool result = (bool)isSafe.Invoke(null, args)!;
        // args[1] is now populated with the out IReadOnlyList<string> violations
        return result;
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
