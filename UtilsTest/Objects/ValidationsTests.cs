using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Objects;

namespace UtilsTest.Objects;

/// <summary>
/// Tests for <see cref="CheckBase{T}"/>, <see cref="Arg{T}"/>, <see cref="Variable{T}"/>,
/// and the <see cref="Validations"/> extension methods.
/// Covers items #62 (consistent predicate-return contract), #63 (independent error accumulation),
/// and #64 (structured exception identity for multiple failures).
/// </summary>
[TestClass]
public class ValidationsTests
{
    // ------------------------------------------------------------------ #62: MustBeOfRank returns Exception? (not throw)

    [TestMethod]
    public void MustBeOfRank_MatchingRank_ReturnsNull()
    {
        int[] array = [1, 2, 3];
        Exception? result = array.MustBeOfRank(1);
        Assert.IsNull(result, "MustBeOfRank must return null when the rank matches.");
    }

    [TestMethod]
    public void MustBeOfRank_MismatchedRank_ReturnsException()
    {
        // A 1-D array with expected rank 2 must return an Exception, not throw.
        int[] array = [1, 2, 3];
        Exception? result = array.MustBeOfRank(2);
        Assert.IsNotNull(result, "MustBeOfRank must return an exception when the rank does not match.");
        // ArrayDimensionException is internal; verify through message content (#62 consistent contract).
        StringAssert.Contains(result.Message, "rank 2",
            "The exception message must name the expected rank.");
    }

    [TestMethod]
    public void MustBeOfRank_MismatchedRank_DoesNotThrow()
    {
        // Before #62, MustBeOfRank threw instead of returning. Prove it no longer throws.
        int[] array = [1, 2, 3];
        try
        {
            Exception? result = array.MustBeOfRank(2);
            // If we reach here the method returned; that is the correct behaviour.
        }
        catch (Exception ex) when (ex is not AssertFailedException)
        {
            Assert.Fail($"MustBeOfRank must not throw; it should return the exception. Got: {ex.GetType().Name}");
        }
    }

    [TestMethod]
    public void MustBeOfSize_MatchingSizes_ReturnsNull()
    {
        int[,] array = new int[2, 3];
        Exception? result = array.MustBeOfSize([2, 3]);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void MustBeOfSize_MismatchedSize_ReturnsException()
    {
        int[,] array = new int[2, 3];
        Exception? result = array.MustBeOfSize([2, 4]);
        Assert.IsNotNull(result, "MustBeOfSize must return a non-null exception when sizes do not match (#62).");
        StringAssert.Contains(result.Message, "size",
            "Exception message must describe the size constraint.");
    }

    // ------------------------------------------------------------------ #63: Must accumulates all independent errors

    [TestMethod]
    public void Must_TwoFailingChecks_AccumulatesBothErrors()
    {
        // Both checks must run even though the first one records an error.
        // Before #63, Must used Value (which throws on accumulated errors), so the second check
        // would never execute once the first check recorded a failure.
        var validator = new Arg<int>(0);

        validator.Must(
            v => v > 10 ? null : new ArgumentException("must be > 10"),
            v => v > 20 ? null : new ArgumentException("must be > 20")
        );

        Assert.AreEqual(2, GetErrorCount(validator),
            "Both checks should have run independently and accumulated their errors.");
    }

    [TestMethod]
    public void Must_NullChecksArray_ThrowsArgumentNullException()
    {
        var validator = new Arg<int>(5);
        Assert.ThrowsExactly<ArgumentNullException>(() => validator.Must(null!));
    }

    [TestMethod]
    public void Must_NullDelegateInArray_ThrowsArgumentException()
    {
        var validator = new Arg<int>(5);
        Assert.ThrowsExactly<ArgumentException>(() =>
            validator.Must(v => null, null!));
    }

    [TestMethod]
    public void Must_PassingChecks_RecordsNoErrors()
    {
        var validator = new Arg<int>(42);
        validator.Must(
            v => v > 10 ? null : new ArgumentException("must be > 10"),
            v => v < 100 ? null : new ArgumentException("must be < 100")
        );
        // ThrowErrors should not throw when all checks pass.
        validator.ThrowErrors();
    }

    // ------------------------------------------------------------------ #63: Value getter respects accumulated errors

    [TestMethod]
    public void Value_AfterFailingMust_ThrowsWithAccumulatedErrors()
    {
        var validator = new Arg<int>(0);
        validator.Must(v => v > 10 ? null : new ArgumentException("must be > 10"));

        // Accessing Value must surface the accumulated error.
        try
        {
            _ = (int)validator;
            Assert.Fail("Expected an exception when accessing Value with accumulated errors.");
        }
        catch (AggregateException)
        {
            // Expected for multiple errors (though here we have one, single-error falls through to Errors[0]).
        }
        catch (ArgumentException)
        {
            // Expected: single error is rethrown directly as ArgumentException.
        }
    }

    // ------------------------------------------------------------------ #64: ThrowErrors uses AggregateException for multiple errors

    [TestMethod]
    public void ThrowErrors_SingleError_RethrowsOriginalException()
    {
        var validator = new Arg<int>(0);
        validator.Must(v => v > 0 ? null : new ArgumentOutOfRangeException("v", "must be positive"));

        // A single accumulated error must be rethrown as-is, preserving the exact type.
        // This is critical so callers can catch the specific exception type (#64).
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => validator.ThrowErrors());
    }

    [TestMethod]
    public void ThrowErrors_MultipleErrors_ThrowsAggregateException()
    {
        var validator = new Arg<int>(0);
        validator.Must(
            v => v > 10 ? null : new ArgumentException("must be > 10"),
            v => v > 20 ? null : new ArgumentException("must be > 20")
        );

        // Multiple errors must be surfaced as AggregateException so callers can inspect each cause (#64).
        var agg = Assert.ThrowsExactly<AggregateException>(() => validator.ThrowErrors());
        Assert.AreEqual(2, agg.InnerExceptions.Count,
            "AggregateException must contain all individual validation failures.");
        Assert.IsTrue(agg.InnerExceptions[0].Message.Contains("10"),
            "First inner exception must carry the first failure's message.");
        Assert.IsTrue(agg.InnerExceptions[1].Message.Contains("20"),
            "Second inner exception must carry the second failure's message.");
    }

    [TestMethod]
    public void ThrowErrors_NoErrors_DoesNotThrow()
    {
        var validator = new Arg<int>(5);
        validator.Must(v => v > 0 ? null : new ArgumentException("must be positive"));
        // Should complete without throwing.
        validator.ThrowErrors();
    }

    [TestMethod]
    public void CheckBase_Variable_MultipleErrors_ThrowsAggregateException()
    {
        // Variable<T> uses the base ThrowErrors; verify AggregateException there too (#64).
        var validator = new Variable<string>("hello");
        validator.Must(
            v => v.Length > 10 ? null : new InvalidOperationException("too short"),
            v => v.StartsWith("x", StringComparison.Ordinal) ? null : new InvalidOperationException("must start with x")
        );

        var agg = Assert.ThrowsExactly<AggregateException>(() => validator.ThrowErrors());
        Assert.AreEqual(2, agg.InnerExceptions.Count);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Reads the error count from a <see cref="CheckBase{T}"/> without triggering Value-related throws.
    /// Done by calling ThrowErrors and counting inner exceptions in the AggregateException.
    /// For a single error, catches the direct throw.
    /// </summary>
    private static int GetErrorCount<T>(CheckBase<T> validator)
    {
        try
        {
            validator.ThrowErrors();
            return 0;
        }
        catch (AggregateException agg)
        {
            return agg.InnerExceptions.Count;
        }
        catch
        {
            return 1;
        }
    }
}
