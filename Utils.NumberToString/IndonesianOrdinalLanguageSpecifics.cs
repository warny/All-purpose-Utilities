using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Produces Indonesian ordinals, including suppletive <c>pertama</c> and productive
/// <c>ke-</c> forms over the Indonesian cardinal system.
/// </summary>
public class IndonesianOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    /// <summary>Gets the language-specific word for eight.</summary>
    protected virtual string Eight => "delapan";

    /// <summary>Gets the language-specific word for 10^9.</summary>
    protected virtual string Billion => "miliar";

    /// <summary>Gets the language-specific word for 10^12.</summary>
    protected virtual string Trillion => "triliun";

    /// <summary>Gets the language-specific word for 10^15.</summary>
    protected virtual string Quadrillion => "kuadriliun";

    /// <summary>Gets the language-specific word for 10^18.</summary>
    protected virtual string Quintillion => "kuintiliun";

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
        => TryConvertOrdinal((long)number, activeVariants, out result);

    /// <inheritdoc />
    public bool TryConvertOrdinal(long number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        if (number <= 0)
        {
            result = null;
            return false;
        }

        result = number == 1 ? "pertama" : $"ke{BuildCardinal(number)}";
        return true;
    }

    /// <summary>Builds the cardinal stem used by the productive ordinal prefix.</summary>
    /// <param name="number">A positive integer.</param>
    /// <returns>The Indonesian or Malay cardinal wording.</returns>
    protected string BuildCardinal(long number)
    {
        // Keep these stems and scale words synchronized with the ID/MS XML cardinal configurations.
        // IOrdinalLanguageSpecifics currently has no converter context from which to request a cardinal form.
        string[] units = ["", "satu", "dua", "tiga", "empat", "lima", "enam", "tujuh", Eight, "sembilan"];
        if (number < 10) return units[(int)number];
        if (number == 10) return "sepuluh";
        if (number == 11) return "sebelas";
        if (number < 20) return $"{units[(int)(number - 10)]} belas";
        if (number < 100) return Join($"{units[(int)(number / 10)]} puluh", BuildCardinal(number % 10));
        if (number < 200) return Join("seratus", BuildCardinal(number - 100));
        if (number < 1000) return Join($"{units[(int)(number / 100)]} ratus", BuildCardinal(number % 100));
        if (number < 2000) return Join("seribu", BuildCardinal(number - 1000));
        if (number < 1_000_000) return Join($"{BuildCardinal(number / 1000)} ribu", BuildCardinal(number % 1000));
        if (number < 1_000_000_000) return Join($"{BuildCardinal(number / 1_000_000)} juta", BuildCardinal(number % 1_000_000));
        if (number < 1_000_000_000_000) return Join($"{BuildCardinal(number / 1_000_000_000)} {Billion}", BuildCardinal(number % 1_000_000_000));
        if (number < 1_000_000_000_000_000) return Join($"{BuildCardinal(number / 1_000_000_000_000)} {Trillion}", BuildCardinal(number % 1_000_000_000_000));
        if (number < 1_000_000_000_000_000_000) return Join($"{BuildCardinal(number / 1_000_000_000_000_000)} {Quadrillion}", BuildCardinal(number % 1_000_000_000_000_000));
        return Join($"{BuildCardinal(number / 1_000_000_000_000_000_000)} {Quintillion}", BuildCardinal(number % 1_000_000_000_000_000_000));
    }

    /// <summary>Joins a non-empty cardinal head to an optional remainder.</summary>
    /// <param name="head">The non-empty leading text.</param>
    /// <param name="remainder">The optional trailing text.</param>
    /// <returns>The joined cardinal wording.</returns>
    private static string Join(string head, string remainder) =>
        string.IsNullOrEmpty(remainder) ? head : $"{head} {remainder}";
}

/// <summary>Produces Malay ordinals using the Malay <c>lapan</c> cardinal stem.</summary>
public sealed class MalayOrdinalLanguageSpecifics : IndonesianOrdinalLanguageSpecifics
{
    /// <inheritdoc />
    protected override string Eight => "lapan";

    /// <inheritdoc />
    protected override string Billion => "bilion";

    /// <inheritdoc />
    protected override string Trillion => "trilion";

    /// <inheritdoc />
    protected override string Quadrillion => "kuadrilion";

    /// <inheritdoc />
    protected override string Quintillion => "kuintilion";
}
