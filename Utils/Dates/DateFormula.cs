using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Objects;

namespace Utils.Dates;
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields

/// <summary>
/// Evaluates date formulas using culture specific tokens.
/// </summary>
public static class DateFormula
{
    private static readonly Lazy<IDateFormulaLanguageProvider> _defaultProvider = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory,
            "Objects",
            "DateFormulaConfigurations",
            "DateFormulaConfiguration.json");
        var json = File.ReadAllText(path);
        return new JsonDateFormulaLanguageProvider(json);
    });

    /// <summary>The default language provider loaded from the bundled configuration file.</summary>
    internal static IDateFormulaLanguageProvider DefaultProvider => _defaultProvider.Value;

    /// <summary>Cache for compiled formulas.</summary>
    private static readonly Dictionary<FormulaCacheKey, Func<DateTime, DateTime>> _cache = [];

    /// <summary>Represents a unique key for the formula cache.</summary>
    private sealed record FormulaCacheKey(IDateFormulaLanguageProvider Provider, string CultureName, string Formula, ICalendarProvider? CalendarProvider);

    /// <summary>
    /// Retrieves a compiled formula from the cache or compiles it if missing.
    /// </summary>
    /// <param name="formula">Formula to compile.</param>
    /// <param name="provider">Language provider.</param>
    /// <param name="culture">Culture used for token interpretation.</param>
    /// <param name="calendarProvider">Calendar that defines the base for the formula to compute dates</param>
    /// <returns>A delegate computing the date from a base value.</returns>
    private static Func<DateTime, DateTime> GetCompiledFormula(string formula, IDateFormulaLanguageProvider provider, CultureInfo culture, ICalendarProvider? calendarProvider)
    {
        var key = new FormulaCacheKey(provider, culture.Name, formula, calendarProvider);
        lock (_cache)
        {
            return _cache.GetOrAdd(key, () => Compile(formula, provider, culture, calendarProvider));
        }
    }

    /// <summary>
    /// Calculates the date described by <paramref name="formula"/>.
    /// </summary>
    /// <param name="date">Base date.</param>
    /// <param name="formula">Formula to evaluate.</param>
    /// <param name="culture">Culture used to interpret tokens. If null, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
    /// <param name="calendarProvider">Optional calendar provider defining the base for date computations.</param>
    /// <returns>The computed date.</returns>
    public static DateTime Calculate(this DateTime date, string formula, CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
                    => date.Calculate(formula, _defaultProvider.Value, culture ?? CultureInfo.CurrentCulture, calendarProvider);

    /// <summary>
    /// Calculates the date described by <paramref name="formula"/> using a custom provider.
    /// </summary>
    /// <param name="date">Base date.</param>
    /// <param name="formula">Formula to evaluate.</param>
    /// <param name="provider">Language provider.</param>
    /// <param name="culture">Culture to interpret tokens. If null, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
    /// <param name="calendarProvider">Optional calendar provider defining the base for date computations.</param>
    /// <returns>The computed date.</returns>
    public static DateTime Calculate(this DateTime date, string formula, IDateFormulaLanguageProvider provider, CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var compiled = GetCompiledFormula(formula, provider, culture, calendarProvider);
        return compiled(date);
    }

    /// <summary>
    /// Builds an expression tree for <paramref name="formula"/>.
    /// </summary>
    public static Expression<Func<DateTime, DateTime>> ToExpression(string formula, CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
        => DateFormulaExpression.Parse(formula, _defaultProvider.Value, culture ?? CultureInfo.CurrentCulture).ToExpression(culture ?? CultureInfo.CurrentCulture, calendarProvider);

    /// <summary>
    /// Builds an expression tree for <paramref name="formula"/> using a custom provider.
    /// </summary>
    public static Expression<Func<DateTime, DateTime>> ToExpression(string formula, IDateFormulaLanguageProvider provider, CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
        => DateFormulaExpression.Parse(formula, provider, culture ?? CultureInfo.CurrentCulture).ToExpression(culture ?? CultureInfo.CurrentCulture, calendarProvider);

    /// <summary>
    /// Compiles the provided <paramref name="formula"/> into a delegate.
    /// </summary>
    /// <param name="formula">Formula to compile.</param>
    /// <param name="culture">Culture used to interpret tokens. If null, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
    /// <param name="calendarProvider">Optional calendar provider defining the base for date computations.</param>
    /// <returns>A delegate computing the resulting date.</returns>
    public static Func<DateTime, DateTime> Compile(string formula, CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
                    => Compile(formula, _defaultProvider.Value, culture ?? CultureInfo.CurrentCulture, calendarProvider);

    /// <summary>
    /// Parses <paramref name="formula"/> into a culture-neutral <see cref="DateFormulaExpression"/>.
    /// </summary>
    public static DateFormulaExpression Parse(string formula, CultureInfo culture = null)
        => DateFormulaExpression.Parse(formula, _defaultProvider.Value, culture ?? CultureInfo.CurrentCulture);

    /// <summary>
    /// Parses <paramref name="formula"/> into a culture-neutral <see cref="DateFormulaExpression"/>
    /// using a custom <paramref name="provider"/>.
    /// </summary>
    public static DateFormulaExpression Parse(string formula, IDateFormulaLanguageProvider provider, CultureInfo culture = null)
        => DateFormulaExpression.Parse(formula, provider, culture ?? CultureInfo.CurrentCulture);

    /// <summary>
    /// Compiles the provided <paramref name="formula"/> using a custom provider.
    /// </summary>
    /// <param name="formula">Formula to compile.</param>
    /// <param name="provider">Language provider.</param>
    /// <param name="culture">Culture used to interpret tokens. If null, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
    /// <param name="calendarProvider">Optional calendar provider defining the base for date computations.</param>
    /// <returns>A delegate computing the resulting date.</returns>
    public static Func<DateTime, DateTime> Compile(string formula, IDateFormulaLanguageProvider provider, CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return DateFormulaExpression.Parse(formula, provider, culture).Compile(culture, calendarProvider);
    }

    internal static Expression CreateCalendarCall(CultureInfo culture, Expression expr, PeriodTypeEnum period, int value, ICalendarProvider? calendarProvider)
    {
        if (period == PeriodTypeEnum.WorkingDay)
        {
            if (calendarProvider is null)
                throw new InvalidOperationException("Working day operations require a calendar provider.");
            return Expression.Call(
                    typeof(DateUtils).GetMethod(nameof(DateUtils.AddWorkingDays), BindingFlags.Public | BindingFlags.Static)!,
                    expr,
                    Expression.Constant(value),
                    Expression.Constant(calendarProvider));
        }

        (string methodName, value) = period switch
        {
            PeriodTypeEnum.Day => ("AddDays", value),
            PeriodTypeEnum.Week => ("AddDays", value * 7),
            PeriodTypeEnum.Month => ("AddMonths", value),
            PeriodTypeEnum.Quarter => ("AddMonths", value * 3),
            PeriodTypeEnum.Year => ("AddYears", value),
            _ => (null, value)

        };
        if (methodName is null) return expr;

        var calendarExpression = Expression.Constant(culture.Calendar);
        return Expression.Call(
                calendarExpression,
                typeof(Calendar).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!,
                expr,
                Expression.Constant(value));
    }

    internal static PeriodTypeEnum ParsePeriod(char token, DateFormulaLanguage lang)
        => token switch
        {
            var t when t == lang.Day => PeriodTypeEnum.Day,
            var t when t == lang.Week => PeriodTypeEnum.Week,
            var t when t == lang.Month => PeriodTypeEnum.Month,
            var t when t == lang.Quarter => PeriodTypeEnum.Quarter,
            var t when t == lang.Year => PeriodTypeEnum.Year,
            var t when t == lang.WorkingDay => PeriodTypeEnum.WorkingDay,
            _ => throw new ArgumentException($"Unknown period token '{token}'.")
        };

    private static DateTime AdjustToDayOfWeek(DateTime date, DayOfWeek day, bool after)
    {
        if (after)
        {
            var delta = ((int)day - (int)date.DayOfWeek + 7) % 7;
            return date.AddDays(delta == 0 ? 7 : delta);
        }

        var previousDelta = ((int)date.DayOfWeek - (int)day + 7) % 7;
        return date.AddDays(previousDelta == 0 ? -7 : -previousDelta);
    }

    private static DateTime MoveToSameWeekDay(DateTime date, DayOfWeek day, DayOfWeek firstDayOfWeek)
    {
        var weekStart = date.StartOf(PeriodTypeEnum.Week, firstDayOfWeek);
        var delta = ((int)day - (int)firstDayOfWeek + 7) % 7;
        return weekStart.AddDays(delta);
    }
}
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
