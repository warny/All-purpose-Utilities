using System;
using System.Globalization;
using System.IO;
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
                var lang = provider.GetLanguage(culture);

                if (formula.Length < 2)
                        throw new ArgumentException("Formula is too short.", nameof(formula));

                var start = formula[0] == lang.Start;
                var end = formula[0] == lang.End;
                if (!start && !end)
                        throw new ArgumentException("Invalid formula start token.", nameof(formula));

                var period = ParsePeriod(formula[1], lang);
                DateTime result = start ? date.StartOf(period, culture) : date.EndOf(period, culture);

                var index = 2;
                // numeric adjustments
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
                                result = AddPeriod(result, unit, value, culture.Calendar);
                                index = pos + 1;
                        }
                        else
                        {
                                var day = formula.Substring(pos, 2);
                                result = AdjustToDayOfWeek(result, lang.Days[day], sign == '+');
                                index = pos + 2;
                                return result.Date;
                        }
                }
                if (index < formula.Length)
                {
                        var day = formula.Substring(index, 2);
                        result = MoveToSameWeekDay(result, lang.Days[day], culture.DateTimeFormat.FirstDayOfWeek);
                }
                return result.Date;
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
