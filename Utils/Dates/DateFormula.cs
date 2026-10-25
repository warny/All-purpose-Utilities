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
        const string resourceName = "Utils.Objects.DateFormulaConfigurations.DateFormulaConfiguration.json";
        using Stream stream = typeof(DateFormula).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded DateFormula configuration resource '{resourceName}' is missing from omy.Utils.");
        using var reader = new StreamReader(stream);
        return new JsonDateFormulaLanguageProvider(reader.ReadToEnd());
    });

    /// <summary>The default language provider loaded from the bundled configuration file.</summary>
    internal static IDateFormulaLanguageProvider DefaultProvider => _defaultProvider.Value;

    /// <summary>Cache for compiled formulas.</summary>
    private static readonly Dictionary<FormulaCacheKey, Func<DateTime, DateTime>> _cache = [];

    /// <summary>
    /// Represents a unique key for the formula cache. The compiled delegate captures a snapshot of the culture's selected
    /// calendar and first day of the week, so two cultures with the same name but a different selection (a clone with
    /// another <see cref="DateTimeFormatInfo.Calendar"/>) must not share an entry.
    /// </summary>
    private sealed record FormulaCacheKey(IDateFormulaLanguageProvider Provider, string CultureName, CalendarIdentity Calendar, DayOfWeek FirstDayOfWeek, string Formula, ICalendarProvider? CalendarProvider);

    /// <summary>
    /// Identifies a calendar by what changes its date arithmetic: its type and, for the built-in calendars carrying such
    /// a state, the variant of that type.
    /// </summary>
    /// <param name="Type">The runtime type of the calendar.</param>
    /// <param name="Variant">
    /// <see cref="HijriCalendar.HijriAdjustment"/> for a <see cref="HijriCalendar"/>, <see cref="GregorianCalendar.CalendarType"/>
    /// for a <see cref="GregorianCalendar"/>, 0 for any other calendar.
    /// </param>
    internal readonly record struct CalendarIdentity(Type Type, int Variant)
    {
        /// <summary>Builds the identity of <paramref name="calendar"/>.</summary>
        /// <param name="calendar">The calendar.</param>
        /// <returns>Its type and variant.</returns>
        public static CalendarIdentity Of(Calendar calendar) => new(calendar.GetType(), calendar switch
        {
            HijriCalendar hijri => hijri.HijriAdjustment,
            GregorianCalendar gregorian => (int)gregorian.CalendarType,
            _ => 0,
        });
    }

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
        var format = culture.DateTimeFormat;
        var key = new FormulaCacheKey(provider, culture.Name, CalendarIdentity.Of(format.Calendar), format.FirstDayOfWeek, formula, calendarProvider);
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

    /// <summary>Builds the call adding <paramref name="value"/> periods to <paramref name="expr"/>.</summary>
    /// <param name="calendar">The calendar of the arithmetic, a read-only snapshot of the culture's selected calendar.</param>
    /// <param name="expr">The date expression.</param>
    /// <param name="period">The period unit.</param>
    /// <param name="value">The signed number of periods.</param>
    /// <param name="calendarProvider">Required for working days.</param>
    /// <returns>The call expression, or <paramref name="expr"/> when the period adds nothing.</returns>
    internal static Expression CreateCalendarCall(Calendar calendar, Expression expr, PeriodTypeEnum period, int value, ICalendarProvider? calendarProvider)
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

        var calendarExpression = Expression.Constant(calendar);
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
