using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Produces Romanian ordinals following DOOM (dexonline): the masculine is the article <c>al</c>,
/// the cardinal, and <c>-lea</c> on its last word (<c>al doilea</c>, <c>al douăzeci și unulea</c>,
/// <c>al o sutălea</c>, <c>al două sutelea</c>, <c>al o miilea</c>); the feminine is <c>a</c>, the
/// cardinal, and <c>-a</c> on its last word (<c>a doua</c>, <c>a douăzeci și una</c>,
/// <c>a o suta</c>, <c>a o mia</c>). One alone is suppletive (<c>primul</c>, <c>prima</c>).
/// </summary>
/// <remarks>
/// The cardinal prefix is rebuilt here because <see cref="IOrdinalLanguageSpecifics"/> receives no
/// converter. The implemented range is 1–999 999 plus exactly one million (<c>al un
/// milionulea</c>, masculine only); other values, round thousands whose multiplier takes the
/// <c>de</c> construction, and the feminine of one million are declined and fail closed.
/// </remarks>
public sealed class RomanianOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    private static readonly string[] s_unitsMasculine = ["", "unu", "doi", "trei", "patru", "cinci", "șase", "șapte", "opt", "nouă"];
    private static readonly string[] s_unitsFeminine = ["", "una", "două", "trei", "patru", "cinci", "șase", "șapte", "opt", "nouă"];
    private static readonly string[] s_teensMasculine =
        ["zece", "unsprezece", "doisprezece", "treisprezece", "paisprezece", "cincisprezece", "șaisprezece", "șaptesprezece", "optsprezece", "nouăsprezece"];
    private static readonly string[] s_tens = ["", "", "douăzeci", "treizeci", "patruzeci", "cincizeci", "șaizeci", "șaptezeci", "optzeci", "nouăzeci"];

    // Last-word ordinal forms: masculine "-lea" and feminine "-a".
    private static readonly Dictionary<string, (string Masculine, string Feminine)> s_lastWords = new()
    {
        ["unu"] = ("unulea", "una"),
        ["doi"] = ("doilea", "doua"),
        ["trei"] = ("treilea", "treia"),
        ["patru"] = ("patrulea", "patra"),
        ["cinci"] = ("cincilea", "cincea"),
        ["șase"] = ("șaselea", "șasea"),
        ["șapte"] = ("șaptelea", "șaptea"),
        ["opt"] = ("optulea", "opta"),
        ["nouă"] = ("nouălea", "noua"),
        ["zece"] = ("zecelea", "zecea"),
        ["unsprezece"] = ("unsprezecelea", "unsprezecea"),
        ["doisprezece"] = ("doisprezecelea", "douăsprezecea"),
        ["treisprezece"] = ("treisprezecelea", "treisprezecea"),
        ["paisprezece"] = ("paisprezecelea", "paisprezecea"),
        ["cincisprezece"] = ("cincisprezecelea", "cincisprezecea"),
        ["șaisprezece"] = ("șaisprezecelea", "șaisprezecea"),
        ["șaptesprezece"] = ("șaptesprezecelea", "șaptesprezecea"),
        ["optsprezece"] = ("optsprezecelea", "optsprezecea"),
        ["nouăsprezece"] = ("nouăsprezecelea", "nouăsprezecea"),
        ["douăzeci"] = ("douăzecilea", "douăzecea"),
        ["treizeci"] = ("treizecilea", "treizecea"),
        ["patruzeci"] = ("patruzecilea", "patruzecea"),
        ["cincizeci"] = ("cincizecilea", "cincizecea"),
        ["șaizeci"] = ("șaizecilea", "șaizecea"),
        ["șaptezeci"] = ("șaptezecilea", "șaptezecea"),
        ["optzeci"] = ("optzecilea", "optzecea"),
        ["nouăzeci"] = ("nouăzecilea", "nouăzecea"),
        ["sută"] = ("sutălea", "suta"),
        ["sute"] = ("sutelea", "suta"),
        ["mie"] = ("miilea", "mia"),
        ["mii"] = ("miilea", "mia"),
    };

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        result = null;
        bool feminine = activeVariants.TryGetValue("gen", out string? gender) && gender == "feminin";
        if (number == 1)
        {
            result = feminine ? "prima" : "primul";
            return true;
        }
        if (number == 1_000_000)
        {
            if (feminine)
                return false;
            result = "al un milionulea";
            return true;
        }
        if (number <= 1 || number >= 1_000_000)
            return false;

        string? cardinal = BuildCardinal(number);
        if (cardinal == null)
            return false;
        int split = cardinal.LastIndexOf(' ') + 1;
        string head = cardinal[..split];
        if (!s_lastWords.TryGetValue(cardinal[split..], out var forms))
            return false;
        result = (feminine ? "a " : "al ") + head + (feminine ? forms.Feminine : forms.Masculine);
        return true;
    }

    /// <summary>Builds the cardinal of a number between 2 and 999 999, or <see langword="null"/> when declined.</summary>
    /// <param name="number">A number between 2 and 999 999.</param>
    /// <returns>The masculine cardinal wording.</returns>
    private static string? BuildCardinal(int number)
    {
        int thousands = number / 1000;
        int units = number % 1000;
        var words = new List<string>();
        if (thousands > 0)
        {
            if (thousands == 1)
                words.Add("o mie");
            else
            {
                int lastTwo = thousands % 100;
                bool takesDe = lastTwo == 0 || lastTwo >= 20;
                // A round multiple of a thousand that takes "de" ("douăzeci de mii") has no verified
                // ordinal form, so it is declined.
                if (takesDe && units == 0)
                    return null;
                words.Add(Group(thousands, feminine: true) + (takesDe ? " de mii" : " mii"));
            }
        }
        if (units > 0)
            words.Add(Group(units, feminine: false));
        return string.Join(" ", words);
    }

    /// <summary>Builds the cardinal of a group between 1 and 999.</summary>
    /// <param name="group">A number between 1 and 999.</param>
    /// <param name="feminine">Whether the group counts a feminine noun (mii).</param>
    /// <returns>The cardinal wording.</returns>
    private static string Group(int group, bool feminine)
    {
        var words = new List<string>();
        int hundreds = group / 100;
        int remainder = group % 100;
        if (hundreds == 1)
            words.Add("o sută");
        else if (hundreds > 1)
            words.Add(s_unitsFeminine[hundreds] + " sute");
        string[] units = feminine ? s_unitsFeminine : s_unitsMasculine;
        if (remainder >= 20)
        {
            words.Add(s_tens[remainder / 10]);
            if (remainder % 10 > 0)
            {
                words.Add("și");
                words.Add(units[remainder % 10]);
            }
        }
        else if (remainder >= 10)
            words.Add(feminine && remainder == 12 ? "douăsprezece" : s_teensMasculine[remainder - 10]);
        else if (remainder > 0)
            words.Add(units[remainder]);
        return string.Join(" ", words);
    }
}
