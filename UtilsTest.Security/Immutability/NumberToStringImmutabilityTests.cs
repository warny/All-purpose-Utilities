using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.Security.Immutability;

/// <summary>
/// Verifies that a <see cref="NumberToStringConverter"/> keeps an immutable snapshot of the options it was built from.
/// </summary>
[TestClass]
public sealed class NumberToStringImmutabilityTests
{
    /// <summary>Units of the synthetic two-level test language.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens of the synthetic two-level test language, indexed by digit.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Mutating the source fusion rule list after construction does not affect the converter snapshot.</summary>
    [TestMethod]
    public void Fusion_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var fusions = new List<FusionType> { new() { For = "1" } };
        var converter = new NumberToStringConverter(Options(fusions));

        fusions[0].Right = "changed";
        fusions.Add(new FusionType { For = "2" });

        Assert.AreEqual("twentyone", converter.Convert(new BigInteger(21)));
        Assert.AreEqual("twenty two", converter.Convert(new BigInteger(22)));
    }

    /// <summary>Creates options for a two-level language whose tens digit 2 carries <paramref name="fusions"/>.</summary>
    /// <param name="fusions">The fusion rules attached to the tens digit 2.</param>
    /// <returns>The converter options.</returns>
    private static NumberToStringConverterOptions Options(List<FusionType> fusions)
    {
        var units = new DigitListType { Digits = [.. Units.Select((u, i) => new DigitType(i, u))] };
        var tens = new DigitListType
        {
            Digits = [.. Tens.Select((t, i) => i == 2
                ? new DigitType(i, t, t + " *") { Fusions = fusions }
                : new DigitType(i, t, i == 0 ? "*" : t + " *"))],
        };
        return new NumberToStringConverterOptions
        {
            Group = 2,
            Zero = "zero",
            Minus = "minus *",
            Groups = new Dictionary<int, DigitListType> { [1] = units, [2] = tens },
            Scale = new NumberScale(["", "thousand"], ["illion"]),
        };
    }
}
