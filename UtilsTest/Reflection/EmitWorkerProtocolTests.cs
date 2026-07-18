using System;
using System.Collections.Frozen;
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
            MethodMetadataToken = 0x06000123,
            ArgumentsJson = ["1", "\"hello\"", null],
        };

        string json = JsonSerializer.Serialize(request);
        WorkerRequest? roundTripped = JsonSerializer.Deserialize<WorkerRequest>(json);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(request.MethodMetadataToken, roundTripped.MethodMetadataToken);
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

    // ─── Item 38: method token restricted to loaded interface ────────────────────

    /// <summary>Minimal interface used only to supply real metadata tokens for token-restriction tests.</summary>
    private interface ITokenTestContract
    {
        int Compute(int a, int b);
    }

    /// <summary>
    /// A Call request whose <see cref="WorkerRequest.MethodMetadataToken"/> does not appear in the
    /// loaded interface's method set must be rejected before <c>Module.ResolveMethod</c> is invoked,
    /// preventing the worker from being directed to call arbitrary methods in the same module.
    /// </summary>
    [TestMethod]
    public void ValidateMethodToken_RejectsTokenOutsideInterfaceContract()
    {
        FrozenSet<int> allowedTokens = typeof(ITokenTestContract)
            .GetMethods()
            .Select(m => m.MetadataToken)
            .ToFrozenSet();

        // 0x06ABCDEF is a syntactically valid method token that is not in ITokenTestContract.
        const int foreignToken = 0x06ABCDEF;

        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => EmitWorkerHost.ValidateMethodToken(typeof(ITokenTestContract), allowedTokens, foreignToken));

        // The error message should contain the token so it can be correlated in logs.
        StringAssert.Contains(ex.Message, "0x06ABCDEF",
            "The error message should include the rejected token value.");
    }

    [TestMethod]
    public void ValidateMethodToken_AcceptsTokenFromInterface()
    {
        FrozenSet<int> allowedTokens = typeof(ITokenTestContract)
            .GetMethods()
            .Select(m => m.MetadataToken)
            .ToFrozenSet();

        int validToken = typeof(ITokenTestContract).GetMethod(nameof(ITokenTestContract.Compute))!.MetadataToken;

        // Must not throw — a valid interface method token is accepted.
        EmitWorkerHost.ValidateMethodToken(typeof(ITokenTestContract), allowedTokens, validToken);
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
}
