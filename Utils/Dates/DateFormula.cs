using System;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;
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
        private static readonly Dictionary<FormulaCacheKey, Func<DateTime, DateTime>> _cache = new();

        /// <summary>Represents a unique key for the formula cache.</summary>
        private sealed record FormulaCacheKey(IDateFormulaLanguageProvider Provider, string CultureName, string Formula);

        /// <summary>
        /// Retrieves a compiled formula from the cache or compiles it if missing.
        /// </summary>
        /// <param name="formula">Formula to compile.</param>
        /// <param name="provider">Language provider.</param>
        /// <param name="culture">Culture used for token interpretation.</param>
        /// <returns>A delegate computing the date from a base value.</returns>
        private static Func<DateTime, DateTime> GetCompiledFormula(string formula, IDateFormulaLanguageProvider provider, CultureInfo culture)
        {
                var key = new FormulaCacheKey(provider, culture.Name, formula);
                lock (_cache)
                {
                        if (!_cache.TryGetValue(key, out var compiled))
                        {
                                compiled = Compile(formula, provider, culture);
                                _cache[key] = compiled;
                        }
                        return compiled;
                }
        }

        /// <summary>
        /// Calculates the date described by <paramref name="formula"/>.
        /// </summary>
        /// <param name="date">Base date.</param>
        /// <param name="formula">Formula to evaluate.</param>
        /// <param name="culture">Culture used to interpret tokens. If null, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
        /// <returns>The computed date.</returns>
        public static DateTime Calculate(this DateTime date, string formula, CultureInfo culture = null)
                => date.Calculate(formula, _defaultProvider.Value, culture ?? CultureInfo.CurrentCulture);

        /// <summary>
        /// Calculates the date described by <paramref name="formula"/> using a custom provider.
        /// </summary>
        /// <param name="date">Base date.</param>
        /// <param name="formula">Formula to evaluate.</param>
        /// <param name="provider">Language provider.</param>
        /// <param name="culture">Culture to interpret tokens. If null, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
        /// <returns>The computed date.</returns>
        public static DateTime Calculate(this DateTime date, string formula, IDateFormulaLanguageProvider provider, CultureInfo culture = null)
        {
                culture ??= CultureInfo.CurrentCulture;
                var compiled = GetCompiledFormula(formula, provider, culture);
                return compiled(date);
        }

        /// <summary>
        /// Compiles the provided <paramref name="formula"/> into a delegate.
        /// </summary>
        /// <param name="formula">Formula to compile.</param>
        /// <param name="culture">Culture used to interpret tokens. If null, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
        /// <returns>A delegate computing the resulting date.</returns>
        public static Func<DateTime, DateTime> Compile(string formula, CultureInfo culture = null)
                => Compile(formula, _defaultProvider.Value, culture ?? CultureInfo.CurrentCulture);

        /// <summary>
        /// Compiles the provided <paramref name="formula"/> using a custom provider.
        /// </summary>
        /// <param name="formula">Formula to compile.</param>
        /// <param name="provider">Language provider.</param>
        /// <param name="culture">Culture used to interpret tokens. If null, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
        /// <returns>A delegate computing the resulting date.</returns>
        public static Func<DateTime, DateTime> Compile(string formula, IDateFormulaLanguageProvider provider, CultureInfo culture = null)
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

                var param = Expression.Parameter(typeof(DateTime), "d");
                Expression expr = Expression.Call(
                        typeof(DateUtils),
                        start ? nameof(DateUtils.StartOf) : nameof(DateUtils.EndOf),
                        null,
                        param,
                        Expression.Constant(period),
                        Expression.Constant(culture));

                var index = 2;
                while (index < formula.Length && (formula[index] == '+' || formula[index] == '-'))
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
                                expr = Expression.Call(
                                        typeof(DateFormula).GetMethod("AddPeriod", BindingFlags.NonPublic | BindingFlags.Static)!,
                                        expr,
                                        Expression.Constant(unit),
                                        Expression.Constant(value),
                                        Expression.Constant(culture.Calendar));
                                index = pos + 1;
                        }
                        else
                        {
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
                        var day = formula.Substring(index, 2);
                        expr = Expression.Call(
                                typeof(DateFormula).GetMethod("MoveToSameWeekDay", BindingFlags.NonPublic | BindingFlags.Static)!,
                                expr,
                                Expression.Constant(lang.Days[day]),
                                Expression.Constant(culture.DateTimeFormat.FirstDayOfWeek));
                }
                expr = Expression.Property(expr, nameof(DateTime.Date));
                return Expression.Lambda<Func<DateTime, DateTime>>(expr, param).Compile();
        }

        private static PeriodTypeEnum ParsePeriod(char token, DateFormulaLanguage lang)
                => token switch
                {
                        var t when t == lang.Day => PeriodTypeEnum.Day,
                        var t when t == lang.Week => PeriodTypeEnum.Week,
                        var t when t == lang.Month => PeriodTypeEnum.Month,
                        var t when t == lang.Quarter => PeriodTypeEnum.Quarter,
                        var t when t == lang.Year => PeriodTypeEnum.Year,
                        _ => throw new ArgumentException($"Unknown period token '{token}'.")
                };

        /// <summary>
        /// Adds a period to a date using the provided calendar.
        /// </summary>
        /// <param name="date">Date to adjust.</param>
        /// <param name="period">Type of period to add.</param>
        /// <param name="value">Number of units to add.</param>
        /// <param name="calendar">Calendar used for computations.</param>
        /// <returns>The adjusted date.</returns>
        private static DateTime AddPeriod(DateTime date, PeriodTypeEnum period, int value, Calendar calendar)
        {
                return period switch
                {
                        PeriodTypeEnum.Day => calendar.AddDays(date, value),
                        PeriodTypeEnum.Week => calendar.AddDays(date, value * 7),
                        PeriodTypeEnum.Month => calendar.AddMonths(date, value),
                        PeriodTypeEnum.Quarter => calendar.AddMonths(date, value * 3),
                        PeriodTypeEnum.Year => calendar.AddYears(date, value),
                        _ => date
                };
        }

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
