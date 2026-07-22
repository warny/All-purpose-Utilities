using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Tests for <see cref="ODataStreamingDataReader"/> (private nested type) covering
/// items 42 and 43 of the Utils.OData audit:
/// — GetBytes/GetChars argument validation
/// — Duplicate column name detection.
/// </summary>
[TestClass]
public class ODataStreamingReaderTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly Type ReaderType = typeof(QueryOData).GetNestedType(
        "ODataStreamingDataReader",
        System.Reflection.BindingFlags.NonPublic)!;

    private static readonly Type ColDefType = typeof(QueryOData).GetNestedType(
        "ColumnDefinition",
        System.Reflection.BindingFlags.NonPublic)!;

    private static object MakeColumnDefinition(string name, Type clrType, int ordinal,
        Func<JsonNode?, object>? converter = null)
    {
        converter ??= _ => DBNull.Value;
        // ColumnDefinition has a 5th parameter AllowDbNull added by item 54; default to true.
        return ColDefType.GetConstructors()[0].Invoke([name, clrType, ordinal, converter, true]);
    }

    private static System.Collections.IList MakeColumnList(params object[] cols)
    {
        var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(ColDefType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var col in cols)
            list.Add(col);
        return list;
    }

    private static System.Data.IDataReader CreateReader(
        System.Collections.IList columns,
        ChannelReader<object?[]> channelReader,
        CancellationTokenSource cts,
        Task backgroundTask)
    {
        var ctor = ReaderType.GetConstructors(
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance)[0];
        return (System.Data.IDataReader)ctor.Invoke([columns, channelReader, cts, backgroundTask]);
    }

    private static (System.Data.IDataReader reader, ChannelWriter<object?[]> writer)
        CreateReaderWithChannel(params object[] columnDefs)
    {
        var channel = Channel.CreateBounded<object?[]>(100);
        using var cts = new CancellationTokenSource();
        var columns = MakeColumnList(columnDefs);
        var reader = CreateReader(columns, channel.Reader, cts, Task.CompletedTask);
        return (reader, channel.Writer);
    }

    // -----------------------------------------------------------------------
    // Item 42 — GetBytes argument validation (P1)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void GetBytes_NullBuffer_ReturnsFieldLength()
    {
        byte[] fieldValue = [1, 2, 3, 4, 5];
        Func<JsonNode?, object> converter = _ => fieldValue;
        var col = MakeColumnDefinition("Data", typeof(byte[]), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite([fieldValue]);

        reader.Read();
        long length = reader.GetBytes(0, 0, null, 0, 0);
        Assert.AreEqual(5, length, "Null buffer must return total field length");
        reader.Dispose();
        cts.Dispose();
    }

    [TestMethod]
    public void GetBytes_NegativeFieldOffset_ThrowsArgumentOutOfRange()
    {
        byte[] fieldValue = [1, 2, 3];
        Func<JsonNode?, object> converter = _ => fieldValue;
        var col = MakeColumnDefinition("Data", typeof(byte[]), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite([fieldValue]);
        reader.Read();

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => reader.GetBytes(0, -1, null, 0, 0),
            "Negative field offset must throw ArgumentOutOfRangeException");
        reader.Dispose();
        cts.Dispose();
    }

    [TestMethod]
    public void GetBytes_OverflowingFieldOffset_ThrowsArgumentOutOfRange()
    {
        byte[] fieldValue = [1, 2, 3];
        Func<JsonNode?, object> converter = _ => fieldValue;
        var col = MakeColumnDefinition("Data", typeof(byte[]), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite([fieldValue]);
        reader.Read();

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => reader.GetBytes(0, (long)int.MaxValue + 1, null, 0, 0),
            "Field offset > int.MaxValue must throw ArgumentOutOfRangeException");
        reader.Dispose();
        cts.Dispose();
    }

    [TestMethod]
    public void GetBytes_NegativeLength_ThrowsArgumentOutOfRange()
    {
        byte[] fieldValue = [1, 2, 3];
        Func<JsonNode?, object> converter = _ => fieldValue;
        var col = MakeColumnDefinition("Data", typeof(byte[]), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite([fieldValue]);
        reader.Read();

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => reader.GetBytes(0, 0, new byte[10], 0, -1),
            "Negative length must throw ArgumentOutOfRangeException");
        reader.Dispose();
        cts.Dispose();
    }

    [TestMethod]
    public void GetBytes_DestinationRangeOverflow_ThrowsArgumentException()
    {
        byte[] fieldValue = [1, 2, 3, 4, 5];
        Func<JsonNode?, object> converter = _ => fieldValue;
        var col = MakeColumnDefinition("Data", typeof(byte[]), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite([fieldValue]);
        reader.Read();

        // Buffer has 3 slots, offset 1 leaves 2 free, but we request 3 bytes → overflow.
        Assert.ThrowsException<ArgumentException>(
            () => reader.GetBytes(0, 0, new byte[3], 1, 3),
            "bufferoffset + length > buffer.Length must throw ArgumentException");
        reader.Dispose();
        cts.Dispose();
    }

    [TestMethod]
    public void GetBytes_CopiesCorrectData()
    {
        byte[] fieldValue = [10, 20, 30, 40, 50];
        Func<JsonNode?, object> converter = _ => fieldValue;
        var col = MakeColumnDefinition("Data", typeof(byte[]), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite([fieldValue]);
        reader.Read();

        var dest = new byte[3];
        long count = reader.GetBytes(0, 1, dest, 0, 3);
        Assert.AreEqual(3, count);
        CollectionAssert.AreEqual(new byte[] { 20, 30, 40 }, dest);
        reader.Dispose();
        cts.Dispose();
    }

    // -----------------------------------------------------------------------
    // Item 42 — GetChars argument validation (P1)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void GetChars_NullBuffer_ReturnsFieldLength()
    {
        Func<JsonNode?, object> converter = _ => "Hello";
        var col = MakeColumnDefinition("Text", typeof(string), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite(["Hello"]);
        reader.Read();

        long length = reader.GetChars(0, 0, null, 0, 0);
        Assert.AreEqual(5, length, "Null buffer must return total field character count");
        reader.Dispose();
        cts.Dispose();
    }

    [TestMethod]
    public void GetChars_NegativeFieldOffset_ThrowsArgumentOutOfRange()
    {
        Func<JsonNode?, object> converter = _ => "Hello";
        var col = MakeColumnDefinition("Text", typeof(string), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite(["Hello"]);
        reader.Read();

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => reader.GetChars(0, -1, null, 0, 0));
        reader.Dispose();
        cts.Dispose();
    }

    [TestMethod]
    public void GetChars_OverflowingFieldOffset_ThrowsArgumentOutOfRange()
    {
        Func<JsonNode?, object> converter = _ => "Hello";
        var col = MakeColumnDefinition("Text", typeof(string), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite(["Hello"]);
        reader.Read();

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => reader.GetChars(0, (long)int.MaxValue + 1, null, 0, 0));
        reader.Dispose();
        cts.Dispose();
    }

    [TestMethod]
    public void GetChars_NegativeLength_ThrowsArgumentOutOfRange()
    {
        Func<JsonNode?, object> converter = _ => "Hello";
        var col = MakeColumnDefinition("Text", typeof(string), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite(["Hello"]);
        reader.Read();

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => reader.GetChars(0, 0, new char[10], 0, -1));
        reader.Dispose();
        cts.Dispose();
    }

    [TestMethod]
    public void GetChars_DestinationRangeOverflow_ThrowsArgumentException()
    {
        Func<JsonNode?, object> converter = _ => "Hello";
        var col = MakeColumnDefinition("Text", typeof(string), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite(["Hello"]);
        reader.Read();

        // Buffer has 3 slots, offset 1 leaves 2 free, but we request 3 chars → overflow.
        Assert.ThrowsException<ArgumentException>(
            () => reader.GetChars(0, 0, new char[3], 1, 3),
            "bufferoffset + length > buffer.Length must throw ArgumentException");
        reader.Dispose();
        cts.Dispose();
    }

    [TestMethod]
    public void GetChars_CopiesCorrectCharacters()
    {
        Func<JsonNode?, object> converter = _ => "Hello";
        var col = MakeColumnDefinition("Text", typeof(string), 0, converter);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var reader = CreateReader(MakeColumnList(col), channel.Reader, cts, Task.CompletedTask);

        channel.Writer.TryWrite(["Hello"]);
        reader.Read();

        var dest = new char[3];
        long count = reader.GetChars(0, 1, dest, 0, 3);
        Assert.AreEqual(3, count);
        CollectionAssert.AreEqual(new[] { 'e', 'l', 'l' }, dest);
        reader.Dispose();
        cts.Dispose();
    }

    // -----------------------------------------------------------------------
    // Item 43 — Duplicate column name rejection (P1)
    // -----------------------------------------------------------------------

    private static ArgumentException ThrowsArgumentExceptionFromReflectedCtor(Action act)
    {
        // Reflection wraps constructor exceptions in TargetInvocationException.
        var tie = Assert.ThrowsException<System.Reflection.TargetInvocationException>(act);
        Assert.IsInstanceOfType<ArgumentException>(tie.InnerException,
            "Inner exception must be ArgumentException");
        return (ArgumentException)tie.InnerException!;
    }

    [TestMethod]
    public void DuplicateColumnName_ThrowsArgumentException()
    {
        var col1 = MakeColumnDefinition("Name", typeof(string), 0);
        var col2 = MakeColumnDefinition("Name", typeof(string), 1);
        var channel = Channel.CreateBounded<object?[]>(10);
        using var cts = new CancellationTokenSource();
        var columns = MakeColumnList(col1, col2);

        ThrowsArgumentExceptionFromReflectedCtor(
            () => CreateReader(columns, channel.Reader, cts, Task.CompletedTask));
    }

    [TestMethod]
    public void DuplicateColumnName_CaseInsensitive_ThrowsArgumentException()
    {
        var col1 = MakeColumnDefinition("name", typeof(string), 0);
        var col2 = MakeColumnDefinition("NAME", typeof(string), 1);
        var channel = Channel.CreateBounded<object?[]>(10);
        using var cts = new CancellationTokenSource();
        var columns = MakeColumnList(col1, col2);

        ThrowsArgumentExceptionFromReflectedCtor(
            () => CreateReader(columns, channel.Reader, cts, Task.CompletedTask));
    }

    [TestMethod]
    public void UniqueColumnNames_DoNotThrow()
    {
        var col1 = MakeColumnDefinition("Id", typeof(int), 0);
        var col2 = MakeColumnDefinition("Name", typeof(string), 1);
        var channel = Channel.CreateBounded<object?[]>(10);
        var cts = new CancellationTokenSource();
        var columns = MakeColumnList(col1, col2);

        // Dispose the reader before the cts so the reader can cancel cleanly.
        var reader = CreateReader(columns, channel.Reader, cts, Task.CompletedTask);
        Assert.IsNotNull(reader, "Reader with unique columns must be created successfully");
        reader.Dispose();
        cts.Dispose();
    }
}
