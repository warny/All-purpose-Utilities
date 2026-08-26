using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using Utils.Dates;
using Utils.Expressions;
using Utils.Fonts;
using Utils.Fonts.TTF.Tables;
using Utils.Fonts.TTF.Tables.CMap;
using Utils.IO.Serialization;
using Utils.OData.Linq;

namespace UtilsTest.Security.Immutability;

/// <summary>Verifies the immutable collection boundaries identified by the final repository audit.</summary>
[TestClass]
public sealed class RemainingCollectionImmutabilityTests
{
    /// <summary>Verifies that font reference name tables are immutable and remain lookup-consistent.</summary>
    [TestMethod]
    public void FontSupport_NameTablesAreImmutableAndConsistent()
    {
        string originalName = FontSupport.StdNames[1];

        Assert.IsFalse(FontSupport.StdNames is string[]);
        Assert.IsFalse(FontSupport.StdValues is string[]);
        Assert.IsFalse(FontSupport.MacExtras is string[]);
        Assert.AreEqual(originalName, FontSupport.GetName(1));
        Assert.AreEqual(1, FontSupport.GetStrIndex(originalName));
        Assert.AreEqual(FontSupport.MacExtras[0], FontSupport.GetName(FontSupport.StdNames.Count));
    }

    /// <summary>Verifies that every numeric font reference table hides mutable array storage.</summary>
    [TestMethod]
    public void FontSupport_NumericTablesAreImmutable()
    {
        IReadOnlyList<int>[] tables =
        [
            FontSupport.Type1CExpertCharset,
            FontSupport.Type1CExpertSubCharset,
            FontSupport.MacRomanEncoding,
            FontSupport.IsoLatin1Encoding,
            FontSupport.WinAnsiEncoding,
            FontSupport.StandardEncoding
        ];

        Assert.IsTrue(tables.All(table => table is not int[]));
        Assert.AreEqual(1, FontSupport.Type1CExpertCharset[0]);
        Assert.AreEqual(1, FontSupport.StandardEncoding[32]);
    }

    /// <summary>Verifies that numeric type classifications cannot be changed through their public values.</summary>
    [TestMethod]
    public void Types_NumericClassificationsAreImmutable()
    {
        IReadOnlyList<Type>[] groups =
        [
            Utils.Objects.Types.Number,
            Utils.Objects.Types.UnsignedNumber,
            Utils.Objects.Types.SignedNumber,
            Utils.Objects.Types.FloatingPointNumber,
            Utils.Objects.Types._8BitsNumberI,
            Utils.Objects.Types._16BitsNumberI,
            Utils.Objects.Types._32BitsNumberI,
            Utils.Objects.Types._32BitsNumberF,
            Utils.Objects.Types._64BitsNumberI,
            Utils.Objects.Types._64BitsNumberIF,
            Utils.Objects.Types._128BitsNumberIF
        ];

        Assert.IsTrue(groups.All(group => group is not Type[]));
        CollectionAssert.Contains(Utils.Objects.Types.Number.ToArray(), typeof(decimal));
        CollectionAssert.AreEqual(new[] { typeof(byte), typeof(ushort), typeof(uint), typeof(ulong) },
            Utils.Objects.Types.UnsignedNumber.ToArray());
    }

    /// <summary>Verifies that numeric expression signature attributes own immutable value snapshots.</summary>
    [TestMethod]
    public void ConstantNumericAttribute_ValuesAreImmutableSnapshot()
    {
        double[] source = [1, 2];
        var attribute = new ConstantNumericAttribute(source);

        source[0] = 9;

        Assert.AreEqual(1D, attribute.Values![0]);
        Assert.IsFalse(attribute.Values is double[]);
    }

    /// <summary>Verifies that a format 4 table map owns one coherent snapshot in both lookup directions.</summary>
    [TestMethod]
    public void CMapFormat4_TableMapOwnsCoherentSnapshot()
    {
        short[] source = [41, 0, 43];
        var map = new CMapFormat4.TableMap('A', 'C', source);

        source[0] = 99;
        source[2] = 99;

        Assert.AreEqual((short)41, map['A']);
        Assert.AreEqual((short)43, map['C']);
        Assert.AreEqual('A', map[(short)41]);
        Assert.AreEqual('C', map[(short)43]);
        Assert.AreEqual('\0', map[(short)0]);
    }

    /// <summary>Verifies that date language initialization copies values and retains case comparison semantics.</summary>
    [TestMethod]
    public void DateFormulaLanguage_DaysAreImmutableWithSourceComparer()
    {
        var source = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
        {
            ["MO"] = DayOfWeek.Monday
        };
        DateFormulaLanguage language = CreateLanguage(source);

        source["MO"] = DayOfWeek.Friday;
        source["TU"] = DayOfWeek.Tuesday;

        Assert.AreEqual(DayOfWeek.Monday, language.Days["mo"]);
        Assert.IsFalse(language.Days.ContainsKey("TU"));
        Assert.IsFalse(language.Days is Dictionary<string, DayOfWeek>);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase,
            ((ImmutableDictionary<string, DayOfWeek>)language.Days).KeyComparer);
    }

    /// <summary>Verifies that mutable sorted day mappings retain compatible equality comparers.</summary>
    [TestMethod]
    public void DateFormulaLanguage_DaysPreserveSortedDictionaryComparer()
    {
        var source = new SortedDictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
        {
            ["MO"] = DayOfWeek.Monday
        };
        DateFormulaLanguage language = CreateLanguage(source);

        source["MO"] = DayOfWeek.Friday;

        Assert.AreEqual(DayOfWeek.Monday, language.Days["mo"]);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase,
            ((ImmutableDictionary<string, DayOfWeek>)language.Days).KeyComparer);
    }

    /// <summary>Verifies that immutable sorted day mappings retain compatible equality comparers.</summary>
    [TestMethod]
    public void DateFormulaLanguage_DaysPreserveImmutableSortedDictionaryComparer()
    {
        ImmutableSortedDictionary<string, DayOfWeek> source =
            ImmutableSortedDictionary.Create<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
                .Add("MO", DayOfWeek.Monday);
        DateFormulaLanguage language = CreateLanguage(source);

        Assert.AreEqual(DayOfWeek.Monday, language.Days["mo"]);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase,
            ((ImmutableDictionary<string, DayOfWeek>)language.Days).KeyComparer);
    }

    /// <summary>Verifies that serialization diagnostics are enumerated once into immutable storage.</summary>
    [TestMethod]
    public void SerializationContractException_DiagnosticsAreSingleImmutableSnapshot()
    {
        var diagnostic = new SerializationContractDiagnostic("TEST", "Original");
        var source = new SingleUseEnumerable<SerializationContractDiagnostic>([diagnostic]);

        var exception = new SerializationContractException(typeof(string), source);

        Assert.AreEqual(1, source.EnumerationCount);
        Assert.AreSame(diagnostic, exception.Diagnostics[0]);
        Assert.IsFalse(exception.Diagnostics is SerializationContractDiagnostic[]);
        StringAssert.Contains(exception.Message, "[TEST] Original");
    }

    /// <summary>Verifies that compiled OData filters and expansions are immutable snapshots.</summary>
    [TestMethod]
    public void ODataQueryCompilation_CollectionsAreImmutableSnapshots()
    {
        var filters = new List<string> { "Name eq 'Ada'" };
        string[] expansions = ["Orders"];
        var compilation = new ODataQueryCompilation("People", filters, expansions);

        filters[0] = "Name eq 'Grace'";
        expansions[0] = "Manager";

        Assert.AreEqual("Name eq 'Ada'", compilation.Filters[0]);
        Assert.AreEqual("Orders", compilation.Expansions[0]);
        Assert.IsFalse(compilation.Filters is string[]);
        Assert.IsFalse(compilation.Expansions is string[]);
        Assert.AreEqual("People?$expand=Orders&$filter=Name%20eq%20%27Ada%27", compilation.ToUriString());
    }

    /// <summary>Verifies that an accent description retains an ordered immutable extension snapshot.</summary>
    [TestMethod]
    public void AcntTable_MultipleExtensionsAreImmutableSnapshot()
    {
        var first = new AcntTable.ExtensionEntry(1, 2);
        var source = new List<AcntTable.ExtensionEntry> { first };
        var description = new AcntTable.AccentDescription.Multiple(12, source);

        source[0] = new AcntTable.ExtensionEntry(3, 4);
        source.Add(new AcntTable.ExtensionEntry(5, 6));

        Assert.AreEqual(1, description.Extensions.Count);
        Assert.AreEqual(first, description.Extensions[0]);
        Assert.IsFalse(description.Extensions is AcntTable.ExtensionEntry[]);
        Assert.IsFalse(description.Extensions is List<AcntTable.ExtensionEntry>);
    }

    /// <summary>Verifies that numeric suffix matching retains its configured culture-insensitive comparer.</summary>
    [TestMethod]
    public void ParserOptions_NumberSuffixesRetainCurrentCultureIgnoreCaseComparer()
    {
        using var culture = new CultureScope(CultureInfo.GetCultureInfo("tr-TR"));
        var options = new ParserOptions();
        var suffixes = (ImmutableDictionary<string, Func<string, object>>)options.NumberSuffixes;

        Assert.IsTrue(suffixes.KeyComparer.Equals("i", "İ"));
        Assert.IsFalse(StringComparer.OrdinalIgnoreCase.Equals("i", "İ"));
        Assert.AreEqual(12L, options.NumberSuffixes["L"]("12"));
        Assert.AreEqual(15F, options.NumberSuffixes["F"]("15"));
    }

    /// <summary>Creates a complete date formula language around the supplied day mapping.</summary>
    private static DateFormulaLanguage CreateLanguage(IReadOnlyDictionary<string, DayOfWeek> days) => new()
    {
        Start = '<',
        End = '>',
        Day = 'D',
        Week = 'W',
        Month = 'M',
        Quarter = 'Q',
        Year = 'Y',
        WorkingDay = 'B',
        Days = days
    };

    /// <summary>Provides an enumerable that fails if a consumer requests more than one enumeration.</summary>
    private sealed class SingleUseEnumerable<T>(IEnumerable<T> values) : IEnumerable<T>
    {
        /// <summary>Gets the number of enumerators requested.</summary>
        public int EnumerationCount { get; private set; }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            Assert.AreEqual(1, EnumerationCount, "The sequence must be materialized exactly once.");
            return values.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Temporarily changes the current culture for comparer verification.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo originalCulture = CultureInfo.CurrentCulture;

        /// <summary>Initializes the scope with the requested culture.</summary>
        public CultureScope(CultureInfo culture) => CultureInfo.CurrentCulture = culture;

        /// <summary>Restores the culture active before the scope was created.</summary>
        public void Dispose() => CultureInfo.CurrentCulture = originalCulture;
    }
}
