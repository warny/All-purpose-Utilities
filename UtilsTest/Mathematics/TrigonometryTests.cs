using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics;

[TestClass]
public class TrigonometryTests
{
    private const double Delta = 1e-10;

    // ── Radian — trigonométrie de base ──────────────────────────────────────

    [TestMethod]
    public void Radian_Sin_KnownValues()
    {
        var trig = Trigonometry<double>.Radian;
        Assert.AreEqual(0.0,  trig.Sin(0),               Delta);
        Assert.AreEqual(1.0,  trig.Sin(Math.PI / 2),     Delta);
        Assert.AreEqual(0.0,  trig.Sin(Math.PI),          Delta);
        Assert.AreEqual(-1.0, trig.Sin(3 * Math.PI / 2), Delta);
    }

    [TestMethod]
    public void Radian_Cos_KnownValues()
    {
        var trig = Trigonometry<double>.Radian;
        Assert.AreEqual(1.0,  trig.Cos(0),           Delta);
        Assert.AreEqual(0.0,  trig.Cos(Math.PI / 2), Delta);
        Assert.AreEqual(-1.0, trig.Cos(Math.PI),     Delta);
    }

    [TestMethod]
    public void Radian_Tan_KnownValues()
    {
        var trig = Trigonometry<double>.Radian;
        Assert.AreEqual(0.0, trig.Tan(0),           Delta);
        Assert.AreEqual(1.0, trig.Tan(Math.PI / 4), Delta);
    }

    [TestMethod]
    public void Radian_ReciprocalFunctions_KnownValues()
    {
        var trig = Trigonometry<double>.Radian;
        Assert.AreEqual(1.0, trig.Sec(0),              Delta); // sec(0) = 1/cos(0) = 1
        Assert.AreEqual(1.0, trig.Csc(Math.PI / 2),   Delta); // csc(π/2) = 1/sin(π/2) = 1
        Assert.AreEqual(1.0, trig.Cot(Math.PI / 4),   Delta); // cot(π/4) = cos/sin = 1
    }

    // ── Radian — trigonométrie inverse ──────────────────────────────────────

    [TestMethod]
    public void Radian_InverseTrig_KnownValues()
    {
        var trig = Trigonometry<double>.Radian;
        Assert.AreEqual(Math.PI / 6, trig.Asin(0.5), Delta); // arcsin(1/2) = π/6
        Assert.AreEqual(Math.PI / 3, trig.Acos(0.5), Delta); // arccos(1/2) = π/3
        Assert.AreEqual(Math.PI / 4, trig.Atan(1.0), Delta); // arctan(1) = π/4
    }

    [TestMethod]
    public void Radian_Atan2_FourQuadrants()
    {
        var trig = Trigonometry<double>.Radian;
        Assert.AreEqual(Math.PI / 4,      trig.Atan2(1, 1),  Delta); // Q1
        Assert.AreEqual(3 * Math.PI / 4,  trig.Atan2(1, -1), Delta); // Q2
        Assert.AreEqual(-3 * Math.PI / 4, trig.Atan2(-1, -1), Delta); // Q3
        Assert.AreEqual(-Math.PI / 4,     trig.Atan2(-1, 1),  Delta); // Q4
    }

    // ── Radian — hyperbolique ────────────────────────────────────────────────

    [TestMethod]
    public void Radian_Hyperbolic_KnownValues()
    {
        var trig = Trigonometry<double>.Radian;
        Assert.AreEqual(0.0, trig.Sinh(0), Delta);
        Assert.AreEqual(1.0, trig.Cosh(0), Delta);
        Assert.AreEqual(0.0, trig.Tanh(0), Delta);
        Assert.AreEqual(1.0, trig.Sech(0), Delta); // sech(0) = 1/cosh(0) = 1
    }

    [TestMethod]
    public void Radian_InverseHyperbolic_KnownValues()
    {
        var trig = Trigonometry<double>.Radian;
        Assert.AreEqual(0.0, trig.Asinh(0), Delta);
        Assert.AreEqual(0.0, trig.Acosh(1), Delta);
        Assert.AreEqual(0.0, trig.Atanh(0), Delta);
    }

    // ── Radian — normalisation et arithmétique angulaire ────────────────────

    [TestMethod]
    public void Radian_Normalize0To2Max_ReducesMod2Pi()
    {
        var trig = Trigonometry<double>.Radian;
        Assert.AreEqual(Math.PI,     trig.Normalize0To2Max(3 * Math.PI),      Delta);
        Assert.AreEqual(Math.PI / 2, trig.Normalize0To2Max(5 * Math.PI / 2),  Delta);
        Assert.AreEqual(0.0,         trig.Normalize0To2Max(0),                 Delta);
        Assert.AreEqual(0.0,         trig.Normalize0To2Max(2 * Math.PI),       Delta);
    }

    [TestMethod]
    public void Radian_AddAngles_WrapsAround()
    {
        var trig = Trigonometry<double>.Radian;
        // 3π/2 + π = 5π/2 → mod 2π = π/2
        Assert.AreEqual(Math.PI / 2, trig.AddAngles(3 * Math.PI / 2, Math.PI), Delta);
    }

    [TestMethod]
    public void Radian_SubtractAngles_WrapsAround()
    {
        var trig = Trigonometry<double>.Radian;
        // π/4 - π/2 = -π/4 → mod 2π = 7π/4
        Assert.AreEqual(7 * Math.PI / 4, trig.SubtractAngles(Math.PI / 4, Math.PI / 2), Delta);
    }

    [TestMethod]
    public void Radian_FromRadian_ToRadian_AreIdentity()
    {
        var trig = Trigonometry<double>.Radian;
        Assert.AreEqual(1.5, trig.FromRadian(1.5), Delta);
        Assert.AreEqual(1.5, trig.ToRadian(1.5),   Delta);
    }

    // ── Degrés ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void Degree_Sin_KnownValues()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(0.0,  trig.Sin(0),   Delta);
        Assert.AreEqual(1.0,  trig.Sin(90),  Delta);
        Assert.AreEqual(0.0,  trig.Sin(180), Delta);
        Assert.AreEqual(-1.0, trig.Sin(270), Delta);
    }

    [TestMethod]
    public void Degree_Cos_KnownValues()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(1.0,  trig.Cos(0),   Delta);
        Assert.AreEqual(0.0,  trig.Cos(90),  Delta);
        Assert.AreEqual(-1.0, trig.Cos(180), Delta);
    }

    [TestMethod]
    public void Degree_Tan_KnownValues()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(0.0, trig.Tan(0),  Delta);
        Assert.AreEqual(1.0, trig.Tan(45), Delta);
    }

    [TestMethod]
    public void Degree_NormalizeMinToMax_ReducesToMinus180_180()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(-90.0, trig.NormalizeMinToMax(270),  Delta);
        Assert.AreEqual(90.0,  trig.NormalizeMinToMax(-270), Delta);
        Assert.AreEqual(0.0,   trig.NormalizeMinToMax(360),  Delta);
        Assert.AreEqual(0.0,   trig.NormalizeMinToMax(0),    Delta);
    }

    [TestMethod]
    public void Degree_Normalize0To2Max_ReducesTo0_360()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(90.0, trig.Normalize0To2Max(450), Delta);
        Assert.AreEqual(0.0,  trig.Normalize0To2Max(360), Delta);
        Assert.AreEqual(0.0,  trig.Normalize0To2Max(0),   Delta);
    }

    [TestMethod]
    public void Degree_AddAngles_WrapsAround()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(10.0, trig.AddAngles(350, 20), Delta);
    }

    [TestMethod]
    public void Degree_SubtractAngles_WrapsAround()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(350.0, trig.SubtractAngles(10, 20), Delta);
    }

    [TestMethod]
    public void Degree_FromRadian_ToRadian()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(180.0,    trig.FromRadian(Math.PI), Delta);
        Assert.AreEqual(Math.PI,  trig.ToRadian(180),       Delta);
    }

    // ── Grades ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void Grade_Sin_KnownValues()
    {
        var trig = Trigonometry<double>.Grade;
        Assert.AreEqual(0.0,  trig.Sin(0),   Delta); // 0 gr = 0°
        Assert.AreEqual(1.0,  trig.Sin(100), Delta); // 100 gr = 90°
        Assert.AreEqual(0.0,  trig.Sin(200), Delta); // 200 gr = 180°
        Assert.AreEqual(-1.0, trig.Sin(300), Delta); // 300 gr = 270°
    }

    [TestMethod]
    public void Grade_Cos_KnownValues()
    {
        var trig = Trigonometry<double>.Grade;
        Assert.AreEqual(1.0,  trig.Cos(0),   Delta);
        Assert.AreEqual(-1.0, trig.Cos(200), Delta); // 200 gr = 180°
    }

    // ── Degrés — trigonométrie inverse ──────────────────────────────────────

    [TestMethod]
    public void Degree_InverseTrig_KnownValues()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(30.0, trig.Asin(0.5), Delta); // arcsin(1/2) = 30°
        Assert.AreEqual(60.0, trig.Acos(0.5), Delta); // arccos(1/2) = 60°
        Assert.AreEqual(45.0, trig.Atan(1.0), Delta); // arctan(1)   = 45°
    }

    [TestMethod]
    public void Degree_InverseTrig_Boundary()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(90.0,  trig.Asin(1.0),  Delta);  // arcsin(1)  = 90°
        Assert.AreEqual(0.0,   trig.Acos(1.0),  Delta);  // arccos(1)  = 0°
        Assert.AreEqual(0.0,   trig.Atan(0.0),  Delta);  // arctan(0)  = 0°
    }

    // ── Grades — trigonométrie inverse ──────────────────────────────────────

    [TestMethod]
    public void Grade_InverseTrig_KnownValues()
    {
        var trig = Trigonometry<double>.Grade;
        // 30° = 33.333... grad, 60° = 66.666... grad, 45° = 50 grad
        Assert.AreEqual(100.0 / 3.0, trig.Asin(0.5), Delta);
        Assert.AreEqual(200.0 / 3.0, trig.Acos(0.5), Delta);
        Assert.AreEqual(50.0,        trig.Atan(1.0),  Delta);
    }

    [TestMethod]
    public void Grade_InverseTrig_Boundary()
    {
        var trig = Trigonometry<double>.Grade;
        Assert.AreEqual(100.0, trig.Asin(1.0), Delta);  // arcsin(1) = 100 grad
        Assert.AreEqual(0.0,   trig.Acos(1.0), Delta);  // arccos(1) = 0 grad
    }

    // ── Radian — NormalizeMinToMax ──────────────────────────────────────────

    [TestMethod]
    public void Radian_NormalizeMinToMax_CorrectlyNormalizesToMinusPiPi()
    {
        var trig = Trigonometry<double>.Radian;
        // Values that should map to themselves (already in [-π, π))
        Assert.AreEqual(0.0,          trig.NormalizeMinToMax(0),              Delta);
        Assert.AreEqual(Math.PI / 2,  trig.NormalizeMinToMax(Math.PI / 2),   Delta);
        Assert.AreEqual(-Math.PI / 2, trig.NormalizeMinToMax(-Math.PI / 2),  Delta);
        // 2π → 0 (wraps to 0)
        Assert.AreEqual(0.0,          trig.NormalizeMinToMax(2 * Math.PI),    Delta);
        // 3π/2 → -π/2  (wraps)
        Assert.AreEqual(-Math.PI / 2, trig.NormalizeMinToMax(3 * Math.PI / 2), Delta);
        // -3π/2 → π/2  (wraps negative)
        Assert.AreEqual(Math.PI / 2,  trig.NormalizeMinToMax(-3 * Math.PI / 2), Delta);
    }

    // ── Trigonometry.Get() — cache ───────────────────────────────────────────

    [TestMethod]
    public void Get_SamePerigon_ReturnsSameInstance()
    {
        var a = Trigonometry<double>.Get(360);
        var b = Trigonometry<double>.Get(360);
        Assert.AreSame(a, b);
    }

    [TestMethod]
    public void Get_Degree_ReturnsSameInstanceAsProperty()
    {
        Assert.AreSame(Trigonometry<double>.Degree, Trigonometry<double>.Get(360));
    }

    [TestMethod]
    public void Get_Radian_ReturnsSameInstanceAsProperty()
    {
        Assert.AreSame(Trigonometry<double>.Radian, Trigonometry<double>.Get(2 * Math.PI));
    }

    [TestMethod]
    public void Get_Grade_ReturnsSameInstanceAsProperty()
    {
        Assert.AreSame(Trigonometry<double>.Grade, Trigonometry<double>.Get(400));
    }

    // ── Degree — AreEqual (tolerance, circular) ──────────────────────────────

    [TestMethod]
    public void Degree_AreEqual_TrueForValuesWithinTolerance()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.IsTrue(trig.AreEqual(10.0, 10.000001, 1e-5));
        Assert.IsTrue(trig.AreEqual(10.0, 10.0, 0));
    }

    [TestMethod]
    public void Degree_AreEqual_FalseForValuesBeyondTolerance()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.IsFalse(trig.AreEqual(10.0, 10.1, 1e-5));
    }

    [TestMethod]
    public void Degree_AreEqual_HandlesWraparoundAcrossZero()
    {
        var trig = Trigonometry<double>.Degree;
        // 359.999999 and 0.000001 are 0.000002 apart going through the 0/360 seam,
        // not ~360 apart as a naive |a - b| would suggest.
        Assert.IsTrue(trig.AreEqual(359.999999, 0.000001, 1e-5));
        Assert.IsFalse(trig.AreEqual(359.999999, 0.000001, 1e-7));
    }

    // ── Degree — AreEqualRounded / NormalizeRounded ──────────────────────────

    [TestMethod]
    public void Degree_AreEqualRounded_TrueWhenBothRoundToSameValue()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.IsTrue(trig.AreEqualRounded(10.123456, 10.1234560001, 5));
    }

    [TestMethod]
    public void Degree_AreEqualRounded_FalseAcrossARoundingBoundary()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.IsFalse(trig.AreEqualRounded(1.0000449999, 1.0000450001, 5));
    }

    [TestMethod]
    public void Degree_AreEqualRounded_HandlesWraparoundNearZero()
    {
        var trig = Trigonometry<double>.Degree;
        // 359.999998° and -179.999998°+360°=180.000002° style seams: here, a value just
        // below a full turn and a small positive value near zero must compare equal once
        // both are normalized to [0, 360) and rounded.
        Assert.IsTrue(trig.AreEqualRounded(359.999998, 0.000002, 5));
    }

    [TestMethod]
    public void Degree_NormalizeRounded_WrapsFullTurnBackToZero()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(0.0, trig.NormalizeRounded(359.999998, 5));
        Assert.AreEqual(0.0, trig.NormalizeRounded(0.000002, 5));
    }

    [TestMethod]
    public void Degree_NormalizeRounded_IsAlwaysWithinPerigonRange()
    {
        var trig = Trigonometry<double>.Degree;
        Assert.AreEqual(180.0, trig.NormalizeRounded(-180.0, 5));
        Assert.AreEqual(90.0, trig.NormalizeRounded(450.0, 5));
    }
}
