using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Tests for <see cref="QueryOData"/> covering items 13, 39, 40, 41, 44
/// of the Utils.OData audit (empty results, cancellation semantics, disposal,
/// finalizer removal, and non-object row rejection).
/// </summary>
[TestClass]
public class QueryODataBehaviourTests
{
    // -----------------------------------------------------------------------
    // Helpers — build in-memory JSON streams
    // -----------------------------------------------------------------------

    private static Stream MakeJsonStream(string json)
        => new MemoryStream(Encoding.UTF8.GetBytes(json));

    private static Stream EmptyODataStream()
        => MakeJsonStream("""{"value":[]}""");

    private static Stream SingleRowODataStream()
        => MakeJsonStream("""{"value":[{"Id":1,"Name":"Widget"}]}""");

    private static Stream NonObjectRowStream()
        => MakeJsonStream("""{"value":[42]}""");

    private static Stream NullRowStream()
        => MakeJsonStream("""{"value":[null]}""");

    private static Stream ArrayRowStream()
        => MakeJsonStream("""{"value":[[1,2,3]]}""");

    // -----------------------------------------------------------------------
    // Item 13 — Empty result is not an error (P2)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ConvertDatas_EmptyValueArray_ReturnsSuccessWithEmptyArray()
    {
        // Access ConvertDatas via reflection since it is private
        var method = typeof(QueryOData).GetMethod(
            "ConvertDatas",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method, "ConvertDatas helper must exist");

        using var stream = EmptyODataStream();
        var result = method.Invoke(null, [stream, true /* allowEmpty */]);

        Assert.IsNotNull(result, "Result must not be null");

        // ReturnValue<(JsonArray?, Dictionary?)> — check IsError via reflection
        var resultType = result.GetType();
        var isErrorProp = resultType.GetProperty("IsError");
        Assert.IsNotNull(isErrorProp);
        Assert.IsFalse((bool)isErrorProp.GetValue(result)!,
            "An empty response must be a success, not an error");
    }

    [TestMethod]
    public void ConvertDatas_EmptyValueArray_AllowEmptyFalse_ReturnsError()
    {
        var method = typeof(QueryOData).GetMethod(
            "ConvertDatas",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method);

        using var stream = EmptyODataStream();
        var result = method.Invoke(null, [stream, false /* allowEmpty */]);
        Assert.IsNotNull(result);

        var isErrorProp = result.GetType().GetProperty("IsError");
        Assert.IsNotNull(isErrorProp);
        Assert.IsTrue((bool)isErrorProp.GetValue(result)!,
            "Empty response with allowEmpty=false must return an error");
    }

    // -----------------------------------------------------------------------
    // Item 39 — Cancellation propagates through the channel (P1)
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task Channel_WhenCompletedWithOce_IsNotSuccess()
    {
        // Verifies that a channel completed with an OperationCanceledException surfaces the
        // cancellation rather than completing successfully — so callers can distinguish EOF
        // from cancellation.
        var channel = Channel.CreateBounded<object?[]>(10);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        channel.Writer.TryComplete(new OperationCanceledException(cts.Token));

        bool caughtOce = false;
        try
        {
            await channel.Reader.Completion;
        }
        catch (OperationCanceledException)
        {
            caughtOce = true;
        }

        Assert.IsTrue(caughtOce || channel.Reader.Completion.IsCanceled || channel.Reader.Completion.IsFaulted,
            "Completion must not succeed silently when the channel was completed with an OCE");
        Assert.IsFalse(channel.Reader.Completion.IsCompletedSuccessfully,
            "Completion must not be IsCompletedSuccessfully when cancelled");
    }

    // -----------------------------------------------------------------------
    // Item 40 — Dispose swallows only cancellation, not arbitrary errors (P1)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ODataStreamingDataReader_Dispose_DoesNotThrow_WhenBackgroundTaskCancelled()
    {
        var colDefs = BuildColumnDefinitions();
        var channel = Channel.CreateBounded<object?[]>(1);
        using var cts = new CancellationTokenSource();

        // Background task that completes immediately after cancellation
        var tcs = new TaskCompletionSource();
        cts.Cancel();
        tcs.SetCanceled(cts.Token);
        var backgroundTask = tcs.Task;

        var reader = CreateReaderViaReflection(colDefs, channel.Reader, cts, backgroundTask);
        // Must not throw even though the background task is cancelled
        reader.Dispose();
    }

    // -----------------------------------------------------------------------
    // Item 41 — No finalizer on ODataStreamingDataReader (P1)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ODataStreamingDataReader_HasNoFinalizer()
    {
        // The type must not declare a destructor; GC.SuppressFinalize is pointless without one.
        var readerType = typeof(QueryOData).GetNestedType(
            "ODataStreamingDataReader",
            System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(readerType, "ODataStreamingDataReader nested type must exist");

        // Finalizers compile to a method named 'Finalize'.
        // Use DeclaredOnly to exclude the inherited System.Object.Finalize.
        var finalize = readerType.GetMethod(
            "Finalize",
            System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.IsNull(finalize, "ODataStreamingDataReader must not declare a finalizer");
    }

    // -----------------------------------------------------------------------
    // Item 44 — Non-object entries in value array must throw (P1)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void WriteBatchAsync_ScalarEntry_ThrowsInvalidOperationException()
    {
        var method = typeof(QueryOData).GetMethod(
            "WriteBatchAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method, "WriteBatchAsync helper must exist");

        var batch = JsonNode.Parse("""[42]""")!.AsArray();
        var columns = BuildColumnDefinitions();
        var converter = BuildConverter(columns);
        var channel = Channel.CreateBounded<object?[]>(10);

        var task = (Task)method.Invoke(null, [batch, columns, converter, channel.Writer, CancellationToken.None, (int?)null])!;

        var ex = Assert.ThrowsException<AggregateException>(() => task.Wait());
        Assert.IsInstanceOfType<InvalidOperationException>(ex.InnerException,
            "A scalar value in the OData value array must raise InvalidOperationException");
    }

    [TestMethod]
    public void WriteBatchAsync_NullEntry_ThrowsInvalidOperationException()
    {
        var method = typeof(QueryOData).GetMethod(
            "WriteBatchAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method);

        var batch = JsonNode.Parse("""[null]""")!.AsArray();
        var columns = BuildColumnDefinitions();
        var converter = BuildConverter(columns);
        var channel = Channel.CreateBounded<object?[]>(10);

        var task = (Task)method.Invoke(null, [batch, columns, converter, channel.Writer, CancellationToken.None, (int?)null])!;

        var ex = Assert.ThrowsException<AggregateException>(() => task.Wait());
        Assert.IsInstanceOfType<InvalidOperationException>(ex.InnerException,
            "A null entry in the OData value array must raise InvalidOperationException");
    }

    [TestMethod]
    public void WriteBatchAsync_ArrayEntry_ThrowsInvalidOperationException()
    {
        var method = typeof(QueryOData).GetMethod(
            "WriteBatchAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method);

        var batch = JsonNode.Parse("""[[1,2,3]]""")!.AsArray();
        var columns = BuildColumnDefinitions();
        var converter = BuildConverter(columns);
        var channel = Channel.CreateBounded<object?[]>(10);

        var task = (Task)method.Invoke(null, [batch, columns, converter, channel.Writer, CancellationToken.None, (int?)null])!;

        var ex = Assert.ThrowsException<AggregateException>(() => task.Wait());
        Assert.IsInstanceOfType<InvalidOperationException>(ex.InnerException,
            "A nested array in the OData value array must raise InvalidOperationException");
    }

    [TestMethod]
    public void WriteBatchAsync_ValidObjectEntries_WritesToChannel()
    {
        var method = typeof(QueryOData).GetMethod(
            "WriteBatchAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method);

        var batch = JsonNode.Parse("""[{"Id":1},{"Id":2}]""")!.AsArray();
        var columns = BuildColumnDefinitions();
        var converter = BuildConverter(columns);
        var channel = Channel.CreateBounded<object?[]>(10);

        var task = (Task)method.Invoke(null, [batch, columns, converter, channel.Writer, CancellationToken.None, (int?)null])!;
        task.Wait();

        Assert.IsTrue(channel.Reader.TryRead(out _), "First row should be readable");
        Assert.IsTrue(channel.Reader.TryRead(out _), "Second row should be readable");
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static System.Data.IDataReader CreateReaderViaReflection(
        object columnDefs,
        ChannelReader<object?[]> channelReader,
        CancellationTokenSource cts,
        Task task)
    {
        var readerType = typeof(QueryOData).GetNestedType(
            "ODataStreamingDataReader",
            System.Reflection.BindingFlags.NonPublic)!;
        var ctor = readerType.GetConstructors(
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance)[0];

        return (System.Data.IDataReader)ctor.Invoke([columnDefs, channelReader, cts, task]);
    }

    private static object BuildColumnDefinitions()
    {
        var colDefType = typeof(QueryOData).GetNestedType(
            "ColumnDefinition",
            System.Reflection.BindingFlags.NonPublic)!;
        var ctor = colDefType.GetConstructors()[0];

        Func<JsonNode?, object> converter = node => node?.GetValue<int>() ?? (object)DBNull.Value;
        // ColumnDefinition now has a 5th parameter AllowDbNull (item 54); default to true.
        var col = ctor.Invoke(["Id", typeof(int), 0, converter, true]);

        var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(colDefType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        list.Add(col);

        var roListType = typeof(System.Collections.Generic.IReadOnlyList<>).MakeGenericType(colDefType);
        return list;
    }

    private static Func<JsonObject, object[]> BuildConverter(object columns)
    {
        // Use the private CompileRowConverter method
        var method = typeof(QueryOData).GetMethod(
            "CompileRowConverter",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (Func<JsonObject, object[]>)method.Invoke(null, [columns])!;
    }
}
