using System.Collections.Generic;
using System.Text;

namespace Utils.NumberToString;

/// <summary>
/// Produces Hungarian ordinals. The ordinal suffix attaches to the last morpheme of the numeral
/// with vowel harmony and stem changes (<c>tizenegyedik</c>, <c>huszadik</c>, <c>századik</c>,
/// <c>ezredik</c>, <c>milliomodik</c>); only the numbers 1 and 2 standing alone are suppletive
/// (<c>első</c>, <c>második</c>), whereas they are regular at the end of a compound
/// (<c>huszonegyedik</c>, <c>tizenkettedik</c>). Numbers up to 2000 are written as one word and
/// larger ones separate their thousand groups with hyphens (<c>kétezer-egyedik</c>); a single
/// million or milliard is written with <c>egy</c> (<c>egymilliomodik</c>, <c>egymillió-egyedik</c>).
/// </summary>
/// <remarks>
/// The non-ordinal prefix is rebuilt here because <see cref="IOrdinalLanguageSpecifics"/> receives
/// no converter; it uses the attributive <c>két</c> before another morpheme. Only the
/// <see cref="int"/> range is implemented; larger values are declined and fail closed.
/// </remarks>
public sealed class HungarianOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    private static readonly string[] s_units = ["", "egy", "két", "három", "négy", "öt", "hat", "hét", "nyolc", "kilenc"];
    private static readonly string[] s_tens = ["", "tíz", "húsz", "harminc", "negyven", "ötven", "hatvan", "hetven", "nyolcvan", "kilencven"];
    private static readonly string[] s_tensWithUnits = ["", "tizen", "huszon", "harminc", "negyven", "ötven", "hatvan", "hetven", "nyolcvan", "kilencven"];
    private static readonly string[] s_scales = ["", "ezer", "millió", "milliárd"];

    private static readonly string[] s_ordinalUnits =
        ["", "egyedik", "kettedik", "harmadik", "negyedik", "ötödik", "hatodik", "hetedik", "nyolcadik", "kilencedik"];
    private static readonly string[] s_ordinalTens =
        ["", "tizedik", "huszadik", "harmincadik", "negyvenedik", "ötvenedik", "hatvanadik", "hetvenedik", "nyolcvanadik", "kilencvenedik"];
    private static readonly string[] s_ordinalScales = ["századik", "ezredik", "milliomodik", "milliárdodik"];

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        result = number switch
        {
            <= 0 => null,
            1 => "első",
            2 => "második",
            _ => BuildOrdinal(number),
        };
        return result != null;
    }

    /// <summary>Builds the ordinal of a number greater than two.</summary>
    /// <param name="number">A number greater than two.</param>
    /// <returns>The ordinal wording.</returns>
    private static string BuildOrdinal(int number)
    {
        var groups = new List<(int Value, int Scale)>();
        for (int scale = 3, divisor = 1_000_000_000; scale >= 0; scale--, divisor /= 1000)
        {
            int value = number / divisor % 1000;
            if (value > 0)
                groups.Add((value, scale));
        }

        var chunks = new List<string>(groups.Count);
        for (int i = 0; i < groups.Count; i++)
        {
            var (value, scale) = groups[i];
            bool last = i == groups.Count - 1;
            var chunk = new StringBuilder();
            // A single thousand is the bare "ezer" (ezredik, ezeregyedik); a single million or milliard
            // keeps "egy" (MTA "Számok" tool, AkH12-292: egymilliomodik, egymillió-egyedik).
            if (!(value == 1 && scale == 1))
                AppendGroup(chunk, value, ordinalEnd: last && scale == 0);
            if (scale > 0)
                chunk.Append(last ? s_ordinalScales[scale] : s_scales[scale]);
            chunks.Add(chunk.ToString());
        }

        return string.Join(number > 2000 ? "-" : string.Empty, chunks);
    }

    /// <summary>Appends the morphemes of a group between 1 and 999.</summary>
    /// <param name="builder">The target builder.</param>
    /// <param name="group">A number between 1 and 999.</param>
    /// <param name="ordinalEnd">Whether the group's last morpheme ends the ordinal.</param>
    private static void AppendGroup(StringBuilder builder, int group, bool ordinalEnd)
    {
        int hundreds = group / 100;
        int tens = group / 10 % 10;
        int units = group % 10;
        if (hundreds > 0)
        {
            if (hundreds > 1)
                builder.Append(s_units[hundreds]);
            builder.Append(ordinalEnd && tens == 0 && units == 0 ? s_ordinalScales[0] : "száz");
        }
        if (tens > 0)
        {
            if (units > 0)
                builder.Append(s_tensWithUnits[tens]);
            else
                builder.Append(ordinalEnd ? s_ordinalTens[tens] : s_tens[tens]);
        }
        if (units > 0)
            builder.Append(ordinalEnd ? s_ordinalUnits[units] : s_units[units]);
    }
}
