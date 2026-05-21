using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class RuntimeObservationConsumersTests
{
    [TestMethod]
    public void RuntimeObservationRecorder_RecordsDeterministicSequence()
    {
        var recorder = new RuntimeObservationRecorder();

        recorder.OnAlternativeStarted(CreateObservation(ParserRuntimeObservationKind.AlternativeStarted, ParserRuntimeObservationStatus.Active, 0));
        recorder.OnAlternativeCompleted(CreateObservation(ParserRuntimeObservationKind.AlternativeCompleted, ParserRuntimeObservationStatus.Completed, 0));
        recorder.OnAlternativeSelected(CreateObservation(ParserRuntimeObservationKind.AlternativeSelected, ParserRuntimeObservationStatus.Completed, 0));

        Assert.AreEqual(3, recorder.Observations.Count);
    }

    [TestMethod]
    public void RuntimeObservationRecorder_Observations_ReturnsLiveReadOnlyView()
    {
        var recorder = new RuntimeObservationRecorder();
        recorder.OnAlternativeStarted(CreateObservation(ParserRuntimeObservationKind.AlternativeStarted, ParserRuntimeObservationStatus.Active, 0));

        var firstSnapshot = recorder.Observations;
        recorder.OnAlternativeCompleted(CreateObservation(ParserRuntimeObservationKind.AlternativeCompleted, ParserRuntimeObservationStatus.Completed, 0));

        Assert.AreEqual(2, firstSnapshot.Count);
        Assert.AreEqual(2, recorder.Observations.Count);
    }

    [TestMethod]
    public void RuntimeObservationTextWriter_ProducesStableDeterministicOutput()
    {
        var text = RuntimeObservationTextWriter.Write(CreateTraceObservations());
        var expected = "AlternativeStarted status=Active rule=entry alt=0 priority=0 origin=0 current=1\n" +
            "AlternativeCompleted status=Completed rule=entry alt=0 priority=0 origin=0 current=3\n" +
            "AlternativeSelected status=Completed rule=entry alt=0 priority=0 origin=0 current=3";
        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void RuntimeObservationJsonWriter_ProducesStableDeterministicOutput()
    {
        var json = RuntimeObservationJsonWriter.Write(CreateTraceObservations());
        var expected = "[{\"Kind\":\"AlternativeStarted\",\"Status\":\"Active\",\"Rule\":\"entry\",\"Alternative\":0,\"CurrentInputPosition\":1,\"OriginInputPosition\":0,\"Priority\":0},{\"Kind\":\"AlternativeCompleted\",\"Status\":\"Completed\",\"Rule\":\"entry\",\"Alternative\":0,\"CurrentInputPosition\":3,\"OriginInputPosition\":0,\"Priority\":0},{\"Kind\":\"AlternativeSelected\",\"Status\":\"Completed\",\"Rule\":\"entry\",\"Alternative\":0,\"CurrentInputPosition\":3,\"OriginInputPosition\":0,\"Priority\":0}]";
        Assert.AreEqual(expected, json);
    }

    [TestMethod]
    public void RuntimeObservationConsumers_AreConsumerReplaceableAcrossRuns()
    {
        var first = RuntimeObservationTextWriter.Write(CreateTraceObservations());
        var second = RuntimeObservationTextWriter.Write(CreateTraceObservations());
        Assert.AreEqual(first, second);
    }



    [TestMethod]
    public void RuntimeObservationJsonWriter_UsesStableFieldNamesAndDistinctKindStatus()
    {
        var json = RuntimeObservationJsonWriter.Write(CreateTraceObservations());
        using var document = JsonDocument.Parse(json);
        var first = document.RootElement[0];

        Assert.IsTrue(first.TryGetProperty("Kind", out var kind));
        Assert.IsTrue(first.TryGetProperty("Status", out var status));
        Assert.IsTrue(first.TryGetProperty("Rule", out _));
        Assert.IsTrue(first.TryGetProperty("Alternative", out _));
        Assert.IsTrue(first.TryGetProperty("CurrentInputPosition", out _));
        Assert.IsTrue(first.TryGetProperty("OriginInputPosition", out _));
        Assert.IsTrue(first.TryGetProperty("Priority", out _));
        Assert.AreEqual("AlternativeStarted", kind.GetString());
        Assert.AreEqual("Active", status.GetString());
    }

    [TestMethod]
    public void RuntimeObservationExports_DoNotExposeActiveParseStateInternals()
    {
        var observations = CreateTraceObservations();

        var text = RuntimeObservationTextWriter.Write(observations);
        var json = RuntimeObservationJsonWriter.Write(observations);

        Assert.IsFalse(text.Contains("ActiveParseState", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("StateKey", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("ActiveParseState", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("StateKey", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RuntimeObservationExports_AreDeterministicAcrossEquivalentParserRuns()
    {
        var first = RunObservedParse("a");
        var second = RunObservedParse("a");

        Assert.AreEqual(first.TextTrace, second.TextTrace);
        Assert.AreEqual(first.JsonTrace, second.JsonTrace);
    }

    [TestMethod]
    public void RuntimeObservationRecorder_WorksWithRealParserPipeline()
    {
        const string grammar = """
            grammar Sample;
            start : A ;
            A : 'a' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;
        var definition = Antlr4GrammarConverter.Parse(grammar);
        var recorder = new RuntimeObservationRecorder();
        var parser = new ParserEngine(
            definition,
            ParserRuntimeFeaturePolicy.Default with
            {
                RuntimeObserver = recorder
            });
        var diagnostics = new DiagnosticBag();
        var tokens = new CompiledGrammar(definition).Tokenize("a");

        var parseResult = parser.Parse(tokens, diagnostics: diagnostics);
        var textTrace = RuntimeObservationTextWriter.Write(recorder.Observations);
        var jsonTrace = RuntimeObservationJsonWriter.Write(recorder.Observations);

        Assert.IsFalse(parseResult is ErrorNode);
        Assert.IsTrue(recorder.Observations.Count > 0);
        Assert.IsTrue(textTrace.Length > 0);
        Assert.IsTrue(jsonTrace.StartsWith("[", StringComparison.Ordinal));
    }



    private static (string TextTrace, string JsonTrace) RunObservedParse(string input)
    {
        const string grammar = """
            grammar Sample;
            start : A ;
            A : 'a' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var definition = Antlr4GrammarConverter.Parse(grammar);
        var recorder = new RuntimeObservationRecorder();
        var parser = new ParserEngine(
            definition,
            ParserRuntimeFeaturePolicy.Default with
            {
                RuntimeObserver = recorder
            });
        var diagnostics = new DiagnosticBag();
        var tokens = new CompiledGrammar(definition).Tokenize(input);

        _ = parser.Parse(tokens, diagnostics: diagnostics);

        return (
            RuntimeObservationTextWriter.Write(recorder.Observations),
            RuntimeObservationJsonWriter.Write(recorder.Observations));
    }

    private static AlternativeRuntimeObservation[] CreateTraceObservations()
    {
        return
        [
            CreateObservation(ParserRuntimeObservationKind.AlternativeStarted, ParserRuntimeObservationStatus.Active, 0, 1),
            CreateObservation(ParserRuntimeObservationKind.AlternativeCompleted, ParserRuntimeObservationStatus.Completed, 0, 3),
            CreateObservation(ParserRuntimeObservationKind.AlternativeSelected, ParserRuntimeObservationStatus.Completed, 0, 3)
        ];
    }

    private static AlternativeRuntimeObservation CreateObservation(ParserRuntimeObservationKind kind, ParserRuntimeObservationStatus status, int alternativeIndex, int currentInputPosition = 0)
    {
        return new AlternativeRuntimeObservation(kind, "entry", alternativeIndex, 0, 0, currentInputPosition, status);
    }
}
