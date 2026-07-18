using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Characterizes the common contract shared by the four explicit literal rule-call binding policies.
/// </summary>
[TestClass]
public class LiteralRuleCallExecutionPolicyContractTests
{
    /// <summary>
    /// Verifies that every explicit literal policy submits exactly one complete batch for a valid call.
    /// </summary>
    [TestMethod]
    public void LiteralPolicies_ValidCalls_SubmitExactlyOneCompleteBatch()
    {
        foreach (PolicyCase policyCase in PolicyCases())
        {
            int attempts = 0;
            IReadOnlyDictionary<string, object?>? batch = null;

            policyCase.CreatePolicy().BeforeRuleCall(policyCase.CreateValidContext(values =>
            {
                attempts++;
                batch = Copy(values);
                return true;
            }));

            Assert.AreEqual(1, attempts, policyCase.Name);
            Assert.IsNotNull(batch, policyCase.Name);
            CollectionAssert.AreEquivalent(policyCase.ExpectedKeys, batch!.Keys.ToArray(), policyCase.Name);
            policyCase.AssertValidBatch(batch);
        }
    }

    /// <summary>
    /// Verifies that validation failures occur before mutation and failed batch retention is not retried.
    /// </summary>
    [TestMethod]
    public void LiteralPolicies_InvalidOrRejectedCalls_AreAtomicAndSingleAttempt()
    {
        foreach (PolicyCase policyCase in PolicyCases())
        {
            int invalidAttempts = 0;
            policyCase.CreatePolicy().BeforeRuleCall(policyCase.CreateInvalidLiteralContext(values =>
            {
                invalidAttempts++;
                return true;
            }));
            Assert.AreEqual(0, invalidAttempts, policyCase.Name);

            int rejectedAttempts = 0;
            policyCase.CreatePolicy().BeforeRuleCall(policyCase.CreateValidContext(values =>
            {
                rejectedAttempts++;
                return false;
            }));
            Assert.AreEqual(1, rejectedAttempts, policyCase.Name);
        }
    }

    /// <summary>
    /// Verifies ignore/throw failure modes, no-op after-call behavior, null validation, and conservative defaults.
    /// </summary>
    [TestMethod]
    public void LiteralPolicies_FailureModesAndCallbacks_AreConsistent()
    {
        foreach (PolicyCase policyCase in PolicyCases())
        {
            policyCase.CreatePolicy(ParserRuleCallBindingFailureBehavior.IgnoreCall)
                .BeforeRuleCall(policyCase.CreateMissingDescriptorContext());

            ParserRuleCallBindingException exception = Assert.ThrowsException<ParserRuleCallBindingException>(() =>
                policyCase.CreatePolicy(ParserRuleCallBindingFailureBehavior.Throw)
                    .BeforeRuleCall(policyCase.CreateMissingDescriptorContext()), policyCase.Name);
            Assert.AreEqual("child", exception.RuleName, policyCase.Name);
            Assert.AreEqual(policyCase.CreateMissingDescriptorContext().RawArguments, exception.RawArguments, policyCase.Name);

            ParserRuleCallExecutionContext validContext = policyCase.CreateValidContext(static _ => true);
            policyCase.CreatePolicy().AfterRuleCall(validContext);
            Assert.ThrowsException<ArgumentNullException>(() => policyCase.CreatePolicy().BeforeRuleCall(null!), policyCase.Name);
            Assert.ThrowsException<ArgumentNullException>(() => policyCase.CreatePolicy().AfterRuleCall(null!), policyCase.Name);
        }

        Assert.AreSame(NullParserRuleCallExecutionPolicy.Instance, ParserRuntimeFeaturePolicy.Default.RuleCallExecutionPolicy);
        Assert.IsNotInstanceOfType(ParserRuntimeFeaturePolicy.Default.RuleCallExecutionPolicy, typeof(PositionalLiteralRuleCallExecutionPolicy));
        Assert.IsNotInstanceOfType(ParserRuntimeFeaturePolicy.Default.RuleCallExecutionPolicy, typeof(NamedLiteralRuleCallExecutionPolicy));
        Assert.IsNotInstanceOfType(ParserRuntimeFeaturePolicy.Default.RuleCallExecutionPolicy, typeof(TypedPositionalLiteralRuleCallExecutionPolicy));
        Assert.IsNotInstanceOfType(ParserRuntimeFeaturePolicy.Default.RuleCallExecutionPolicy, typeof(TypedNamedLiteralRuleCallExecutionPolicy));
    }

    /// <summary>
    /// Verifies typed-only conversion/default behavior and untyped-only exact-coverage behavior.
    /// </summary>
    [TestMethod]
    public void LiteralPolicies_TypedAndUntypedBoundaries_RemainDistinct()
    {
        foreach (PolicyCase policyCase in PolicyCases())
        {
            int defaultAttempts = 0;
            policyCase.CreatePolicy().BeforeRuleCall(policyCase.CreateDefaultCoverageContext(values =>
            {
                defaultAttempts++;
                if (policyCase.IsTyped)
                {
                    Assert.AreEqual(2, values.Count, policyCase.Name);
                    Assert.AreEqual((byte)2, values["second"], policyCase.Name);
                }

                return true;
            }));
            Assert.AreEqual(policyCase.IsTyped ? 1 : 0, defaultAttempts, policyCase.Name);

            int conversionAttempts = 0;
            policyCase.CreatePolicy().BeforeRuleCall(policyCase.CreateInvalidTypedConversionContext(values =>
            {
                conversionAttempts++;
                if (!policyCase.IsTyped)
                {
                    Assert.AreEqual("text", values["value"], policyCase.Name);
                }

                return true;
            }));
            Assert.AreEqual(policyCase.IsTyped ? 0 : 1, conversionAttempts, policyCase.Name);
        }
    }

    /// <summary>
    /// Verifies representative failure metadata without requiring unavailable typed details from untyped policies.
    /// </summary>
    [TestMethod]
    public void ParserRuleCallBindingException_ExposesPolicySpecificStructuredMetadata()
    {
        ParserRuleCallBindingException untyped = Assert.ThrowsException<ParserRuleCallBindingException>(() =>
            new PositionalLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw)
                .BeforeRuleCall(PositionalContext(["notALiteral"], Descriptor(Parameter("value", "int")))));
        Assert.AreEqual("child", untyped.RuleName);
        Assert.AreEqual("notALiteral", untyped.RawArguments);
        Assert.AreEqual(0, untyped.ArgumentIndex);
        Assert.IsNull(untyped.ParameterName);
        Assert.IsNull(untyped.DeclaredType);
        StringAssert.Contains(untyped.Message, "supported simple literal");

        ParserRuleCallBindingException typed = Assert.ThrowsException<ParserRuleCallBindingException>(() =>
            new TypedPositionalLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw)
                .BeforeRuleCall(PositionalContext(["300"], Descriptor(Parameter("value", "byte")))));
        Assert.AreEqual("child", typed.RuleName);
        Assert.AreEqual("300", typed.RawArguments);
        Assert.AreEqual(0, typed.ArgumentIndex);
        Assert.AreEqual("value", typed.ParameterName);
        Assert.AreEqual("byte", typed.DeclaredType);
        StringAssert.Contains(typed.Message, "byte");
    }

    /// <summary>
    /// Verifies runtime rollback observes the winning bound value instead of a rejected alternative seed.
    /// </summary>
    [TestMethod]
    public void RuntimeLiteralPolicies_RollbackUsesWinningBoundValue()
    {
        foreach (PolicyCase policyCase in PolicyCases())
        {
            string[] rollbackObservations = ParseObserved(policyCase, policyCase.RollbackGrammar, "aa");
            Assert.AreEqual(policyCase.SecondObserved, rollbackObservations[^1], policyCase.Name);
            Assert.AreEqual(2, rollbackObservations.Length, policyCase.Name);
        }
    }

    /// <summary>
    /// Parses input with a recording lifecycle executor.
    /// </summary>
    /// <param name="policyCase">Policy case being exercised.</param>
    /// <param name="grammar">Grammar source.</param>
    /// <param name="input">Input text.</param>
    /// <returns>The observed child parameter values.</returns>
    private static string[] ParseObserved(PolicyCase policyCase, string grammar, string input)
    {
        var observed = new List<string>();
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = policyCase.CreatePolicy(),
            RuleInvocationFrameManager = new StackParserRuleInvocationFrameManager(),
            RuleLifecycleExecutor = new RecordingLifecycleExecutor(observed),
        };
        ParseNode result = new CompiledGrammar(Antlr4GrammarConverter.Parse(grammar), policy).Parse(input);
        Assert.IsNotInstanceOfType(result, typeof(ErrorNode), policyCase.Name);
        return observed.ToArray();
    }

    /// <summary>
    /// Creates all policy cases used by the contract matrix.
    /// </summary>
    /// <returns>The supported literal policy cases.</returns>
    private static PolicyCase[] PolicyCases() =>
    [
        PolicyCase.Positional(false),
        PolicyCase.Named(false),
        PolicyCase.Positional(true),
        PolicyCase.Named(true),
    ];

    /// <summary>
    /// Creates direct positional policy metadata.
    /// </summary>
    /// <param name="arguments">Raw positional arguments.</param>
    /// <param name="descriptor">Target descriptor.</param>
    /// <param name="writer">Optional seed writer.</param>
    /// <returns>A rule-call context.</returns>
    private static ParserRuleCallExecutionContext PositionalContext(IReadOnlyList<string> arguments, ParserRuleInvocationDescriptor? descriptor, Func<IReadOnlyDictionary<string, object?>, bool>? writer = null) => new()
    {
        CallerFrame = null,
        RuleName = "child",
        RawArguments = string.Join(", ", arguments),
        PositionalRawArguments = arguments,
        TargetRuleDescriptor = descriptor,
        ParameterSeedWriter = writer,
    };

    /// <summary>
    /// Creates direct named policy metadata.
    /// </summary>
    /// <param name="rawArguments">Raw argument text.</param>
    /// <param name="arguments">Split named arguments.</param>
    /// <param name="descriptor">Target descriptor.</param>
    /// <param name="writer">Optional seed writer.</param>
    /// <returns>A rule-call context.</returns>
    private static ParserRuleCallExecutionContext NamedContext(string rawArguments, IReadOnlyDictionary<string, string>? arguments, ParserRuleInvocationDescriptor? descriptor, Func<IReadOnlyDictionary<string, object?>, bool>? writer = null) => new()
    {
        CallerFrame = null,
        RuleName = "child",
        RawArguments = rawArguments,
        NamedRawArguments = arguments,
        TargetRuleDescriptor = descriptor,
        ParameterSeedWriter = writer,
    };

    /// <summary>
    /// Creates a descriptor for the child rule.
    /// </summary>
    /// <param name="parameters">Parameter descriptors.</param>
    /// <returns>A parser rule descriptor.</returns>
    private static ParserRuleInvocationDescriptor Descriptor(params IEnumerable<ParserRuleParameterDescriptor> parameters) => new()
    {
        RuleName = "child",
        Parameters = parameters.ToArray(),
    };

    /// <summary>
    /// Creates parameter metadata.
    /// </summary>
    /// <param name="name">Parameter name.</param>
    /// <param name="type">Raw type.</param>
    /// <param name="defaultValue">Optional default text.</param>
    /// <returns>A parameter descriptor.</returns>
    private static ParserRuleParameterDescriptor Parameter(string name, string? type, string? defaultValue = null) => new()
    {
        Name = name,
        RawType = type,
        RawDefaultValue = defaultValue,
        RawDeclaration = type is null ? name : $"{type} {name}",
    };

    /// <summary>
    /// Copies submitted values into an ordinal dictionary.
    /// </summary>
    /// <param name="values">Submitted batch.</param>
    /// <returns>A detached copy.</returns>
    private static IReadOnlyDictionary<string, object?> Copy(IReadOnlyDictionary<string, object?> values) => new Dictionary<string, object?>(values, StringComparer.Ordinal);

    /// <summary>
    /// Creates an ordinal named argument dictionary.
    /// </summary>
    /// <param name="entries">Argument entries.</param>
    /// <returns>An ordinal dictionary.</returns>
    private static IReadOnlyDictionary<string, string> NamedArgs(params IEnumerable<(string Name, string Value)> entries) => entries.ToDictionary(static entry => entry.Name, static entry => entry.Value, StringComparer.Ordinal);

    /// <summary>
    /// Records the value parameter from child frames during initialization.
    /// </summary>
    private sealed class RecordingLifecycleExecutor(List<string> observed) : IParserRuleLifecycleExecutor
    {
        /// <summary>
        /// Records present child values as invariant strings.
        /// </summary>
        /// <param name="phase">Lifecycle phase.</param>
        /// <param name="ruleName">Current rule name.</param>
        /// <param name="context">Lifecycle context.</param>
        public void Execute(ParserRuleLifecyclePhase phase, string ruleName, ParserRuleLifecycleContext context)
        {
            if (phase == ParserRuleLifecyclePhase.Init && ruleName == "child" && context.InvocationFrame?.TryGetParameter("value", out object? value) == true)
            {
                observed.Add(FormattableString.Invariant($"{value}:{value?.GetType().Name ?? "null"}"));
            }
        }
    }

    /// <summary>
    /// Describes one concrete literal policy family for common contract tests.
    /// </summary>
    private sealed class PolicyCase
    {
        /// <summary>
        /// Initializes a policy case.
        /// </summary>
        private PolicyCase(string name, bool named, bool typed)
        {
            Name = name;
            IsNamed = named;
            IsTyped = typed;
        }

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets whether named syntax is used.
        /// </summary>
        public bool IsNamed { get; }

        /// <summary>
        /// Gets whether declared type conversion is enforced.
        /// </summary>
        public bool IsTyped { get; }

        /// <summary>
        /// Gets the raw valid arguments.
        /// </summary>
        public string RawArguments => IsNamed ? "second: 2, first: 1" : "1, 2";

        /// <summary>
        /// Gets expected keys for a valid batch.
        /// </summary>
        public string[] ExpectedKeys => ["first", "second"];

        /// <summary>
        /// Gets the observed value for the winning rollback alternative.
        /// </summary>
        public string SecondObserved => IsTyped ? "2:Int32" : "2:Int32";


        /// <summary>
        /// Gets the rollback grammar for this syntax family.
        /// </summary>
        public string RollbackGrammar => IsNamed ? """
            grammar P;
            start : child[value: 1] B | A child[value: 2] ;
            child[int value] : A ;
            A : 'a' ;
            B : 'b' ;
            """ : """
            grammar P;
            start : child[1] B | A child[2] ;
            child[int value] : A ;
            A : 'a' ;
            B : 'b' ;
            """;


        /// <summary>
        /// Creates a positional or named policy case.
        /// </summary>
        public static PolicyCase Positional(bool typed) => new(typed ? "Typed positional" : "Positional", false, typed);

        /// <summary>
        /// Creates a named policy case.
        /// </summary>
        public static PolicyCase Named(bool typed) => new(typed ? "Typed named" : "Named", true, typed);

        /// <summary>
        /// Creates the policy implementation under test.
        /// </summary>
        public IParserRuleCallExecutionPolicy CreatePolicy(ParserRuleCallBindingFailureBehavior behavior = ParserRuleCallBindingFailureBehavior.IgnoreCall) => (IsNamed, IsTyped) switch
        {
            (false, false) => new PositionalLiteralRuleCallExecutionPolicy(behavior),
            (true, false) => new NamedLiteralRuleCallExecutionPolicy(behavior),
            (false, true) => new TypedPositionalLiteralRuleCallExecutionPolicy(behavior),
            (true, true) => new TypedNamedLiteralRuleCallExecutionPolicy(behavior),
        };

        /// <summary>
        /// Creates a valid binding context.
        /// </summary>
        public ParserRuleCallExecutionContext CreateValidContext(Func<IReadOnlyDictionary<string, object?>, bool> writer) => IsNamed
            ? NamedContext(RawArguments, NamedArgs(("second", "2"), ("first", "1")), Descriptor(Parameter("first", "int"), Parameter("second", "byte")), writer)
            : PositionalContext(["1", "2"], Descriptor(Parameter("first", "int"), Parameter("second", "byte")), writer);

        /// <summary>
        /// Creates an invalid literal context.
        /// </summary>
        public ParserRuleCallExecutionContext CreateInvalidLiteralContext(Func<IReadOnlyDictionary<string, object?>, bool> writer) => IsNamed
            ? NamedContext("value: notALiteral", NamedArgs(("value", "notALiteral")), Descriptor(Parameter("value", "int")), writer)
            : PositionalContext(["notALiteral"], Descriptor(Parameter("value", "int")), writer);

        /// <summary>
        /// Creates a context with no target descriptor.
        /// </summary>
        public ParserRuleCallExecutionContext CreateMissingDescriptorContext() => IsNamed
            ? NamedContext("value: 1", NamedArgs(("value", "1")), null)
            : PositionalContext(["1"], null);

        /// <summary>
        /// Creates a context that typed policies complete from defaults while untyped policies reject for incomplete coverage.
        /// </summary>
        public ParserRuleCallExecutionContext CreateDefaultCoverageContext(Func<IReadOnlyDictionary<string, object?>, bool> writer) => IsNamed
            ? NamedContext("first: 1", NamedArgs(("first", "1")), Descriptor(Parameter("first", "int"), Parameter("second", "byte", "2")), writer)
            : PositionalContext(["1"], Descriptor(Parameter("first", "int"), Parameter("second", "byte", "2")), writer);

        /// <summary>
        /// Creates a context accepted by untyped policies but rejected by typed conversion.
        /// </summary>
        public ParserRuleCallExecutionContext CreateInvalidTypedConversionContext(Func<IReadOnlyDictionary<string, object?>, bool> writer) => IsNamed
            ? NamedContext("value: \"text\"", NamedArgs(("value", "\"text\"")), Descriptor(Parameter("value", "int")), writer)
            : PositionalContext(["\"text\""], Descriptor(Parameter("value", "int")), writer);

        /// <summary>
        /// Asserts a valid batch for this policy case.
        /// </summary>
        public void AssertValidBatch(IReadOnlyDictionary<string, object?> batch)
        {
            Assert.AreEqual(1, batch["first"], Name);
            if (IsTyped)
            {
                Assert.AreEqual((byte)2, batch["second"], Name);
                Assert.IsInstanceOfType<byte>(batch["second"], Name);
            }
            else
            {
                Assert.AreEqual(2, batch["second"], Name);
                Assert.IsInstanceOfType<int>(batch["second"], Name);
            }
        }
    }
}
