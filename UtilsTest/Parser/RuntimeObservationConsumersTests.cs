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
    public void RuntimeObservationRecorder_Observations_ReturnsDefensiveSnapshot()
    {
        var recorder = new RuntimeObservationRecorder();
        recorder.OnAlternativeStarted(CreateObservation(ParserRuntimeObservationKind.AlternativeStarted, ParserRuntimeObservationStatus.Active, 0));

        var firstSnapshot = recorder.Observations;
        recorder.OnAlternativeCompleted(CreateObservation(ParserRuntimeObservationKind.AlternativeCompleted, ParserRuntimeObservationStatus.Completed, 0));

        Assert.AreEqual(1, firstSnapshot.Count);
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
