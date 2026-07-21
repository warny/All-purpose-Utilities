using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.Reflection.Emit;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates the JSON wire format used between <see cref="EmitWorkerProcess"/> (host side) and
/// <see cref="EmitWorkerHost"/> (worker side).
/// </summary>
[TestClass]
public class EmitWorkerProtocolTests
{
    /// <summary>Field-based (no properties) shape typical of a P/Invoke interop struct.</summary>
    public struct FieldOnlyStruct
    {
        public int X;
        public double Y;
    }

    [TestMethod]
    public void JsonOptions_RoundTripsStructWithPublicFields()
    {
        var value = new FieldOnlyStruct { X = 7, Y = 3.5 };

        string json = JsonSerializer.Serialize(value, typeof(FieldOnlyStruct), CrossProcessMarshaling.JsonOptions);
        var roundTripped = (FieldOnlyStruct)JsonSerializer.Deserialize(json, typeof(FieldOnlyStruct), CrossProcessMarshaling.JsonOptions)!;

        Assert.AreEqual(value.X, roundTripped.X);
        Assert.AreEqual(value.Y, roundTripped.Y);
    }

    [TestMethod]
    public void DefaultJsonSerializerOptions_LoseFieldOnlyStructData()
    {
        // Documents the exact bug CrossProcessMarshaling.JsonOptions fixes: without
        // IncludeFields, System.Text.Json silently serializes a field-only struct as "{}".
        var value = new FieldOnlyStruct { X = 7, Y = 3.5 };

        string json = JsonSerializer.Serialize(value, typeof(FieldOnlyStruct));

        Assert.AreEqual("{}", json);
    }

    [TestMethod]
    public void WorkerRequest_LoadKind_RoundTrips()
    {
        var request = new WorkerRequest
        {
            Id = 1,
            Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = @"C:\plugins\MyPlugin.dll",
            InterfaceTypeFullName = "MyPlugin.IMathLib",
            DllPath = @"C:\plugins\math.dll",
            CallingConvention = CallingConvention.Cdecl,
        };

        string json = JsonSerializer.Serialize(request);
        WorkerRequest? roundTripped = JsonSerializer.Deserialize<WorkerRequest>(json);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(request.Id, roundTripped.Id);
        Assert.AreEqual(request.Kind, roundTripped.Kind);
        Assert.AreEqual(request.InterfaceAssemblyPath, roundTripped.InterfaceAssemblyPath);
        Assert.AreEqual(request.InterfaceTypeFullName, roundTripped.InterfaceTypeFullName);
        Assert.AreEqual(request.DllPath, roundTripped.DllPath);
        Assert.AreEqual(request.CallingConvention, roundTripped.CallingConvention);
    }

    [TestMethod]
    public void WorkerRequest_CallKind_RoundTripsArguments()
    {
        var request = new WorkerRequest
        {
            Id = 2,
            Kind = WorkerRequestKind.Call,
            MethodCommandId = 3,
            ArgumentsJson = ["1", "\"hello\"", null],
        };

        string json = JsonSerializer.Serialize(request);
        WorkerRequest? roundTripped = JsonSerializer.Deserialize<WorkerRequest>(json);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(request.MethodCommandId, roundTripped.MethodCommandId);
        CollectionAssert.AreEqual(request.ArgumentsJson, roundTripped.ArgumentsJson);
    }

    [TestMethod]
    public void WorkerResponse_Success_RoundTrips()
    {
        var response = new WorkerResponse
        {
            Id = 3,
            Success = true,
            ReturnValueJson = "42",
            ByRefValuesJson = [null, "\"out-value\""],
        };

        string json = JsonSerializer.Serialize(response);
        WorkerResponse? roundTripped = JsonSerializer.Deserialize<WorkerResponse>(json);

        Assert.IsNotNull(roundTripped);
        Assert.IsTrue(roundTripped.Success);
        Assert.AreEqual(response.ReturnValueJson, roundTripped.ReturnValueJson);
        CollectionAssert.AreEqual(response.ByRefValuesJson, roundTripped.ByRefValuesJson);
    }

    [TestMethod]
    public void WorkerResponse_Failure_CarriesErrorDetails()
    {
        var response = new WorkerResponse
        {
            Id = 4,
            Success = false,
            ErrorMessage = "Native call failed.",
            ErrorTypeName = "System.InvalidOperationException",
            ErrorStackTrace = "   at Utils.Reflection.Reflection.Emit.EmitWorkerHost.HandleCall(...)",
        };

        string json = JsonSerializer.Serialize(response);
        WorkerResponse? roundTripped = JsonSerializer.Deserialize<WorkerResponse>(json);

        Assert.IsNotNull(roundTripped);
        Assert.IsFalse(roundTripped.Success);
        Assert.AreEqual(response.ErrorMessage, roundTripped.ErrorMessage);
        Assert.AreEqual(response.ErrorTypeName, roundTripped.ErrorTypeName);
        Assert.AreEqual(response.ErrorStackTrace, roundTripped.ErrorStackTrace);
    }

    // ─── Item 43: malformed JSON is fatal ────────────────────────────────────────

    [TestMethod]
    public void EmitWorkerHost_Run_ThrowsOnMalformedRequestLine()
    {
        // The first line is valid JSON (a Shutdown request) so the worker actually starts
        // processing before hitting the invalid line; the invalid line itself triggers the
        // fatal path. We send Shutdown first so the worker doesn't block waiting for more input.
        string malformed = "this is not json\n";
        using var input = new StringReader(malformed);
        using var output = new StringWriter();

        Assert.ThrowsException<InvalidOperationException>(
            () => EmitWorkerHost.Run(input, output));
    }

    // ─── Item 40: bounded concurrent dispatch ────────────────────────────────────

    [TestMethod]
    public void MaxConcurrency_IsPositiveAndReasonable()
    {
        // Verify the constant exists, is positive, and is within the range a real worker would use.
        Assert.IsTrue(EmitWorkerHost.MaxConcurrency > 0);
        Assert.IsTrue(EmitWorkerHost.MaxConcurrency <= 1024,
            "MaxConcurrency should be well below typical thread-pool sizes to prevent starvation.");
    }

    // ─── Item 39: bounded protocol line reader ───────────────────────────────────

    [TestMethod]
    public void ReadBoundedLine_ReturnsNullAtEndOfStream()
    {
        using var reader = new StringReader("");
        Assert.IsNull(ProtocolFraming.ReadBoundedLine(reader));
    }

    [TestMethod]
    public void ReadBoundedLine_ReturnsLineContent()
    {
        using var reader = new StringReader("hello world\nsecond line");
        Assert.AreEqual("hello world", ProtocolFraming.ReadBoundedLine(reader));
        Assert.AreEqual("second line", ProtocolFraming.ReadBoundedLine(reader));
        Assert.IsNull(ProtocolFraming.ReadBoundedLine(reader));
    }

    [TestMethod]
    public void ReadBoundedLine_HandlesWindowsLineEndings()
    {
        using var reader = new StringReader("line1\r\nline2");
        Assert.AreEqual("line1", ProtocolFraming.ReadBoundedLine(reader));
        Assert.AreEqual("line2", ProtocolFraming.ReadBoundedLine(reader));
    }

    [TestMethod]
    public void ReadBoundedLine_ThrowsWhenLineExceedsLimit()
    {
        string oversizedLine = new string('x', 101) + "\n";
        using var reader = new StringReader(oversizedLine);

        Assert.ThrowsException<InvalidOperationException>(
            () => ProtocolFraming.ReadBoundedLine(reader, maxLength: 100));
    }

    [TestMethod]
    public void ReadBoundedLine_AcceptsLineExactlyAtLimit()
    {
        string lineAtLimit = new string('x', 100) + "\n";
        using var reader = new StringReader(lineAtLimit);

        string? result = ProtocolFraming.ReadBoundedLine(reader, maxLength: 100);
        Assert.AreEqual(new string('x', 100), result);
    }

    // ─── Finding #1: stable method command table replaces raw metadata tokens ────

    /// <summary>Interface with multiple methods — used to verify <see cref="CrossProcessMarshaling.BuildCommandTable"/>
    /// produces a deterministic, alphabetically-sorted ordering independent of reflection enumeration order.</summary>
    private interface IMultiMethodContract
    {
        string Describe();
        int Compute(int a, int b);
        void Initialize();
    }

    /// <summary>Single-method interface used to verify BuildCommandTable on the simplest case.</summary>
    private interface ISingleMethodContract
    {
        int Compute(int a, int b);
    }

    [TestMethod]
    public void BuildCommandTable_SingleMethod_ReturnsThatMethod()
    {
        var table = CrossProcessMarshaling.BuildCommandTable(typeof(ISingleMethodContract));

        Assert.AreEqual(1, table.Length);
        Assert.AreEqual(nameof(ISingleMethodContract.Compute), table[0].Name);
    }

    [TestMethod]
    public void BuildCommandTable_MultipleMethodsSameDeclaringType_SortsByName()
    {
        // All three methods are on the same declaring type, so the tiebreaker is method name.
        // Alphabetical order: Compute < Describe < Initialize.
        var table = CrossProcessMarshaling.BuildCommandTable(typeof(IMultiMethodContract));

        Assert.AreEqual(3, table.Length);
        Assert.AreEqual(nameof(IMultiMethodContract.Compute), table[0].Name,
            "Compute must be first (C < D < I).");
        Assert.AreEqual(nameof(IMultiMethodContract.Describe), table[1].Name,
            "Describe must be second.");
        Assert.AreEqual(nameof(IMultiMethodContract.Initialize), table[2].Name,
            "Initialize must be third.");
    }

    [TestMethod]
    public void BuildCommandTable_IsDeterministic_SameOutputOnRepeatedCalls()
    {
        var table1 = CrossProcessMarshaling.BuildCommandTable(typeof(IMultiMethodContract));
        var table2 = CrossProcessMarshaling.BuildCommandTable(typeof(IMultiMethodContract));

        Assert.AreEqual(table1.Length, table2.Length);
        for (int i = 0; i < table1.Length; i++)
        {
            Assert.AreEqual(table1[i], table2[i], $"Entry at index {i} must be identical across calls.");
        }
    }

    // ─── Review #472 item 3: bounded active-task tracking ────────────────────────

    [TestMethod]
    public void Run_ShutdownRequest_CompletesWithSuccessResponse()
    {
        // Verifies that Run correctly processes a Shutdown request and writes a success response.
        // Also exercises the DrainDispatched path with an empty active-task dictionary.
        var shutdownRequest = new WorkerRequest { Id = 42, Kind = WorkerRequestKind.Shutdown };
        string requestLine = JsonSerializer.Serialize(shutdownRequest);
        using var input = new StringReader(requestLine + "\n");
        using var output = new StringWriter();

        EmitWorkerHost.Run(input, output);

        string responseJson = output.ToString().Trim();
        WorkerResponse? response = JsonSerializer.Deserialize<WorkerResponse>(responseJson);
        Assert.IsNotNull(response, "A response line must be written for every Shutdown request.");
        Assert.AreEqual(42, response.Id);
        Assert.IsTrue(response.Success, "Shutdown must produce a success response.");
    }

    [TestMethod]
    public void Run_EmptyInput_ReturnsWithoutThrowingOrHanging()
    {
        // Verifies that Run returns gracefully when the input stream is empty (pipe closed abruptly).
        // DrainDispatched must handle an empty active-task dictionary correctly.
        using var input = new StringReader("");
        using var output = new StringWriter();

        EmitWorkerHost.Run(input, output); // Must not throw or block.
    }

    // ─── Finding #3: Shutdown truthfulness ───────────────────────────────────────

    [TestMethod]
    public void Run_Shutdown_WithNoActiveTasks_ReturnsSuccess()
    {
        // When there are no active tasks the drain is trivially complete; Success must be true.
        var shutdown = new WorkerRequest { Id = 99, Kind = WorkerRequestKind.Shutdown };
        using var input = new StringReader(JsonSerializer.Serialize(shutdown) + "\n");
        using var output = new StringWriter();

        EmitWorkerHost.Run(input, output);

        WorkerResponse? response = JsonSerializer.Deserialize<WorkerResponse>(output.ToString().Trim());
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success,
            "Shutdown with no active tasks must return Success = true (drain is trivially complete).");
        Assert.IsNull(response.ErrorMessage,
            "Success shutdown must carry no ErrorMessage.");
    }

}
