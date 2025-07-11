using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Objects;

namespace Utils.Dates;

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
	/// <returns>The computed date.</returns>
        public static DateTime Calculate(this DateTime date, string formula, IDateFormulaLanguageProvider provider, CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
        {
                culture ??= CultureInfo.CurrentCulture;
                var compiled = GetCompiledFormula(formula, provider, culture, calendarProvider);
                return compiled(date);
        }

	/// <summary>
	/// Compiles the provided <paramref name="formula"/> into a delegate.
	/// </summary>
	/// <param name="formula">Formula to compile.</param>
	/// <param name="culture">Culture used to interpret tokens. If null, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
	/// <returns>A delegate computing the resulting date.</returns>
        public static Func<DateTime, DateTime> Compile(string formula, CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
                        => Compile(formula, _defaultProvider.Value, culture ?? CultureInfo.CurrentCulture, calendarProvider);

	/// <summary>
	/// Compiles the provided <paramref name="formula"/> using a custom provider.
	/// </summary>
	/// <param name="formula">Formula to compile.</param>
	/// <param name="provider">Language provider.</param>
	/// <param name="culture">Culture used to interpret tokens. If null, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
	/// <returns>A delegate computing the resulting date.</returns>
        public static Func<DateTime, DateTime> Compile(string formula, IDateFormulaLanguageProvider provider, CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
        {
                culture ??= CultureInfo.CurrentCulture;
                var lang = provider.GetLanguage(culture);

		if (formula.Length < 2)
			throw new ArgumentException("Formula is too short.", nameof(formula));

		var start = formula[0] == lang.Start;
		var end = formula[0] == lang.End;
		if (!start && !end)
			throw new ArgumentException("Invalid formula start token.", nameof(formula));

                var period = ParsePeriod(formula[1], lang);

                if (start && (period == PeriodTypeEnum.Day || period == PeriodTypeEnum.WorkingDay))
                        throw new ArgumentException("Start of day and start of working day formulas are not supported.", nameof(formula));

                if (end && (period == PeriodTypeEnum.Day || period == PeriodTypeEnum.WorkingDay))
                        throw new ArgumentException("End of day and end of working day formulas are not supported.", nameof(formula));

		var param = Expression.Parameter(typeof(DateTime), "d");

		Expression expr = Expression.Call(
				typeof(DateUtils).GetMethod(start ? nameof(DateUtils.StartOf) : nameof(DateUtils.EndOf), BindingFlags.Public | BindingFlags.Static, [typeof(DateTime), typeof(PeriodTypeEnum), typeof(CultureInfo)]),
				param,
				Expression.Constant(period),
				Expression.Constant(culture));

		var index = 2;
                while (index < formula.Length && (formula[index].In('+', '-')))
                {
                        var sign = formula[index];
                        var pos = index + 1;
                        if (pos < formula.Length && char.IsDigit(formula[pos]))
			{
				var startPos = pos;
				while (pos < formula.Length && char.IsDigit(formula[pos])) pos++;
				var value = int.Parse(formula[startPos..pos], CultureInfo.InvariantCulture);
				if (sign == '-') value = -value;
				if (pos >= formula.Length)
					throw new ArgumentException("Missing unit token.", nameof(formula));
                                var unit = ParsePeriod(formula[pos], lang);
                                expr = CreateCalendarCall(culture, expr, unit, value, calendarProvider);
				index = pos + 1;
			}
                        else if (pos < formula.Length && formula[pos] == lang.WorkingDay && pos + 1 == formula.Length)
                        {
                                if (calendarProvider is null)
                                        throw new InvalidOperationException("Working day operations require a calendar provider.");
                                var method = sign == '+' ? nameof(DateUtils.NextWorkingDay) : nameof(DateUtils.PreviousWorkingDay);
                                expr = Expression.Call(
                                                typeof(DateUtils).GetMethod(method, BindingFlags.Public | BindingFlags.Static)!,
                                                expr,
                                                Expression.Constant(calendarProvider));
                                index = pos + 1;
                                return Expression.Lambda<Func<DateTime, DateTime>>(Expression.Property(expr, nameof(DateTime.Date)), param).Compile();
                        }
                        else
                        {
                                if (pos + 2 > formula.Length)
                                        throw new ArgumentException("Incomplete day token.", nameof(formula));
                                var day = formula.Substring(pos, 2);
                                expr = Expression.Call(
                                                typeof(DateFormula).GetMethod("AdjustToDayOfWeek", BindingFlags.NonPublic | BindingFlags.Static)!,
                                                expr,
                                                Expression.Constant(lang.Days[day]),
                                                Expression.Constant(sign == '+'));
                                index = pos + 2;
                                return Expression.Lambda<Func<DateTime, DateTime>>(Expression.Property(expr, nameof(DateTime.Date)), param).Compile();
                        }
                }
                if (index < formula.Length)
                {
                        var token = formula.Substring(index, 2);
                        if (token.Length == 2 && token[1] == lang.WorkingDay && token[0].In('+', '-'))
                        {
                                if (calendarProvider is null)
                                        throw new InvalidOperationException("Working day operations require a calendar provider.");
                                var method = token[0] == '+' ? nameof(DateUtils.NextWorkingDay) : nameof(DateUtils.PreviousWorkingDay);
                                expr = Expression.Call(
                                                typeof(DateUtils).GetMethod(method, BindingFlags.Public | BindingFlags.Static)!,
                                                expr,
                                                Expression.Constant(calendarProvider));
                        }
                        else
                        {
                                expr = Expression.Call(
                                                typeof(DateFormula).GetMethod("MoveToSameWeekDay", BindingFlags.NonPublic | BindingFlags.Static)!,
                                                expr,
                                                Expression.Constant(lang.Days[token]),
                                                Expression.Constant(culture.DateTimeFormat.FirstDayOfWeek));
                        }
                }
                expr = Expression.Property(expr, nameof(DateTime.Date));
                return Expression.Lambda<Func<DateTime, DateTime>>(expr, param).Compile();
        }

        private static Expression CreateCalendarCall(CultureInfo culture, Expression expr, PeriodTypeEnum period, int value, ICalendarProvider? calendarProvider)
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

	private static PeriodTypeEnum ParsePeriod(char token, DateFormulaLanguage lang)
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
		var delta = ((int)day - (int)date.DayOfWeek + 7) % 7;
		if (after)
			return date.AddDays(delta == 0 ? 7 : delta);
		delta = ((int)date.DayOfWeek - (int)day + 7) % 7;
		return date.AddDays(delta == 0 ? -7 : -delta);
	}

	private static DateTime MoveToSameWeekDay(DateTime date, DayOfWeek day, DayOfWeek firstDayOfWeek)
	{
		var weekStart = date.StartOf(PeriodTypeEnum.Week, firstDayOfWeek);
		var delta = ((int)day - (int)firstDayOfWeek + 7) % 7;
		return weekStart.AddDays(delta);
	}
}
