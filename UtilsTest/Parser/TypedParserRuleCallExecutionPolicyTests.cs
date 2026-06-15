using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies explicitly opt-in typed positional and named literal rule-call binding.
/// </summary>
[TestClass]
public class TypedParserRuleCallExecutionPolicyTests
{
    /// <summary>
    /// Verifies neither typed policy is installed by the conservative runtime default.
    /// </summary>
    [TestMethod]
    public void TypedPolicies_AreNotDefault()
    {
        Assert.IsNotInstanceOfType(ParserRuntimeFeaturePolicy.Default.RuleCallExecutionPolicy, typeof(TypedPositionalLiteralRuleCallExecutionPolicy));
        Assert.IsNotInstanceOfType(ParserRuntimeFeaturePolicy.Default.RuleCallExecutionPolicy, typeof(TypedNamedLiteralRuleCallExecutionPolicy));
    }

    /// <summary>
    /// Verifies positional binding converts exact, widening, narrowing, nullable, string, and character values in one batch.
    /// </summary>
    [TestMethod]
    public void TypedPositionalPolicy_ValidConversions_SubmitOneCompleteBatch()
    {
        IReadOnlyDictionary<string, object?>? batch = null;
        int attempts = 0;
        ParserRuleCallExecutionContext context = PositionalContext(
            ["42", "42", "null", "'x'", "\"y\""],
            [("exact", "int"), ("narrow", "byte"), ("nullable", "long?"), ("text", "string"), ("character", "char")],
            values =>
            {
                attempts++;
                batch = new Dictionary<string, object?>(values, StringComparer.Ordinal);
                return true;
            });

        new TypedPositionalLiteralRuleCallExecutionPolicy().BeforeRuleCall(context);

        Assert.AreEqual(1, attempts);
        Assert.AreEqual(5, batch!.Count);
        Assert.AreEqual(42, batch["exact"]);
        Assert.AreEqual((byte)42, batch["narrow"]);
        Assert.IsTrue(batch.ContainsKey("nullable"));
        Assert.IsNull(batch["nullable"]);
        Assert.AreEqual("x", batch["text"]);
        Assert.AreEqual('y', batch["character"]);
    }

    /// <summary>
    /// Verifies any positional conversion failure rejects the whole call before invoking the batch writer.
    /// </summary>
    [TestMethod]
    public void TypedPositionalPolicy_IncompatibleOrOverflowingValue_IsAtomic()
    {
        int attempts = 0;
        var policy = new TypedPositionalLiteralRuleCallExecutionPolicy();

        policy.BeforeRuleCall(PositionalContext(["42", "\"bad\""], [("first", "byte"), ("second", "int")], Reject));
        policy.BeforeRuleCall(PositionalContext(["42", "300"], [("first", "byte"), ("second", "byte")], Reject));
        policy.BeforeRuleCall(PositionalContext(["null"], [("value", "int")], Reject));
        policy.BeforeRuleCall(PositionalContext(["1"], [("value", "MyType")], Reject));

        Assert.AreEqual(0, attempts);
        return;

        bool Reject(IReadOnlyDictionary<string, object?> values)
        {
            attempts++;
            return false;
        }
    }

    /// <summary>
    /// Verifies strict positional failures expose rule, index, parameter, and declared type metadata.
    /// </summary>
    [TestMethod]
    public void TypedPositionalPolicy_ThrowMode_ReportsTypedMetadata()
    {
        var policy = new TypedPositionalLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw);
        ParserRuleCallBindingException exception = Assert.ThrowsException<ParserRuleCallBindingException>(() =>
            policy.BeforeRuleCall(PositionalContext(["\"hello\""], [("value", "int")])));

        Assert.AreEqual("child", exception.RuleName);
        Assert.AreEqual(0, exception.ArgumentIndex);
        Assert.AreEqual("value", exception.ParameterName);
        Assert.AreEqual("int", exception.DeclaredType);
        StringAssert.Contains(exception.Message, "String");
    }

    /// <summary>
    /// Verifies the existing positional policy remains untyped while the new policy rejects the same mismatch.
    /// </summary>
    [TestMethod]
    public void PositionalPolicies_UntypedCompatibilityAndTypedEnforcement_RemainDistinct()
    {
        IReadOnlyDictionary<string, object?>? untypedBatch = null;
        IReadOnlyDictionary<string, object?>? typedBatch = null;
        ParserRuleCallExecutionContext untypedContext = PositionalContext(
            ["\"hello\""],
            [("value", "int")],
            values => { untypedBatch = values; return true; });
        ParserRuleCallExecutionContext typedContext = PositionalContext(
            ["\"hello\""],
            [("value", "int")],
            values => { typedBatch = values; return true; });

        new PositionalLiteralRuleCallExecutionPolicy().BeforeRuleCall(untypedContext);
        new TypedPositionalLiteralRuleCallExecutionPolicy().BeforeRuleCall(typedContext);

        Assert.AreEqual("hello", untypedBatch!["value"]);
        Assert.IsNull(typedBatch);
    }

    /// <summary>
    /// Verifies named binding is order-independent for both colon and equals syntax after conversion.
    /// </summary>
    [TestMethod]
    public void TypedNamedPolicy_ColonAndEqualsSyntax_ConvertsByName()
    {
        IReadOnlyDictionary<string, object?>? batch = null;
        ParserRuleCallExecutionContext context = NamedContext(
            "second = 42, first: 1",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["second"] = "42", ["first"] = "1" },
            [("first", "double"), ("second", "byte")],
            values => { batch = new Dictionary<string, object?>(values, StringComparer.Ordinal); return true; });

        new TypedNamedLiteralRuleCallExecutionPolicy().BeforeRuleCall(context);

        Assert.AreEqual(1d, batch!["first"]);
        Assert.AreEqual((byte)42, batch["second"]);
    }

    /// <summary>
    /// Verifies named conversion, coverage, nullability, and unsupported-type failures occur before mutation.
    /// </summary>
    [TestMethod]
    public void TypedNamedPolicy_InvalidCalls_AreAtomic()
    {
        int attempts = 0;
        var policy = new TypedNamedLiteralRuleCallExecutionPolicy();

        policy.BeforeRuleCall(NamedContext("first: 1, second: \"bad\"", Named(("first", "1"), ("second", "\"bad\"")), [("first", "byte"), ("second", "int")], Reject));
        policy.BeforeRuleCall(NamedContext("value: null", Named(("value", "null")), [("value", "int")], Reject));
        policy.BeforeRuleCall(NamedContext("value: 1", Named(("value", "1")), [("value", "SomeEnum")], Reject));
        policy.BeforeRuleCall(NamedContext("other: 1", Named(("other", "1")), [("value", "int")], Reject));

        Assert.AreEqual(0, attempts);
        return;

        bool Reject(IReadOnlyDictionary<string, object?> values)
        {
            attempts++;
            return false;
        }
    }

    /// <summary>
    /// Verifies the existing named policy remains untyped while the new policy enforces declared types.
    /// </summary>
    [TestMethod]
    public void NamedPolicies_UntypedCompatibilityAndTypedEnforcement_RemainDistinct()
    {
        IReadOnlyDictionary<string, object?>? untypedBatch = null;
        IReadOnlyDictionary<string, object?>? typedBatch = null;
        IReadOnlyDictionary<string, string> arguments = Named(("value", "\"hello\""));

        new NamedLiteralRuleCallExecutionPolicy().BeforeRuleCall(NamedContext(
            "value: \"hello\"", arguments, [("value", "int")], values => { untypedBatch = values; return true; }));
        new TypedNamedLiteralRuleCallExecutionPolicy().BeforeRuleCall(NamedContext(
            "value: \"hello\"", arguments, [("value", "int")], values => { typedBatch = values; return true; }));

        Assert.AreEqual("hello", untypedBatch!["value"]);
        Assert.IsNull(typedBatch);
    }

    /// <summary>
    /// Verifies manager rejection observes one complete converted batch and cannot leave partial state.
    /// </summary>
    [TestMethod]
    public void TypedPolicies_ManagerRejection_ReceivesOneCompleteBatch()
    {
        int attempts = 0;
        IReadOnlyDictionary<string, object?>? lastBatch = null;
        bool Reject(IReadOnlyDictionary<string, object?> values)
        {
            attempts++;
            lastBatch = new Dictionary<string, object?>(values, StringComparer.Ordinal);
            return false;
        }

        new TypedPositionalLiteralRuleCallExecutionPolicy().BeforeRuleCall(
            PositionalContext(["1", "2"], [("first", "byte"), ("second", "long")], Reject));

        Assert.AreEqual(1, attempts);
        Assert.AreEqual((byte)1, lastBatch!["first"]);
        Assert.AreEqual(2L, lastBatch["second"]);
    }

    /// <summary>
    /// Verifies nullable null is present as a seed rather than treated as an absent parameter.
    /// </summary>
    [TestMethod]
    public void TypedPositionalPolicy_NullableNull_IsPresentAndNull()
    {
        const string grammar = """
            grammar P;
            start : child[null] ;
            child[int? value] : A ;
            A : 'a' ;
            """;
        var observed = new List<object?>();

        ParseNode result = Compile(grammar, new TypedPositionalLiteralRuleCallExecutionPolicy(), observed).Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, observed.Count);
        Assert.IsNull(observed[0]);
    }

    /// <summary>
    /// Verifies positional omission is trailing-only, explicit values override defaults, and all values use one batch.
    /// </summary>
    [TestMethod]
    public void TypedPositionalPolicy_OmittedTrailingDefaultsAndOverrides_SubmitOneCompleteBatch()
    {
        IReadOnlyDictionary<string, object?>? defaultBatch = null;
        IReadOnlyDictionary<string, object?>? overrideBatch = null;
        ParserRuleInvocationDescriptor descriptor = Descriptor(
            Parameter("first", "int"),
            Parameter("second", "byte", "2"),
            Parameter("text", "string", "'x'"));

        new TypedPositionalLiteralRuleCallExecutionPolicy().BeforeRuleCall(
            PositionalContext(["1"], descriptor, values => { defaultBatch = Copy(values); return true; }));
        new TypedPositionalLiteralRuleCallExecutionPolicy().BeforeRuleCall(
            PositionalContext(["1", "7", "\"explicit\""], descriptor, values => { overrideBatch = Copy(values); return true; }));

        Assert.AreEqual(1, defaultBatch!["first"]);
        Assert.AreEqual((byte)2, defaultBatch["second"]);
        Assert.AreEqual("x", defaultBatch["text"]);
        Assert.AreEqual((byte)7, overrideBatch!["second"]);
        Assert.AreEqual("explicit", overrideBatch["text"]);
    }

    /// <summary>
    /// Verifies required, malformed, unsupported, incompatible, overflowing, and empty positional values fail atomically.
    /// </summary>
    [TestMethod]
    public void TypedPositionalPolicy_DefaultFailures_AreAtomic()
    {
        int attempts = 0;
        var policy = new TypedPositionalLiteralRuleCallExecutionPolicy();
        bool Write(IReadOnlyDictionary<string, object?> values) { attempts++; return true; }

        policy.BeforeRuleCall(PositionalContext([], Descriptor(Parameter("value", "int")), Write));
        policy.BeforeRuleCall(PositionalContext([""], Descriptor(Parameter("value", "int", "42")), Write));
        policy.BeforeRuleCall(PositionalContext([], Descriptor(Parameter("value", "int", "otherParameter")), Write));
        policy.BeforeRuleCall(PositionalContext([], Descriptor(Parameter("value", "int", "\"42\"")), Write));
        policy.BeforeRuleCall(PositionalContext([], Descriptor(Parameter("value", "byte", "300")), Write));
        policy.BeforeRuleCall(PositionalContext(["1", "2"], Descriptor(Parameter("value", "int", "3")), Write));
        policy.BeforeRuleCall(PositionalContext(["1"], Descriptor(Parameter("first", "int", "9"), Parameter("second", "int")), Write));

        Assert.AreEqual(0, attempts);
    }

    /// <summary>
    /// Verifies invalid defaults are skipped when explicit positional values cover their parameters.
    /// </summary>
    [TestMethod]
    public void TypedPositionalPolicy_ExplicitValue_SkipsInvalidDefault()
    {
        IReadOnlyDictionary<string, object?>? batch = null;

        new TypedPositionalLiteralRuleCallExecutionPolicy().BeforeRuleCall(PositionalContext(
            ["7"],
            Descriptor(Parameter("value", "int", "invalidExpression")),
            values => { batch = Copy(values); return true; }));

        Assert.AreEqual(7, batch!["value"]);
    }

    /// <summary>
    /// Verifies positional defaults preserve nullable null and checked numeric target conversion.
    /// </summary>
    [TestMethod]
    public void TypedPositionalPolicy_DefaultConversions_PreserveNullAndRuntimeTypes()
    {
        IReadOnlyDictionary<string, object?>? batch = null;

        new TypedPositionalLiteralRuleCallExecutionPolicy().BeforeRuleCall(PositionalContext(
            [],
            Descriptor(Parameter("nullable", "int?", "null"), Parameter("small", "byte", "42")),
            values => { batch = Copy(values); return true; }));

        Assert.IsTrue(batch!.ContainsKey("nullable"));
        Assert.IsNull(batch["nullable"]);
        Assert.IsInstanceOfType<byte>(batch["small"]);
        Assert.AreEqual((byte)42, batch["small"]);
    }

    /// <summary>
    /// Verifies named omission can occur in declaration order independently while required names still fail.
    /// </summary>
    [TestMethod]
    public void TypedNamedPolicy_OmittedDefaultsAndExplicitOverrides_SubmitOneCompleteBatch()
    {
        ParserRuleInvocationDescriptor descriptor = Descriptor(
            Parameter("first", "int", "1"),
            Parameter("required", "long"),
            Parameter("text", "string", "\"default\""));
        IReadOnlyDictionary<string, object?>? batch = null;
        int failedAttempts = 0;

        new TypedNamedLiteralRuleCallExecutionPolicy().BeforeRuleCall(NamedContext(
            "text: \"explicit\", required: 6",
            Named(("text", "\"explicit\""), ("required", "6")),
            descriptor,
            values => { batch = Copy(values); return true; }));
        new TypedNamedLiteralRuleCallExecutionPolicy().BeforeRuleCall(NamedContext(
            "text: \"x\"",
            Named(("text", "\"x\"")),
            descriptor,
            values => { failedAttempts++; return true; }));

        Assert.AreEqual(1, batch!["first"]);
        Assert.AreEqual(6L, batch["required"]);
        Assert.AreEqual("explicit", batch["text"]);
        Assert.AreEqual(0, failedAttempts);
    }

    /// <summary>
    /// Verifies invalid named defaults are required only for omitted parameters and nullable defaults remain present.
    /// </summary>
    [TestMethod]
    public void TypedNamedPolicy_DefaultValidation_IsLazyAndAtomic()
    {
        int invalidAttempts = 0;
        IReadOnlyDictionary<string, object?>? overrideBatch = null;
        IReadOnlyDictionary<string, object?>? nullBatch = null;
        ParserRuleInvocationDescriptor invalidDescriptor = Descriptor(Parameter("value", "int", "invalidExpression"));
        var policy = new TypedNamedLiteralRuleCallExecutionPolicy();

        policy.BeforeRuleCall(NamedContext("", Named(), invalidDescriptor, values => { invalidAttempts++; return true; }));
        policy.BeforeRuleCall(NamedContext("value: 7", Named(("value", "7")), invalidDescriptor,
            values => { overrideBatch = Copy(values); return true; }));
        policy.BeforeRuleCall(NamedContext("", Named(), Descriptor(Parameter("value", "int?", "null")),
            values => { nullBatch = Copy(values); return true; }));

        Assert.AreEqual(0, invalidAttempts);
        Assert.AreEqual(7, overrideBatch!["value"]);
        Assert.IsTrue(nullBatch!.ContainsKey("value"));
        Assert.IsNull(nullBatch["value"]);
    }

    /// <summary>
    /// Verifies default-related throw failures report deterministic parameter metadata without an argument index.
    /// </summary>
    [TestMethod]
    public void TypedPolicies_DefaultThrowMode_ReportsOmittedParameterMetadata()
    {
        var positional = new TypedPositionalLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw);
        ParserRuleCallBindingException positionalException = Assert.ThrowsException<ParserRuleCallBindingException>(() =>
            positional.BeforeRuleCall(PositionalContext([], Descriptor(Parameter("value", "byte", "300")))));
        var named = new TypedNamedLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw);
        ParserRuleCallBindingException namedException = Assert.ThrowsException<ParserRuleCallBindingException>(() =>
            named.BeforeRuleCall(NamedContext("", Named(), Descriptor(Parameter("value", "int")))));

        Assert.IsNull(positionalException.ArgumentIndex);
        Assert.AreEqual("value", positionalException.ParameterName);
        Assert.AreEqual("byte", positionalException.DeclaredType);
        StringAssert.Contains(positionalException.Message, "Default value");
        Assert.IsNull(namedException.ArgumentIndex);
        Assert.AreEqual("value", namedException.ParameterName);
        StringAssert.Contains(namedException.Message, "no explicit argument or default value");
    }

    /// <summary>
    /// Verifies legacy untyped policies retain exact coverage and do not consume descriptor defaults.
    /// </summary>
    [TestMethod]
    public void UntypedPolicies_DefaultMetadata_DoesNotChangeExactCoverage()
    {
        int attempts = 0;
        ParserRuleInvocationDescriptor descriptor = Descriptor(Parameter("value", "int", "42"));
        bool Write(IReadOnlyDictionary<string, object?> values) { attempts++; return true; }

        new PositionalLiteralRuleCallExecutionPolicy().BeforeRuleCall(PositionalContext([], descriptor, Write));
        new NamedLiteralRuleCallExecutionPolicy().BeforeRuleCall(NamedContext("", Named(), descriptor, Write));

        Assert.AreEqual(0, attempts);
    }

    /// <summary>
    /// Verifies after-call callbacks are no-ops apart from null validation and constructors reject undefined behavior values.
    /// </summary>
    [TestMethod]
    public void TypedPolicies_AfterCallAndFailureBehavior_AreConservative()
    {
        ParserRuleCallExecutionContext context = PositionalContext([], []);
        new TypedPositionalLiteralRuleCallExecutionPolicy().AfterRuleCall(context);
        new TypedNamedLiteralRuleCallExecutionPolicy().AfterRuleCall(context);

        Assert.ThrowsException<ArgumentNullException>(() => new TypedPositionalLiteralRuleCallExecutionPolicy().AfterRuleCall(null!));
        Assert.ThrowsException<ArgumentNullException>(() => new TypedNamedLiteralRuleCallExecutionPolicy().AfterRuleCall(null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TypedPositionalLiteralRuleCallExecutionPolicy((ParserRuleCallBindingFailureBehavior)int.MaxValue));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TypedNamedLiteralRuleCallExecutionPolicy((ParserRuleCallBindingFailureBehavior)int.MaxValue));
    }

    /// <summary>
    /// Creates direct positional policy metadata with an optional atomic batch writer.
    /// </summary>
    /// <param name="arguments">Raw positional arguments.</param>
    /// <param name="parameters">Parameter name and raw type pairs.</param>
    /// <param name="writer">Optional atomic seed writer.</param>
    /// <returns>A direct rule-call context.</returns>
    private static ParserRuleCallExecutionContext PositionalContext(
        IReadOnlyList<string> arguments,
        IReadOnlyList<(string Name, string Type)> parameters,
        Func<IReadOnlyDictionary<string, object?>, bool>? writer = null)
    {
        return new ParserRuleCallExecutionContext
        {
            CallerFrame = null,
            RuleName = "child",
            RawArguments = string.Join(", ", arguments),
            PositionalRawArguments = arguments,
            TargetRuleDescriptor = Descriptor(parameters),
            ParameterSeedWriter = writer,
        };
    }

    /// <summary>
    /// Creates direct positional policy metadata from a prepared target descriptor.
    /// </summary>
    /// <param name="arguments">Raw positional arguments.</param>
    /// <param name="descriptor">Prepared target descriptor.</param>
    /// <param name="writer">Optional atomic seed writer.</param>
    /// <returns>A direct rule-call context.</returns>
    private static ParserRuleCallExecutionContext PositionalContext(
        IReadOnlyList<string> arguments,
        ParserRuleInvocationDescriptor descriptor,
        Func<IReadOnlyDictionary<string, object?>, bool>? writer = null)
    {
        return new ParserRuleCallExecutionContext
        {
            CallerFrame = null,
            RuleName = "child",
            RawArguments = string.Join(", ", arguments),
            PositionalRawArguments = arguments,
            TargetRuleDescriptor = descriptor,
            ParameterSeedWriter = writer,
        };
    }

    /// <summary>
    /// Creates direct named policy metadata with an optional atomic batch writer.
    /// </summary>
    /// <param name="rawArguments">Raw named arguments.</param>
    /// <param name="arguments">Split named arguments.</param>
    /// <param name="parameters">Parameter name and raw type pairs.</param>
    /// <param name="writer">Optional atomic seed writer.</param>
    /// <returns>A direct rule-call context.</returns>
    private static ParserRuleCallExecutionContext NamedContext(
        string rawArguments,
        IReadOnlyDictionary<string, string> arguments,
        IReadOnlyList<(string Name, string Type)> parameters,
        Func<IReadOnlyDictionary<string, object?>, bool>? writer = null)
    {
        return new ParserRuleCallExecutionContext
        {
            CallerFrame = null,
            RuleName = "child",
            RawArguments = rawArguments,
            NamedRawArguments = arguments,
            TargetRuleDescriptor = Descriptor(parameters),
            ParameterSeedWriter = writer,
        };
    }

    /// <summary>
    /// Creates direct named policy metadata from a prepared target descriptor.
    /// </summary>
    /// <param name="rawArguments">Raw named arguments.</param>
    /// <param name="arguments">Split named arguments.</param>
    /// <param name="descriptor">Prepared target descriptor.</param>
    /// <param name="writer">Optional atomic seed writer.</param>
    /// <returns>A direct rule-call context.</returns>
    private static ParserRuleCallExecutionContext NamedContext(
        string rawArguments,
        IReadOnlyDictionary<string, string> arguments,
        ParserRuleInvocationDescriptor descriptor,
        Func<IReadOnlyDictionary<string, object?>, bool>? writer = null)
    {
        return new ParserRuleCallExecutionContext
        {
            CallerFrame = null,
            RuleName = "child",
            RawArguments = rawArguments,
            NamedRawArguments = arguments,
            TargetRuleDescriptor = descriptor,
            ParameterSeedWriter = writer,
        };
    }

    /// <summary>
    /// Creates a target rule descriptor from explicit parameter metadata.
    /// </summary>
    /// <param name="parameters">Parameter name and raw type pairs.</param>
    /// <returns>A target descriptor.</returns>
    private static ParserRuleInvocationDescriptor Descriptor(IReadOnlyList<(string Name, string Type)> parameters)
    {
        return new ParserRuleInvocationDescriptor
        {
            RuleName = "child",
            Parameters = parameters.Select(static parameter => new ParserRuleParameterDescriptor
            {
                Name = parameter.Name,
                RawType = parameter.Type,
                RawDeclaration = $"{parameter.Type} {parameter.Name}",
            }).ToArray(),
        };
    }

    /// <summary>
    /// Creates a target rule descriptor from prepared parameter descriptors.
    /// </summary>
    /// <param name="parameters">Prepared parameter descriptors.</param>
    /// <returns>A target descriptor.</returns>
    private static ParserRuleInvocationDescriptor Descriptor(params IEnumerable<ParserRuleParameterDescriptor> parameters)
    {
        return new ParserRuleInvocationDescriptor
        {
            RuleName = "child",
            Parameters = parameters.ToArray(),
        };
    }

    /// <summary>
    /// Creates passive typed parameter metadata with an optional raw default.
    /// </summary>
    /// <param name="name">Parameter name.</param>
    /// <param name="type">Raw declared type.</param>
    /// <param name="defaultValue">Optional raw default text.</param>
    /// <returns>A parameter descriptor.</returns>
    private static ParserRuleParameterDescriptor Parameter(string name, string type, string? defaultValue = null)
    {
        return new ParserRuleParameterDescriptor
        {
            Name = name,
            RawType = type,
            RawDefaultValue = defaultValue,
            RawDeclaration = defaultValue is null ? $"{type} {name}" : $"{type} {name} = {defaultValue}",
        };
    }

    /// <summary>
    /// Copies one submitted seed batch for assertions after policy execution.
    /// </summary>
    /// <param name="values">Submitted values.</param>
    /// <returns>An ordinal copy.</returns>
    private static IReadOnlyDictionary<string, object?> Copy(IReadOnlyDictionary<string, object?> values)
        => new Dictionary<string, object?>(values, StringComparer.Ordinal);

    /// <summary>
    /// Creates an ordinal named argument dictionary.
    /// </summary>
    /// <param name="entries">Name and raw value pairs.</param>
    /// <returns>An ordinal dictionary.</returns>
    private static IReadOnlyDictionary<string, string> Named(params IEnumerable<(string Name, string Value)> entries)
        => entries.ToDictionary(static entry => entry.Name, static entry => entry.Value, StringComparer.Ordinal);

    /// <summary>
    /// Compiles a grammar with managed frames and records every present child value during initialization.
    /// </summary>
    /// <param name="grammar">ANTLR grammar source.</param>
    /// <param name="callPolicy">Explicit typed call policy.</param>
    /// <param name="observed">Value sink.</param>
    /// <returns>A compiled grammar.</returns>
    private static CompiledGrammar Compile(string grammar, IParserRuleCallExecutionPolicy callPolicy, List<object?> observed)
    {
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = callPolicy,
            RuleInvocationFrameManager = new StackParserRuleInvocationFrameManager(),
            RuleLifecycleExecutor = new ValueRecordingLifecycleExecutor(observed),
        };
        return new CompiledGrammar(Antlr4GrammarConverter.Parse(grammar), policy);
    }

    /// <summary>
    /// Records the child value parameter whenever it is present during initialization.
    /// </summary>
    private sealed class ValueRecordingLifecycleExecutor(List<object?> observed) : IParserRuleLifecycleExecutor
    {
        /// <summary>
        /// Records a present child value, including an explicitly seeded null.
        /// </summary>
        /// <param name="phase">Lifecycle phase.</param>
        /// <param name="ruleName">Current rule name.</param>
        /// <param name="context">Lifecycle context.</param>
        public void Execute(ParserRuleLifecyclePhase phase, string ruleName, ParserRuleLifecycleContext context)
        {
            if (phase == ParserRuleLifecyclePhase.Init
                && ruleName == "child"
                && context.InvocationFrame is not null
                && context.InvocationFrame.TryGetParameter("value", out object? value))
            {
                observed.Add(value);
            }
        }
    }
}
