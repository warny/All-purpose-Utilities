using System.Globalization;
using System.Text.Json;

namespace Utils.Dates;

/// <summary>
/// Loads <see cref="DateFormulaLanguage"/> data from a JSON configuration.
/// </summary>
public sealed class JsonDateFormulaLanguageProvider : IDateFormulaLanguageProvider
{
    private readonly Dictionary<string, DateFormulaLanguage> _languages;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDateFormulaLanguageProvider"/> class.
    /// </summary>
    /// <param name="json">JSON configuration.</param>
    public JsonDateFormulaLanguageProvider(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var root = JsonSerializer.Deserialize<Dictionary<string, JsonLanguage>>(json, options) ?? new();
        _languages = root.ToDictionary(
                p => p.Key,
                p => new DateFormulaLanguage
                {
                    Start = p.Value.Start[0],
                    End = p.Value.End[0],
                    Day = p.Value.Units.Day[0],
                    Week = p.Value.Units.Week[0],
                    Month = p.Value.Units.Month[0],
                    Quarter = p.Value.Units.Quarter[0],
                    Year = p.Value.Units.Year[0],
                    WorkingDay = p.Value.Units.Workday[0],
                    Days = p.Value.Days.ToDictionary(d => d.Key, d => Enum.Parse<DayOfWeek>(d.Value))
                });
    }

    /// <inheritdoc />
    public DateFormulaLanguage GetLanguage(CultureInfo culture)
    {
        var key = culture.TwoLetterISOLanguageName;
        if (_languages.TryGetValue(key, out var lang))
            return lang;
        if (_languages.TryGetValue("en", out var en))
            return en;
        throw new NotSupportedException($"Culture {culture.Name} not supported");
    }

    private sealed class JsonLanguage
    {
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public JsonUnits Units { get; set; } = new();
        public Dictionary<string, string> Days { get; set; } = new();
    }

    private sealed class JsonUnits
    {
        public string Day { get; set; } = string.Empty;
        public string Week { get; set; } = string.Empty;
        public string Month { get; set; } = string.Empty;
        public string Quarter { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string Workday { get; set; } = string.Empty;
    }
}
