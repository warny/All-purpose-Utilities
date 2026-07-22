using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Utils.Dates;

#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields

/// <summary>Describes the kind of operation represented by a single <see cref="DateFormulaStep"/>.</summary>
public enum DateFormulaStepKind
{
    /// <summary>Adds or subtracts a fixed count of a calendar period.</summary>
    AddPeriod,
    /// <summary>Moves forward or backward to the nearest occurrence of a specific weekday.</summary>
    AdjustToWeekDay,
    /// <summary>Moves to the occurrence of a specific weekday within the same calendar week.</summary>
    MoveToSameWeekDay,
    /// <summary>Snaps to the next or previous working day.</summary>
    AdjustWorkingDay,
}

/// <summary>Represents a single operation step within a <see cref="DateFormulaExpression"/>.</summary>
public readonly struct DateFormulaStep : IEquatable<DateFormulaStep>
{
    /// <summary>The kind of operation this step performs.</summary>
    public DateFormulaStepKind Kind { get; }

    /// <summary>
    /// Signed count for <see cref="DateFormulaStepKind.AddPeriod"/> (e.g., +3 or -2).
    /// For <see cref="DateFormulaStepKind.AdjustToWeekDay"/> and <see cref="DateFormulaStepKind.AdjustWorkingDay"/>:
    /// +1 means forward, -1 means backward.
    /// Unused for <see cref="DateFormulaStepKind.MoveToSameWeekDay"/>.
    /// </summary>
    public int SignedValue { get; }

    /// <summary>Calendar period unit, used only for <see cref="DateFormulaStepKind.AddPeriod"/>.</summary>
    public PeriodTypeEnum Unit { get; }

    /// <summary>
    /// Target weekday, used for <see cref="DateFormulaStepKind.AdjustToWeekDay"/>
    /// and <see cref="DateFormulaStepKind.MoveToSameWeekDay"/>.
    /// </summary>
    public DayOfWeek WeekDay { get; }

    private DateFormulaStep(DateFormulaStepKind kind, int signedValue, PeriodTypeEnum unit, DayOfWeek weekDay)
    {
        Kind = kind;
        SignedValue = signedValue;
        Unit = unit;
        WeekDay = weekDay;
    }

    internal static DateFormulaStep ForAddPeriod(int signedValue, PeriodTypeEnum unit)
        => new(DateFormulaStepKind.AddPeriod, signedValue, unit, default);

    internal static DateFormulaStep ForAdjustToWeekDay(int sign, DayOfWeek day)
        => new(DateFormulaStepKind.AdjustToWeekDay, sign, default, day);

    internal static DateFormulaStep ForMoveToSameWeekDay(DayOfWeek day)
        => new(DateFormulaStepKind.MoveToSameWeekDay, 0, default, day);

    internal static DateFormulaStep ForAdjustWorkingDay(int sign)
        => new(DateFormulaStepKind.AdjustWorkingDay, sign, default, default);

    /// <inheritdoc/>
    public bool Equals(DateFormulaStep other)
        => Kind == other.Kind
        && SignedValue == other.SignedValue
        && Unit == other.Unit
        && WeekDay == other.WeekDay;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DateFormulaStep other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, SignedValue, Unit, WeekDay);

    /// <summary>Returns <see langword="true"/> when both steps represent the same operation.</summary>
    public static bool operator ==(DateFormulaStep left, DateFormulaStep right) => left.Equals(right);

    /// <summary>Returns <see langword="true"/> when the steps represent different operations.</summary>
    public static bool operator !=(DateFormulaStep left, DateFormulaStep right) => !left.Equals(right);
}

/// <summary>
/// Culture-neutral intermediate representation of a date formula.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="DateFormulaExpression"/> is produced from a culture-specific formula string
/// (e.g., French <c>"FS+3O"</c> or English <c>"EW+3O"</c>) by <see cref="Parse(string, CultureInfo)"/>.
/// All tokens are converted to culture-neutral enum values at parse time, so the same expression
/// can be serialised into any supported culture's formula syntax.
/// </para>
/// <para>
/// Call <see cref="ToString(CultureInfo)"/> with <see cref="CultureInfo.InvariantCulture"/> to obtain a
/// stable English-token representation suitable for database persistence (the invariant culture
/// falls back to the English token set).
/// Call <see cref="Compile(CultureInfo, ICalendarProvider)"/> to produce an executable delegate.
/// </para>
/// </remarks>
public sealed class DateFormulaExpression : IFormattable, IEquatable<DateFormulaExpression>
{
    private static readonly MethodInfo _adjustToDayOfWeekMethod =
        typeof(DateFormula).GetMethod("AdjustToDayOfWeek", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _moveToSameWeekDayMethod =
        typeof(DateFormula).GetMethod("MoveToSameWeekDay", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// <see langword="true"/> if the formula is anchored to the start of <see cref="BasePeriod"/>;
    /// <see langword="false"/> if anchored to the end.
    /// </summary>
    public bool IsStart { get; }

    /// <summary>The base calendar period that the input date is snapped to before steps are applied.</summary>
    public PeriodTypeEnum BasePeriod { get; }

    /// <summary>The ordered list of transformation steps applied after the base period is resolved.</summary>
    public IReadOnlyList<DateFormulaStep> Steps { get; }

    private DateFormulaExpression(bool isStart, PeriodTypeEnum basePeriod, IReadOnlyList<DateFormulaStep> steps)
    {
        IsStart = isStart;
        BasePeriod = basePeriod;
        Steps = Array.AsReadOnly(steps.ToArray());
    }

    /// <summary>
    /// Parses <paramref name="formula"/> using the default language provider and <paramref name="culture"/>.
    /// </summary>
    public static DateFormulaExpression Parse(string formula, CultureInfo culture = null)
        => Parse(formula, DateFormula.DefaultProvider, culture ?? CultureInfo.CurrentCulture);

    /// <summary>
    /// Parses <paramref name="formula"/> using a custom <paramref name="provider"/> and <paramref name="culture"/>.
    /// </summary>
    public static DateFormulaExpression Parse(string formula, IDateFormulaLanguageProvider provider, CultureInfo culture = null)
    {
        ArgumentNullException.ThrowIfNull(formula);
        ArgumentNullException.ThrowIfNull(provider);
        culture ??= CultureInfo.CurrentCulture;

        if (formula.Length < 2)
            throw new ArgumentException("Formula is too short.", nameof(formula));

        var lang = provider.GetLanguage(culture);

        bool isStart = formula[0] == lang.Start;
        if (!isStart && formula[0] != lang.End)
            throw new ArgumentException("Invalid formula start token.", nameof(formula));

        var basePeriod = DateFormula.ParsePeriod(formula[1], lang);

        if (basePeriod is PeriodTypeEnum.Day or PeriodTypeEnum.WorkingDay)
            throw new ArgumentException("Start/end of day and working day are not supported as a base period.", nameof(formula));

        var steps = new List<DateFormulaStep>();
        var index = 2;

        while (index < formula.Length && (formula[index] == '+' || formula[index] == '-'))
        {
            var sign = formula[index];
            var pos = index + 1;

            if (pos < formula.Length && char.IsDigit(formula[pos]))
            {
                // +N unit  (e.g., +3O, -1M)
                var startPos = pos;
                while (pos < formula.Length && char.IsDigit(formula[pos])) pos++;
                var value = int.Parse(formula[startPos..pos], CultureInfo.InvariantCulture);
                if (sign == '-') value = -value;
                if (pos >= formula.Length)
                    throw new ArgumentException("Missing unit token.", nameof(formula));
                var unit = DateFormula.ParsePeriod(formula[pos], lang);
                steps.Add(DateFormulaStep.ForAddPeriod(value, unit));
                index = pos + 1;
            }
            else if (pos < formula.Length && formula[pos] == lang.WorkingDay && pos + 1 == formula.Length)
            {
                // +O or -O as last character (single-char working-day snap)
                steps.Add(DateFormulaStep.ForAdjustWorkingDay(sign == '+' ? 1 : -1));
                index = formula.Length;
                break;
            }
            else
            {
                // +Lu or -Mo (2-char weekday adjust) — always terminal
                if (pos + 2 > formula.Length)
                    throw new ArgumentException("Incomplete day-of-week token.", nameof(formula));
                var dayStr = formula.Substring(pos, 2);
                if (!lang.Days.TryGetValue(dayStr, out var dayOfWeek))
                    throw new ArgumentException($"Unknown day token '{dayStr}'.", nameof(formula));
                steps.Add(DateFormulaStep.ForAdjustToWeekDay(sign == '+' ? 1 : -1, dayOfWeek));
                index = pos + 2;
                if (index != formula.Length)
                    throw new ArgumentException(
                        $"No token is allowed after a day-of-week adjustment (position {index}).",
                        nameof(formula));
                break;
            }
        }

        if (index < formula.Length)
        {
            var remaining = formula.Length - index;
            if (remaining < 2)
                throw new ArgumentException("Trailing token is too short.", nameof(formula));
            if (remaining > 2)
                throw new ArgumentException(
                    $"Unexpected trailing content at position {index + 2}.",
                    nameof(formula));
            var token = formula.Substring(index, 2);

            if ((token[0] == '+' || token[0] == '-') && token[1] == lang.WorkingDay)
            {
                // +O / -O as final 2-char token after AddPeriod steps
                steps.Add(DateFormulaStep.ForAdjustWorkingDay(token[0] == '+' ? 1 : -1));
            }
            else
            {
                // "Lu" / "Mo" — move to same-week occurrence of that weekday
                if (!lang.Days.TryGetValue(token, out var dayOfWeek))
                    throw new ArgumentException($"Unknown day token '{token}'.", nameof(formula));
                steps.Add(DateFormulaStep.ForMoveToSameWeekDay(dayOfWeek));
            }
        }

        return new DateFormulaExpression(isStart, basePeriod, steps);
    }

    /// <summary>
    /// Builds a LINQ expression tree that evaluates this formula for a given <see cref="DateTime"/> input.
    /// </summary>
    /// <param name="culture">Culture used for calendar arithmetic and week-start day resolution.</param>
    /// <param name="calendarProvider">Required when any step involves working days.</param>
    /// <returns>A strongly-typed lambda expression ready for further composition or compilation.</returns>
    public Expression<Func<DateTime, DateTime>> ToExpression(CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        var param = Expression.Parameter(typeof(DateTime), "d");

        Expression expr = Expression.Call(
            typeof(DateUtils).GetMethod(
                IsStart ? nameof(DateUtils.StartOf) : nameof(DateUtils.EndOf),
                BindingFlags.Public | BindingFlags.Static,
                [typeof(DateTime), typeof(PeriodTypeEnum), typeof(CultureInfo)])!,
            param,
            Expression.Constant(BasePeriod),
            Expression.Constant(culture));

        foreach (var step in Steps)
        {
            switch (step.Kind)
            {
                case DateFormulaStepKind.AddPeriod:
                    expr = DateFormula.CreateCalendarCall(culture, expr, step.Unit, step.SignedValue, calendarProvider);
                    break;

                case DateFormulaStepKind.AdjustToWeekDay:
                    expr = Expression.Call(
                        _adjustToDayOfWeekMethod,
                        expr,
                        Expression.Constant(step.WeekDay),
                        Expression.Constant(step.SignedValue > 0));
                    return Expression.Lambda<Func<DateTime, DateTime>>(
                        Expression.Property(expr, nameof(DateTime.Date)), param);

                case DateFormulaStepKind.MoveToSameWeekDay:
                    expr = Expression.Call(
                        _moveToSameWeekDayMethod,
                        expr,
                        Expression.Constant(step.WeekDay),
                        Expression.Constant(culture.DateTimeFormat.FirstDayOfWeek));
                    break;

                case DateFormulaStepKind.AdjustWorkingDay:
                    if (calendarProvider is null)
                        throw new InvalidOperationException("Working day operations require a calendar provider.");
                    var workingDayMethod = step.SignedValue > 0
                        ? nameof(DateUtils.NextWorkingDay)
                        : nameof(DateUtils.PreviousWorkingDay);
                    expr = Expression.Call(
                        typeof(DateUtils).GetMethod(workingDayMethod, BindingFlags.Public | BindingFlags.Static)!,
                        expr,
                        Expression.Constant(calendarProvider));
                    return Expression.Lambda<Func<DateTime, DateTime>>(
                        Expression.Property(expr, nameof(DateTime.Date)), param);
            }
        }

        expr = Expression.Property(expr, nameof(DateTime.Date));
        return Expression.Lambda<Func<DateTime, DateTime>>(expr, param);
    }

    /// <summary>
    /// Compiles this expression into an executable delegate.
    /// </summary>
    /// <param name="culture">Culture used for calendar arithmetic and week-start day resolution.</param>
    /// <param name="calendarProvider">Required when any step involves working days.</param>
    public Func<DateTime, DateTime> Compile(CultureInfo culture = null, ICalendarProvider? calendarProvider = null)
        => ToExpression(culture, calendarProvider).Compile();

    /// <summary>
    /// Renders this expression as a formula string in the language for <paramref name="culture"/>,
    /// using the default language provider.
    /// Pass <see cref="CultureInfo.InvariantCulture"/> to obtain the English-token form
    /// suitable for database persistence.
    /// </summary>
    public string ToString(CultureInfo culture)
        => ToString(DateFormula.DefaultProvider, culture ?? CultureInfo.InvariantCulture);

    /// <summary>
    /// Renders this expression as a formula string in the language for <paramref name="culture"/>,
    /// using an explicit <paramref name="provider"/>.
    /// </summary>
    public string ToString(IDateFormulaLanguageProvider provider, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var lang = provider.GetLanguage(culture ?? CultureInfo.InvariantCulture);
        return RenderToString(lang);
    }

    /// <summary>Returns the English-token form of this expression.</summary>
    public override string ToString() => ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc cref="IFormattable.ToString(string?, IFormatProvider?)"/>
    public string ToString(string? format, IFormatProvider? formatProvider)
        => ToString(formatProvider as CultureInfo ?? CultureInfo.InvariantCulture);

    private string RenderToString(DateFormulaLanguage lang)
    {
        var sb = new StringBuilder();
        sb.Append(IsStart ? lang.Start : lang.End);
        sb.Append(PeriodToChar(BasePeriod, lang));

        foreach (var step in Steps)
        {
            switch (step.Kind)
            {
                case DateFormulaStepKind.AddPeriod:
                    sb.Append(step.SignedValue >= 0 ? '+' : '-');
                    sb.Append(Math.Abs(step.SignedValue));
                    sb.Append(PeriodToChar(step.Unit, lang));
                    break;

                case DateFormulaStepKind.AdjustToWeekDay:
                    sb.Append(step.SignedValue > 0 ? '+' : '-');
                    sb.Append(DayToString(step.WeekDay, lang));
                    break;

                case DateFormulaStepKind.MoveToSameWeekDay:
                    sb.Append(DayToString(step.WeekDay, lang));
                    break;

                case DateFormulaStepKind.AdjustWorkingDay:
                    sb.Append(step.SignedValue > 0 ? '+' : '-');
                    sb.Append(lang.WorkingDay);
                    break;
            }
        }

        return sb.ToString();
    }

    private static char PeriodToChar(PeriodTypeEnum period, DateFormulaLanguage lang) => period switch
    {
        PeriodTypeEnum.Day => lang.Day,
        PeriodTypeEnum.Week => lang.Week,
        PeriodTypeEnum.Month => lang.Month,
        PeriodTypeEnum.Quarter => lang.Quarter,
        PeriodTypeEnum.Year => lang.Year,
        PeriodTypeEnum.WorkingDay => lang.WorkingDay,
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    private static string DayToString(DayOfWeek day, DateFormulaLanguage lang)
        => lang.Days.First(kvp => kvp.Value == day).Key;

    // ── Equality ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A shared <see cref="IEqualityComparer{T}"/> for <see cref="DateFormulaExpression"/>
    /// that delegates to <see cref="Equals(DateFormulaExpression)"/> and <see cref="GetHashCode()"/>.
    /// Suitable for use as a dictionary or hash-set key comparer.
    /// </summary>
    public static IEqualityComparer<DateFormulaExpression> Comparer { get; }
        = EqualityComparer<DateFormulaExpression>.Default;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="other"/> represents the same formula
    /// (same start/end anchor, same base period, and the same sequence of steps).
    /// </summary>
    public bool Equals(DateFormulaExpression? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return IsStart == other.IsStart
            && BasePeriod == other.BasePeriod
            && Steps.SequenceEqual(other.Steps);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DateFormulaExpression other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = HashCode.Combine(IsStart, BasePeriod);
        foreach (var step in Steps)
            hash = HashCode.Combine(hash, step);
        return hash;
    }

    /// <summary>Returns <see langword="true"/> when both expressions represent the same formula.</summary>
    public static bool operator ==(DateFormulaExpression? left, DateFormulaExpression? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>Returns <see langword="true"/> when the expressions represent different formulas.</summary>
    public static bool operator !=(DateFormulaExpression? left, DateFormulaExpression? right)
        => !(left == right);
}

#pragma warning restore S3011
