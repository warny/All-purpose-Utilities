using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Net;

/// <summary>
/// Regression tests for DNS condition expression compilation.
/// </summary>
[TestClass]
public class DNSExpressionTests
{
    /// <summary>
    /// Ensures boolean literals are supported in DNS condition expressions.
    /// </summary>
    [TestMethod]
    public void BuildExpression_BooleanLiteralCondition_EvaluatesCorrectly()
    {
        var parameter = Expression.Parameter(typeof(SampleRecord), "record");
        var expression = BuildDnsConditionExpression(parameter, "Flag == true");
        var lambda = Expression.Lambda<Func<SampleRecord, bool>>(Expression.Convert(expression, typeof(bool)), parameter).Compile();

        Assert.IsTrue(lambda(new SampleRecord { Flag = true }));
        Assert.IsFalse(lambda(new SampleRecord { Flag = false }));
    }

    /// <summary>
    /// Ensures mixed numeric comparisons do not narrow constants in an order-dependent way.
    /// </summary>
    [TestMethod]
    public void BuildExpression_MixedNumericCondition_AvoidsNarrowingWraparound()
    {
        var parameter = Expression.Parameter(typeof(SampleRecord), "record");
        var expression = BuildDnsConditionExpression(parameter, "RetryCount == 256");
        var lambda = Expression.Lambda<Func<SampleRecord, bool>>(Expression.Convert(expression, typeof(bool)), parameter).Compile();

        Assert.IsFalse(lambda(new SampleRecord { RetryCount = 0 }));
    }

    /// <summary>
    /// Ensures mixed numeric comparisons stay symmetric when the literal is on the left side.
    /// </summary>
    [TestMethod]
    public void BuildExpression_MixedNumericConditionWithLeftLiteral_AvoidsNarrowingWraparound()
    {
        var parameter = Expression.Parameter(typeof(SampleRecord), "record");
        var expression = BuildDnsConditionExpression(parameter, "256 == RetryCount");
        var lambda = Expression.Lambda<Func<SampleRecord, bool>>(Expression.Convert(expression, typeof(bool)), parameter).Compile();

        Assert.IsFalse(lambda(new SampleRecord { RetryCount = 0 }));
    }

    /// <summary>
    /// Ensures quoted string literals are supported in DNS condition expressions.
    /// </summary>
    [TestMethod]
    public void BuildExpression_StringLiteralCondition_EvaluatesCorrectly()
    {
        var parameter = Expression.Parameter(typeof(SampleRecord), "record");
        var expression = BuildDnsConditionExpression(parameter, "Name == \"example\"");
        var lambda = Expression.Lambda<Func<SampleRecord, bool>>(Expression.Convert(expression, typeof(bool)), parameter).Compile();

        Assert.IsTrue(lambda(new SampleRecord { Name = "example" }));
        Assert.IsFalse(lambda(new SampleRecord { Name = "other" }));
    }

    /// <summary>
    /// Builds a DNS condition expression through reflection on the internal DNSExpression type.
    /// </summary>
    /// <param name="parameter">Root parameter expression.</param>
    /// <param name="condition">Condition string.</param>
    /// <returns>The generated expression tree.</returns>
    private static Expression BuildDnsConditionExpression(ParameterExpression parameter, string condition)
    {
        var dnsAssembly = typeof(Utils.Net.DNS.DNSHeader).Assembly;
        var dnsExpressionType = dnsAssembly.GetType("Utils.Net.DNS.DNSExpression")
            ?? throw new InvalidOperationException("Unable to load DNSExpression type.");
        var buildMethod = dnsExpressionType.GetMethod(
            "BuildExpression",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            [typeof(Expression), typeof(string)],
            null)
            ?? throw new InvalidOperationException("Unable to locate DNSExpression.BuildExpression.");

        return (Expression)(buildMethod.Invoke(null, [parameter, condition])
            ?? throw new InvalidOperationException("DNSExpression.BuildExpression returned null."));
    }

    /// <summary>
    /// Minimal test record used for DNS condition evaluation.
    /// </summary>
    private sealed class SampleRecord
    {
        /// <summary>
        /// Gets or sets a sample boolean field.
        /// </summary>
        public bool Flag { get; set; }

        /// <summary>
        /// Gets or sets a sample byte counter.
        /// </summary>
        public byte RetryCount { get; set; }

        /// <summary>
        /// Gets or sets a sample string name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
