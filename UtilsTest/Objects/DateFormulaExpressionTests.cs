using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using Utils.Dates;

namespace UtilsTest.Objects;

[TestClass]
public class DateFormulaExpressionTests
{
    private sealed class WeekEndCalendarProvider : ICalendarProvider
    {
        private static readonly DayOfWeek[] _weekEnds = [DayOfWeek.Saturday, DayOfWeek.Sunday];

        public int GetNonWorkingDaysCount(DateTime start, DateTime end) => GetHollydays(start, end).Count();
        public IEnumerable<DateTime> GetHollydays(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                if (System.Array.IndexOf(_weekEnds, d.DayOfWeek) >= 0)
                    yield return d;
        }
        public IEnumerable<DateTime> GetWorkingDays(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                if (System.Array.IndexOf(_weekEnds, d.DayOfWeek) < 0)
                    yield return d;
        }
        public int GetWorkingDaysCount(DateTime start, DateTime end) => GetWorkingDays(start, end).Count();
    }

    private static readonly CultureInfo Fr = new("fr-FR");
    private static readonly CultureInfo En = new("en-US");

    // ──────────────────────────────────────────────────────────────────────────
    //  Parse — IR properties
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_EndOfWeekPlusWorkingDays_BuildsCorrectIR()
    {
        // "FS+3O" (French): End, Week, +3 WorkingDay
        var expr = DateFormulaExpression.Parse("FS+3O", Fr);

        Assert.IsFalse(expr.IsStart);
        Assert.AreEqual(PeriodTypeEnum.Week, expr.BasePeriod);
        Assert.AreEqual(1, expr.Steps.Count);
        Assert.AreEqual(DateFormulaStepKind.AddPeriod, expr.Steps[0].Kind);
        Assert.AreEqual(3, expr.Steps[0].SignedValue);
        Assert.AreEqual(PeriodTypeEnum.WorkingDay, expr.Steps[0].Unit);
    }

    [TestMethod]
    public void Parse_StartOfYearMinusMonth_BuildsCorrectIR()
    {
        // "DA-1M" (French): Start, Year, -1 Month
        var expr = DateFormulaExpression.Parse("DA-1M", Fr);

        Assert.IsTrue(expr.IsStart);
        Assert.AreEqual(PeriodTypeEnum.Year, expr.BasePeriod);
        Assert.AreEqual(1, expr.Steps.Count);
        Assert.AreEqual(DateFormulaStepKind.AddPeriod, expr.Steps[0].Kind);
        Assert.AreEqual(-1, expr.Steps[0].SignedValue);
        Assert.AreEqual(PeriodTypeEnum.Month, expr.Steps[0].Unit);
    }

    [TestMethod]
    public void Parse_AdjustToNextWeekday_BuildsCorrectIR()
    {
        // "FM+1J+Lu" (French): End, Month, +1 Day, then next Monday
        var expr = DateFormulaExpression.Parse("FM+1J+Lu", Fr);

        Assert.AreEqual(2, expr.Steps.Count);
        Assert.AreEqual(DateFormulaStepKind.AddPeriod, expr.Steps[0].Kind);
        Assert.AreEqual(DateFormulaStepKind.AdjustToWeekDay, expr.Steps[1].Kind);
        Assert.AreEqual(1, expr.Steps[1].SignedValue);  // forward
        Assert.AreEqual(DayOfWeek.Monday, expr.Steps[1].WeekDay);
    }

    [TestMethod]
    public void Parse_MoveToSameWeekDay_BuildsCorrectIR()
    {
        // "FM+1JLu" (French): End, Month, +1 Day, then Monday in same week
        var expr = DateFormulaExpression.Parse("FM+1JLu", Fr);

        Assert.AreEqual(2, expr.Steps.Count);
        Assert.AreEqual(DateFormulaStepKind.AddPeriod, expr.Steps[0].Kind);
        Assert.AreEqual(DateFormulaStepKind.MoveToSameWeekDay, expr.Steps[1].Kind);
        Assert.AreEqual(DayOfWeek.Monday, expr.Steps[1].WeekDay);
    }

    [TestMethod]
    public void Parse_WorkingDaySnapAtEnd_BuildsCorrectIR()
    {
        // "FS+O" (French): End, Week, +WorkingDay snap
        var expr = DateFormulaExpression.Parse("FS+O", Fr);

        Assert.AreEqual(1, expr.Steps.Count);
        Assert.AreEqual(DateFormulaStepKind.AdjustWorkingDay, expr.Steps[0].Kind);
        Assert.AreEqual(1, expr.Steps[0].SignedValue);  // forward
    }

    [TestMethod]
    public void Parse_WorkingDaySnapBackward_BuildsCorrectIR()
    {
        // "FS-O" (French): End, Week, -WorkingDay snap
        var expr = DateFormulaExpression.Parse("FS-O", Fr);

        Assert.AreEqual(1, expr.Steps.Count);
        Assert.AreEqual(DateFormulaStepKind.AdjustWorkingDay, expr.Steps[0].Kind);
        Assert.AreEqual(-1, expr.Steps[0].SignedValue);  // backward
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  ToString — round-trip in the same language
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ToString_FrenchRoundTrip_ReturnsSameFormula()
    {
        string[] frenchFormulas = ["FS+3O", "DA-1M", "FM+1J", "FM+1J+Lu", "FM+1JLu", "FS+O", "FS-O"];
        foreach (var formula in frenchFormulas)
        {
            var expr = DateFormulaExpression.Parse(formula, Fr);
            Assert.AreEqual(formula, expr.ToString(Fr), $"Round-trip failed for '{formula}'");
        }
    }

    [TestMethod]
    public void ToString_EnglishRoundTrip_ReturnsSameFormula()
    {
        string[] englishFormulas = ["EW+3O", "SY-1M", "EM+1D", "EM+1D+Mo", "EM+1DMo", "EW+O", "EW-O"];
        foreach (var formula in englishFormulas)
        {
            var expr = DateFormulaExpression.Parse(formula, En);
            Assert.AreEqual(formula, expr.ToString(En), $"Round-trip failed for '{formula}'");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  ToString — cross-language rendering
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ToString_FrenchExprToEnglish_ReturnsEnglishFormula()
    {
        Assert.AreEqual("EW+3O", DateFormulaExpression.Parse("FS+3O", Fr).ToString(En));
        Assert.AreEqual("SY-1M", DateFormulaExpression.Parse("DA-1M", Fr).ToString(En));
        Assert.AreEqual("EM+1D", DateFormulaExpression.Parse("FM+1J", Fr).ToString(En));
        Assert.AreEqual("EM+1D+Mo", DateFormulaExpression.Parse("FM+1J+Lu", Fr).ToString(En));
        Assert.AreEqual("EM+1DMo", DateFormulaExpression.Parse("FM+1JLu", Fr).ToString(En));
    }

    [TestMethod]
    public void ToString_EnglishExprToFrench_ReturnsFrenchFormula()
    {
        Assert.AreEqual("FS+3O", DateFormulaExpression.Parse("EW+3O", En).ToString(Fr));
        Assert.AreEqual("DA-1M", DateFormulaExpression.Parse("SY-1M", En).ToString(Fr));
        Assert.AreEqual("FM+1J", DateFormulaExpression.Parse("EM+1D", En).ToString(Fr));
    }

    [TestMethod]
    public void ToString_FrenchExprToInvariantCulture_ReturnsEnglishFormula()
    {
        // InvariantCulture has TwoLetterISOLanguageName = "iv"; provider falls back to "en".
        Assert.AreEqual("EW+3O", DateFormulaExpression.Parse("FS+3O", Fr).ToString(CultureInfo.InvariantCulture));
        Assert.AreEqual("EM+1D", DateFormulaExpression.Parse("FM+1J", Fr).ToString(CultureInfo.InvariantCulture));
        Assert.AreEqual("EW-O", DateFormulaExpression.Parse("FS-O", Fr).ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ToString_DefaultOverload_ReturnsEnglishForm()
    {
        var expr = DateFormulaExpression.Parse("FS+3O", Fr);
        Assert.AreEqual("EW+3O", expr.ToString());
    }

    [TestMethod]
    public void ToString_IFormattable_InvariantCulture_ReturnsEnglishForm()
    {
        IFormattable expr = DateFormulaExpression.Parse("FS+3O", Fr);
        Assert.AreEqual("EW+3O", expr.ToString(null, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ToString_IFormattable_FrenchProvider_ReturnsFrenchForm()
    {
        IFormattable expr = DateFormulaExpression.Parse("FS+3O", Fr);
        Assert.AreEqual("FS+3O", expr.ToString(null, Fr));
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Compile — results match DateFormula.Calculate
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Compile_ProducesSameResultAsCalculate_EndOfMonthPlusDay()
    {
        var date = new DateTime(2023, 3, 15);
        var expected = date.Calculate("FM+1J", Fr);

        var fn = DateFormulaExpression.Parse("FM+1J", Fr).Compile(Fr);

        Assert.AreEqual(expected, fn(date));
    }

    [TestMethod]
    public void Compile_ProducesSameResultAsCalculate_StartOfYearMinusMonth()
    {
        var date = new DateTime(2023, 3, 15);
        var expected = date.Calculate("DA-1M", Fr);

        var fn = DateFormulaExpression.Parse("DA-1M", Fr).Compile(Fr);

        Assert.AreEqual(expected, fn(date));
    }

    [TestMethod]
    public void Compile_ProducesSameResultAsCalculate_EndOfMonthNextMonday()
    {
        var date = new DateTime(2023, 10, 15);
        var expected = date.Calculate("FM+1J+Lu", Fr);

        var fn = DateFormulaExpression.Parse("FM+1J+Lu", Fr).Compile(Fr);

        Assert.AreEqual(expected, fn(date));
    }

    [TestMethod]
    public void Compile_ProducesSameResultAsCalculate_WorkingDays()
    {
        ICalendarProvider calendar = new WeekEndCalendarProvider();
        var date = new DateTime(2024, 4, 5);  // Friday
        var expected = date.Calculate("FS+3O", Fr, calendar);

        var fn = DateFormulaExpression.Parse("FS+3O", Fr).Compile(Fr, calendar);

        Assert.AreEqual(expected, fn(date));
    }

    [TestMethod]
    public void Compile_WorkingDaySnap_Forward()
    {
        ICalendarProvider calendar = new WeekEndCalendarProvider();
        var date = new DateTime(2024, 4, 6);  // Saturday
        var expected = date.Calculate("FS+O", Fr, calendar);

        var fn = DateFormulaExpression.Parse("FS+O", Fr).Compile(Fr, calendar);

        Assert.AreEqual(expected, fn(date));
    }

    [TestMethod]
    public void Compile_EnglishFormula_SameResultAsCalculate()
    {
        var date = new DateTime(2023, 3, 15);
        var expected = date.Calculate("EM+1D", En);

        var fn = DateFormulaExpression.Parse("EM+1D", En).Compile(En);

        Assert.AreEqual(expected, fn(date));
    }

    [TestMethod]
    public void Compile_ParsedFromFrenchCompiledAsEnglish_SameResult()
    {
        // Parsing FR and EN representations of the same formula must produce identical results.
        var date = new DateTime(2023, 3, 15);
        var fromFr = DateFormulaExpression.Parse("FM+1J", Fr).Compile(Fr)(date);
        var fromEn = DateFormulaExpression.Parse("EM+1D", En).Compile(En)(date);

        Assert.AreEqual(fromFr, fromEn);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Compile — missing calendar provider throws
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Compile_WorkingDaysWithoutCalendarProvider_Throws()
    {
        DateFormulaExpression.Parse("FS+3O", Fr).Compile(Fr);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Compile_WorkingDaySnapWithoutCalendarProvider_Throws()
    {
        DateFormulaExpression.Parse("FS+O", Fr).Compile(Fr);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  DateFormula.Parse overloads
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void DateFormula_Parse_ReturnsSameIRAsDirectParse()
    {
        var direct = DateFormulaExpression.Parse("FM+1J", Fr);
        var viaProp = DateFormula.Parse("FM+1J", Fr);

        Assert.AreEqual(direct.IsStart, viaProp.IsStart);
        Assert.AreEqual(direct.BasePeriod, viaProp.BasePeriod);
        Assert.AreEqual(direct.Steps.Count, viaProp.Steps.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  ToExpression — accessible expression tree
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ToExpression_ReturnsCompilableExpressionTree()
    {
        var expr = DateFormulaExpression.Parse("FM+1J", Fr);
        var lambdaExpr = expr.ToExpression(Fr);

        Assert.IsNotNull(lambdaExpr);
        var fn = lambdaExpr.Compile();
        var date = new DateTime(2023, 3, 15);
        Assert.AreEqual(new DateTime(2023, 4, 1), fn(date));
    }

    [TestMethod]
    public void DateFormula_ToExpression_ReturnsSameResultAsCompile()
    {
        var date = new DateTime(2023, 3, 15);
        var lambdaExpr = DateFormula.ToExpression("FM+1J", Fr);
        var fn = lambdaExpr.Compile();
        var expected = date.Calculate("FM+1J", Fr);

        Assert.AreEqual(expected, fn(date));
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Invalid inputs
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Parse_TooShort_Throws()
        => DateFormulaExpression.Parse("F", Fr);

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Parse_InvalidStartToken_Throws()
        => DateFormulaExpression.Parse("XM+1J", Fr);

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Parse_DayAsBasePeriod_Throws()
        => DateFormulaExpression.Parse("FJ", Fr);

    [DataTestMethod]
    [DataRow("FM+LuMa")]   // AdjustToWeekDay (+Lu) followed by MoveToSameWeekDay (Ma)
    [DataRow("FM+LuXYZ")]  // AdjustToWeekDay (+Lu) followed by unknown tokens
    [DataRow("FM+1JLuX")]  // MoveToSameWeekDay (Lu) with trailing garbage
    public void Parse_ContentAfterTerminalOperation_Throws(string formula)
    {
        Assert.ThrowsException<ArgumentException>(
            () => DateFormulaExpression.Parse(formula, Fr));
    }

    [TestMethod]
    public void Steps_IsReadOnly_CannotBeMutatedByDowncast()
    {
        var expr = DateFormulaExpression.Parse("FM+1J", Fr);
        // Steps is backed by ReadOnlyCollection, not List — the downcast must fail.
        Assert.IsFalse(expr.Steps is List<DateFormulaStep>);
    }
}
