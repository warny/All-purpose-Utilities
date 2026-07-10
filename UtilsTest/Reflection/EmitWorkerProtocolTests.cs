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
        };

        string json = JsonSerializer.Serialize(response);
        WorkerResponse? roundTripped = JsonSerializer.Deserialize<WorkerResponse>(json);

        Assert.IsNotNull(roundTripped);
        Assert.IsFalse(roundTripped.Success);
        Assert.AreEqual(response.ErrorMessage, roundTripped.ErrorMessage);
        Assert.AreEqual(response.ErrorTypeName, roundTripped.ErrorTypeName);
    }
}
