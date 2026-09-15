using System.Globalization;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;
using Utils.Expressions;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.ReqnRoll;

/// <summary>
/// Provides shared ReqnRoll steps for user-visible symbolic expression transformations.
/// </summary>
[Binding]
public sealed class ExpressionTransformationSteps
{
    private readonly CSyntaxExpressionCompiler compiler = new();
    private ParameterExpression[]? parameters;
    private LambdaExpression? source;
    private Expression? transformed;
    private LambdaExpression[]? gradient;
    private string[]? gradientVariables;

    /// <summary>
    /// Configures the ordered set of double parameters shared by all expressions in the scenario.
    /// </summary>
    /// <param name="parameterList">A comma-separated list of parameter names.</param>
    [Given("the double parameters {string}")]
    public void GivenTheDoubleParameters(string parameterList)
    {
        string[] names = parameterList.Length == 0
            ? []
            : parameterList.Split(',').Select(name => name.Trim()).ToArray();

        string? duplicate = names
            .GroupBy(name => name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        Assert.IsNull(duplicate, $"The parameter name '{duplicate}' is declared more than once.");
        Assert.IsFalse(names.Any(string.IsNullOrWhiteSpace), "Parameter names must not be empty.");

        parameters = names.Select(name => Expression.Parameter(typeof(double), name)).ToArray();
        source = null;
        transformed = null;
        gradient = null;
        gradientVariables = null;
    }

    /// <summary>
    /// Compiles the scenario's source expression using its configured shared parameters.
    /// </summary>
    /// <param name="expressionText">The C-syntax mathematical expression body.</param>
    [Given("the source C-syntax expression {string}")]
    public void GivenTheSourceCSyntaxExpression(string expressionText)
    {
        source = CompileExpression(expressionText);
        transformed = null;
        gradient = null;
    }

    /// <summary>
    /// Simplifies the configured source expression.
    /// </summary>
    [When("I simplify the expression")]
    public void WhenISimplifyTheExpression() => transformed = new ExpressionSimplifier().Simplify(Source);

    /// <summary>
    /// Differentiates the configured source expression with respect to the named variable.
    /// </summary>
    /// <param name="variable">The differentiation variable name.</param>
    [When("I derive the expression with respect to {string}")]
    public void WhenIDeriveTheExpressionWithRespectTo(string variable) =>
        transformed = new ExpressionDerivation<double>(variable).Derivate(Source);

    /// <summary>
    /// Integrates the configured source expression without applying post-processing.
    /// </summary>
    /// <param name="variable">The integration variable name.</param>
    [When("I integrate the expression with respect to {string}")]
    public void WhenIIntegrateTheExpressionWithRespectTo(string variable) =>
        transformed = new ExpressionIntegration<double>(variable).Integrate(Source);

    /// <summary>
    /// Integrates and simplifies the configured source expression, matching the legacy structural contract.
    /// </summary>
    /// <param name="variable">The integration variable name.</param>
    [When("I integrate and simplify the expression with respect to {string}")]
    public void WhenIIntegrateAndSimplifyTheExpressionWithRespectTo(string variable)
    {
        Expression integral = new ExpressionIntegration<double>(variable).Integrate(Source);
        transformed = new ExpressionSimplifier().Simplify(integral);
    }

    /// <summary>
    /// Calculates a gradient for every source parameter in declared order.
    /// </summary>
    [When("I calculate the gradient")]
    public void WhenICalculateTheGradient()
    {
        gradientVariables = Parameters.Select(parameter => parameter.Name!).ToArray();
        gradient = Source.Gradient();
    }

    /// <summary>
    /// Calculates a gradient for an explicitly selected ordered subset of source parameters.
    /// </summary>
    /// <param name="parameterList">A comma-separated list of selected parameter names.</param>
    [When("I calculate the gradient with respect to {string}")]
    public void WhenICalculateTheGradientWithRespectTo(string parameterList)
    {
        string[] names = parameterList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        gradientVariables = names;
        gradient = Source.Gradient<double>(names);
    }

    /// <summary>
    /// Compares transformed and expected lambda bodies structurally while retaining their lambda metadata.
    /// </summary>
    /// <param name="expectedText">The expected C-syntax expression body.</param>
    [Then("the transformed expression is structurally equivalent to {string}")]
    public void ThenTheTransformedExpressionIsStructurallyEquivalentTo(string expectedText)
    {
        LambdaExpression expected = CompileExpression(expectedText);
        Expression actual = Transformed;
        Assert.AreEqual(expected, actual, ExpressionComparer.Default);
    }

    /// <summary>
    /// Simplifies the expected expression and compares it structurally with a normalized integration result.
    /// </summary>
    /// <param name="expectedText">The expected C-syntax expression body.</param>
    [Then("the normalized transformed expression is structurally equivalent to {string}")]
    public void ThenTheNormalizedTransformedExpressionIsStructurallyEquivalentTo(string expectedText)
    {
        Expression expected = new ExpressionSimplifier().Simplify(CompileExpression(expectedText));
        Assert.AreEqual(expected, Transformed, ExpressionComparer.Default);
    }

    /// <summary>
    /// Evaluates one-variable transformed and expected expressions at invariant-culture sample values.
    /// </summary>
    /// <param name="expectedText">The expected C-syntax expression body.</param>
    /// <param name="sampleList">Comma-separated input values.</param>
    /// <param name="toleranceText">The invariant-culture maximum accepted absolute difference.</param>
    [Then(@"the transformed expression is numerically equivalent to ""([^""]*)"" at ""([^""]*)"" with tolerance (.*)")]
    public void ThenTheTransformedExpressionIsNumericallyEquivalentTo(
        string expectedText,
        string sampleList,
        string toleranceText)
    {
        Assert.AreEqual(1, Parameters.Length, "Numerical equivalence currently requires exactly one parameter.");
        double tolerance = double.Parse(toleranceText, NumberStyles.Float, CultureInfo.InvariantCulture);
        Func<double, double> expected = (Func<double, double>)CompileExpression(expectedText).Compile();
        Func<double, double> actual = (Func<double, double>)RequireLambda(Transformed).Compile();

        foreach (string sampleText in sampleList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            double sample = double.Parse(sampleText, CultureInfo.InvariantCulture);
            Assert.AreEqual(expected(sample), actual(sample), tolerance, $"The expressions differ at x={sampleText}.");
        }
    }

    /// <summary>
    /// Compares a simplified gradient with expected expressions compiled against the scenario parameters.
    /// </summary>
    /// <param name="table">Expected variable names and expression bodies in gradient order.</param>
    [Then("the gradient is structurally equivalent to")]
    public void ThenTheGradientIsStructurallyEquivalentTo(Table table)
    {
        LambdaExpression[] actualGradient = gradient ?? throw new InvalidOperationException("Calculate a gradient before asserting it.");
        Assert.AreEqual(table.RowCount, actualGradient.Length, "The gradient has an unexpected number of components.");
        ExpressionSimplifier simplifier = new();

        for (int index = 0; index < table.RowCount; index++)
        {
            string variable = table.Rows[index]["variable"];
            Assert.AreEqual(variable, GradientVariables[index]);
            Expression expected = simplifier.Simplify(CompileExpression(table.Rows[index]["expression"]));
            Expression actual = simplifier.Simplify(actualGradient[index]);
            Assert.AreEqual(expected, actual, ExpressionComparer.Default, $"Unexpected gradient component for '{variable}'.");
        }
    }

    /// <summary>
    /// Compiles a C-syntax mathematical expression body to a lambda of the appropriate arity.
    /// Source and expected expressions receive the same parameter instances to make identity-sensitive comparison deterministic.
    /// </summary>
    /// <param name="expressionText">The C-syntax expression body.</param>
    /// <returns>A lambda expression with zero through four double parameters.</returns>
    private LambdaExpression CompileExpression(string expressionText)
    {
        ParameterExpression[] sharedParameters = Parameters;
        LambdaExpression compiled = sharedParameters.Length switch
        {
            0 => compiler.CompileExpression<Func<double>>(expressionText, sharedParameters, typeof(double), false),
            1 => compiler.CompileExpression<Func<double, double>>(expressionText, sharedParameters, typeof(double), false),
            2 => compiler.CompileExpression<Func<double, double, double>>(expressionText, sharedParameters, typeof(double), false),
            3 => compiler.CompileExpression<Func<double, double, double, double>>(expressionText, sharedParameters, typeof(double), false),
            4 => compiler.CompileExpression<Func<double, double, double, double, double>>(expressionText, sharedParameters, typeof(double), false),
            _ => throw new NotSupportedException("Expression transformation scenarios support at most four parameters.")
        };
        return (LambdaExpression)NumericLiteralPromotionVisitor.Instance.Visit(compiled)!;
    }

    /// <summary>
    /// Returns an expression as a lambda or fails with a clear scenario-state diagnostic.
    /// </summary>
    /// <param name="expression">The expression expected to be a lambda.</param>
    /// <returns>The supplied lambda expression.</returns>
    private static LambdaExpression RequireLambda(Expression expression) =>
        expression as LambdaExpression
        ?? throw new InvalidOperationException("The transformation did not produce a lambda expression.");

    /// <summary>
    /// Gets the ordered parameter names selected for the current gradient.
    /// </summary>
    private string[] GradientVariables =>
        gradientVariables ?? throw new InvalidOperationException("Calculate a gradient before asserting it.");

    /// <summary>Gets the configured shared parameter expressions.</summary>
    private ParameterExpression[] Parameters =>
        parameters ?? throw new InvalidOperationException("Configure the double parameters before compiling an expression.");

    /// <summary>Gets the configured source lambda expression.</summary>
    private LambdaExpression Source =>
        source ?? throw new InvalidOperationException("Configure a source C-syntax expression before transforming it.");

    /// <summary>Gets the result of the most recent scalar transformation.</summary>
    private Expression Transformed =>
        transformed ?? throw new InvalidOperationException("Transform the source expression before asserting its result.");

    /// <summary>
    /// Promotes compiler-generated converted integer literals to double constants so the compiled
    /// trees match the statically typed double expressions used by the symbolic APIs.
    /// </summary>
    private sealed class NumericLiteralPromotionVisitor : ExpressionVisitor
    {
        /// <summary>Gets the stateless visitor instance.</summary>
        public static NumericLiteralPromotionVisitor Instance { get; } = new();

        /// <summary>
        /// Replaces an implicit integer-to-double conversion around a literal with the equivalent double literal.
        /// </summary>
        /// <param name="node">The unary expression to inspect.</param>
        /// <returns>The promoted constant, or the normally visited expression.</returns>
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == System.Linq.Expressions.ExpressionType.Convert
                && node.Type == typeof(double)
                && TryReadIntegerLiteral(node.Operand, out int value))
            {
                return Expression.Constant((double)value);
            }

            return base.VisitUnary(node);
        }

        /// <summary>
        /// Reads a positive or unary-negative integer literal from a compiler expression.
        /// </summary>
        /// <param name="expression">The potential integer literal expression.</param>
        /// <param name="value">The signed integer value when recognized.</param>
        /// <returns><see langword="true"/> when the expression is an integer literal.</returns>
        private static bool TryReadIntegerLiteral(Expression expression, out int value)
        {
            if (expression is ConstantExpression { Type: not null, Value: int constant })
            {
                value = constant;
                return true;
            }

            if (expression is UnaryExpression
                {
                    NodeType: System.Linq.Expressions.ExpressionType.Negate,
                    Operand: ConstantExpression { Type: not null, Value: int magnitude }
                })
            {
                value = -magnitude;
                return true;
            }

            value = default;
            return false;
        }
    }
}
