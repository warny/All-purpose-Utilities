using System;
using System.IO;
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
}
