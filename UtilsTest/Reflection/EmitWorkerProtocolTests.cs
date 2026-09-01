using System;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

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
    public void WorkerRequest_CallKind_RoundTripsMethodIdAndArguments()
    {
        // WorkerRequest now uses MethodId (worker-assigned private ID) instead of MethodMetadataToken
        // so that cross-module method identity is preserved (metadata tokens are module-scoped).
        var request = new WorkerRequest
        {
            Id = 2,
            Kind = WorkerRequestKind.Call,
            MethodId = 3, // worker-assigned private ID (not a metadata token)
            ArgumentsJson = ["1", "\"hello\"", null],
        };

        string json = JsonSerializer.Serialize(request);
        WorkerRequest? roundTripped = JsonSerializer.Deserialize<WorkerRequest>(json);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(request.MethodId, roundTripped.MethodId);
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

    // ─── Item 1: worker-private method IDs replace metadata tokens ───────────────

    /// <summary>Minimal interface used for method-descriptor and method-ID tests.</summary>
    private interface IMethodIdTestContract
    {
        int Compute(int a, int b);
        string Format(double value);
    }

    [TestMethod]
    public void MethodDescriptorDto_FromMethodInfo_CapturesCorrectSignature()
    {
        MethodInfo method = typeof(IMethodIdTestContract).GetMethod(nameof(IMethodIdTestContract.Compute))!;

        var descriptor = MethodDescriptorDto.FromMethodInfo(42, method);

        Assert.AreEqual(42, descriptor.MethodId);
        Assert.AreEqual("Compute", descriptor.Name);

        // DeclaringType and parameter/return types use AssemblyQualifiedName for cross-assembly
        // identity, not FullName, so two types from different assemblies with the same name do
        // not collide when the host matches descriptors to local MethodInfo instances.
        Assert.AreEqual(typeof(IMethodIdTestContract).AssemblyQualifiedName, descriptor.DeclaringType);
        Assert.AreEqual(2, descriptor.ParameterTypes.Length);
        Assert.AreEqual(typeof(int).AssemblyQualifiedName, descriptor.ParameterTypes[0]);
        Assert.AreEqual(typeof(int).AssemblyQualifiedName, descriptor.ParameterTypes[1]);
        Assert.AreEqual(typeof(int).AssemblyQualifiedName, descriptor.ReturnType);
    }

    [TestMethod]
    public void MethodDescriptorDto_StableTypeName_ByRefTypeHasAmpersandSuffix()
    {
        // By-ref types (ref/out parameters) have no CLR AssemblyQualifiedName, so StableTypeName
        // constructs a canonical form: element's AQN + "&".
        Type byRefInt = typeof(int).MakeByRefType();
        string name = MethodDescriptorDto.StableTypeName(byRefInt);

        Assert.IsTrue(name.EndsWith("&", StringComparison.Ordinal),
            $"StableTypeName for a ByRef type must end with '&', got: {name}");
        StringAssert.Contains(name, typeof(int).AssemblyQualifiedName!,
            "StableTypeName for ref int must embed the element type's assembly-qualified name.");
    }

    [TestMethod]
    public void MethodDescriptorDto_RoundTripsAsJson()
    {
        MethodInfo method = typeof(IMethodIdTestContract).GetMethod(nameof(IMethodIdTestContract.Format))!;
        var original = MethodDescriptorDto.FromMethodInfo(7, method);

        string json = JsonSerializer.Serialize(original);
        MethodDescriptorDto? roundTripped = JsonSerializer.Deserialize<MethodDescriptorDto>(json);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(original.MethodId, roundTripped.MethodId);
        Assert.AreEqual(original.Name, roundTripped.Name);
        Assert.AreEqual(original.DeclaringType, roundTripped.DeclaringType);
        CollectionAssert.AreEqual(original.ParameterTypes, roundTripped.ParameterTypes);
        Assert.AreEqual(original.ReturnType, roundTripped.ReturnType);
    }

    [TestMethod]
    public void WorkerResponse_Load_CarriesMethodDescriptors()
    {
        // Load responses now carry MethodDescriptors so the host can build a MethodInfo→ID mapping.
        var response = new WorkerResponse
        {
            Id = 1,
            Success = true,
            Handle = 1,
            MethodDescriptors =
            [
                new MethodDescriptorDto { MethodId = 0, Name = "Compute", DeclaringType = "MyNs.IFoo",
                    ParameterTypes = ["System.Int32", "System.Int32"], ReturnType = "System.Int32" },
                new MethodDescriptorDto { MethodId = 1, Name = "Format", DeclaringType = "MyNs.IFoo",
                    ParameterTypes = ["System.Double"], ReturnType = "System.String" },
            ],
        };

        string json = JsonSerializer.Serialize(response);
        WorkerResponse? roundTripped = JsonSerializer.Deserialize<WorkerResponse>(json);

        Assert.IsNotNull(roundTripped);
        Assert.IsNotNull(roundTripped.MethodDescriptors);
        Assert.AreEqual(2, roundTripped.MethodDescriptors.Length);
        Assert.AreEqual(0, roundTripped.MethodDescriptors[0].MethodId);
        Assert.AreEqual("Compute", roundTripped.MethodDescriptors[0].Name);
        Assert.AreEqual(1, roundTripped.MethodDescriptors[1].MethodId);
        Assert.AreEqual("Format", roundTripped.MethodDescriptors[1].Name);
    }

    // ─── Item 2: per-handle call leasing ─────────────────────────────────────────

    /// <summary>Minimal interface for per-handle lease tests.</summary>
    private interface ILeaseTestContract
    {
        void DoWork();
    }

    [TestMethod]
    public void LoadedInterfaceState_TryAcquireCallLease_ReturnsNullWhenClosing()
    {
        var methods = typeof(ILeaseTestContract).GetMethods();
        var methodsById = Enumerable.Range(0, methods.Length)
            .ToFrozenDictionary(i => i, i => methods[i]);
        var state = new EmitWorkerHost.LoadedInterfaceState(new object(), typeof(ILeaseTestContract), methodsById);

        // Acquire and immediately release to confirm the state is open.
        using (EmitWorkerHost.LoadedInterfaceState.CallLease? first = state.TryAcquireCallLease())
        {
            Assert.IsNotNull(first, "TryAcquireCallLease must succeed when Open.");
        }

        // Close the state on a background thread while we hold a lease.
        using (EmitWorkerHost.LoadedInterfaceState.CallLease? lease = state.TryAcquireCallLease())
        {
            Assert.IsNotNull(lease);

            // Attempt to acquire another lease while one is held — must succeed (still Open).
            using (EmitWorkerHost.LoadedInterfaceState.CallLease? concurrent = state.TryAcquireCallLease())
            {
                Assert.IsNotNull(concurrent, "Concurrent leases must be allowed while the state is Open.");
            }
        } // releasing lease here

        // After CloseAndDispose, new leases must be rejected.
        state.CloseAndDispose();

        EmitWorkerHost.LoadedInterfaceState.CallLease? afterClose = state.TryAcquireCallLease();
        Assert.IsNull(afterClose, "TryAcquireCallLease must return null after CloseAndDispose.");
    }

    [TestMethod]
    public void LoadedInterfaceState_CloseAndDispose_WaitsForActiveLease()
    {
        var methods = typeof(ILeaseTestContract).GetMethods();
        var methodsById = Enumerable.Range(0, methods.Length)
            .ToFrozenDictionary(i => i, i => methods[i]);
        var state = new EmitWorkerHost.LoadedInterfaceState(new object(), typeof(ILeaseTestContract), methodsById);

        EmitWorkerHost.LoadedInterfaceState.CallLease? lease = state.TryAcquireCallLease();
        Assert.IsNotNull(lease);

        bool closeCompleted = false;
        var closeThread = new Thread(() =>
        {
            state.CloseAndDispose(force: false);
            closeCompleted = true;
        });
        closeThread.Start();

        // Give the close thread a chance to block — it should wait for the lease.
        Thread.Sleep(50);
        Assert.IsFalse(closeCompleted, "CloseAndDispose must block while a lease is held.");

        // Releasing the lease should unblock CloseAndDispose.
        lease.Dispose();
        closeThread.Join(TimeSpan.FromSeconds(2));
        Assert.IsTrue(closeCompleted, "CloseAndDispose must complete after all leases are released.");
    }

    [TestMethod]
    public void LoadedInterfaceState_ForceClose_DoesNotWaitForActiveLease()
    {
        var methods = typeof(ILeaseTestContract).GetMethods();
        var methodsById = Enumerable.Range(0, methods.Length)
            .ToFrozenDictionary(i => i, i => methods[i]);
        var state = new EmitWorkerHost.LoadedInterfaceState(new object(), typeof(ILeaseTestContract), methodsById);

        using EmitWorkerHost.LoadedInterfaceState.CallLease? lease = state.TryAcquireCallLease();
        Assert.IsNotNull(lease);

        // force=true must return immediately without waiting for the active lease.
        bool completed = false;
        var t = new Thread(() => { state.CloseAndDispose(force: true); completed = true; });
        t.Start();
        t.Join(TimeSpan.FromSeconds(2));

        Assert.IsTrue(completed, "CloseAndDispose(force: true) must return without waiting for leases.");
    }

    [TestMethod]
    public void LoadedInterfaceState_CloseAndDispose_IsIdempotent()
    {
        var methods = typeof(ILeaseTestContract).GetMethods();
        var methodsById = Enumerable.Range(0, methods.Length)
            .ToFrozenDictionary(i => i, i => methods[i]);
        var state = new EmitWorkerHost.LoadedInterfaceState(new object(), typeof(ILeaseTestContract), methodsById);

        // Calling CloseAndDispose twice must not throw.
        state.CloseAndDispose();
        state.CloseAndDispose(); // must be a no-op
    }

    // ─── Item 3+4: truthful shutdown + deterministic cleanup ─────────────────────

    [TestMethod]
    public void Run_ShutdownRequest_CompletesWithSuccessResponse()
    {
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
    public void Run_ShutdownWithNoActiveTasks_IsGraceful()
    {
        var shutdownRequest = new WorkerRequest { Id = 1, Kind = WorkerRequestKind.Shutdown };
        string requestLine = JsonSerializer.Serialize(shutdownRequest);
        using var input = new StringReader(requestLine + "\n");
        using var output = new StringWriter();

        EmitWorkerHost.Run(input, output);

        WorkerResponse? response = JsonSerializer.Deserialize<WorkerResponse>(output.ToString().Trim());
        Assert.IsNotNull(response);
        Assert.IsTrue(response.ShutdownWasGraceful,
            "Shutdown with no active tasks must report ShutdownWasGraceful=true.");
    }

    [TestMethod]
    public void Run_EmptyInput_ReturnsWithoutThrowingOrHanging()
    {
        using var input = new StringReader("");
        using var output = new StringWriter();

        EmitWorkerHost.Run(input, output); // Must not throw or block.
    }

    // ─── Item 38: method token restricted to loaded interface ────────────────────
    // (Replaced by item 1's private method IDs — the following tests verify the new scheme.)

    [TestMethod]
    public void WorkerRequest_MethodId_IsUsedInsteadOfMetadataToken()
    {
        // Verify that WorkerRequest has a MethodId field (not MethodMetadataToken).
        // Metadata tokens are module-scoped and can collide when methods are inherited from another
        // assembly; private IDs are stable per loaded handle.
        var request = new WorkerRequest { Id = 1, Kind = WorkerRequestKind.Call, MethodId = 7 };
        Assert.AreEqual(7, request.MethodId);
    }

    // ─── Review #472 item 3: bounded active-task tracking ────────────────────────

    [TestMethod]
    public void Run_RequestAfterShutdown_WritesFailureResponse()
    {
        // After a Shutdown request, subsequent requests arriving before the loop exits should
        // be rejected (draining state). This tests that the admission logic is in place.
        // Since Run returns after the Shutdown response, concurrent requests from a very fast
        // producer may race, but the loop must not accept them after draining begins.
        var shutdownRequest = new WorkerRequest { Id = 1, Kind = WorkerRequestKind.Shutdown };
        string input = JsonSerializer.Serialize(shutdownRequest) + "\n";
        using var reader = new StringReader(input);
        using var writer = new StringWriter();

        // Run must return without hanging after processing the Shutdown.
        EmitWorkerHost.Run(reader, writer);

        string output = writer.ToString().Trim();
        WorkerResponse? response = JsonSerializer.Deserialize<WorkerResponse>(output);
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);
    }

    // ─── Item 11: protocol versioning and Hello handshake ────────────────────────

    [TestMethod]
    public void ProtocolVersion_IsConsistentBetweenHostAndProcess()
    {
        // Both sides declare the same constant to ensure an incompatibility is caught at
        // compile time, not only at runtime. Mismatching the values here is a build error.
        Assert.AreEqual(EmitWorkerHost.ProtocolVersion, EmitWorkerProcess.ProtocolVersion);
    }

    [TestMethod]
    public void WorkerRequest_Hello_CarriesProtocolVersion()
    {
        var request = new WorkerRequest
        {
            Id = 1,
            Kind = WorkerRequestKind.Hello,
            ProtocolVersion = EmitWorkerHost.ProtocolVersion,
        };

        string json = JsonSerializer.Serialize(request);
        WorkerRequest? deserialized = JsonSerializer.Deserialize<WorkerRequest>(json);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(WorkerRequestKind.Hello, deserialized.Kind);
        Assert.AreEqual(EmitWorkerHost.ProtocolVersion, deserialized.ProtocolVersion);
    }

    [TestMethod]
    public void WorkerResponse_Hello_CarriesProtocolVersion()
    {
        var response = new WorkerResponse
        {
            Id = 1,
            Success = true,
            ProtocolVersion = EmitWorkerHost.ProtocolVersion,
        };

        string json = JsonSerializer.Serialize(response);
        WorkerResponse? deserialized = JsonSerializer.Deserialize<WorkerResponse>(json);

        Assert.IsNotNull(deserialized);
        Assert.IsTrue(deserialized.Success);
        Assert.AreEqual(EmitWorkerHost.ProtocolVersion, deserialized.ProtocolVersion);
    }

    [TestMethod]
    public void Run_Hello_WithMatchingVersion_RespondsSuccessfully()
    {
        var helloRequest = new WorkerRequest
        {
            Id = 42,
            Kind = WorkerRequestKind.Hello,
            ProtocolVersion = EmitWorkerHost.ProtocolVersion,
        };
        var shutdownRequest = new WorkerRequest { Id = 99, Kind = WorkerRequestKind.Shutdown };

        string input = JsonSerializer.Serialize(helloRequest) + "\n"
                     + JsonSerializer.Serialize(shutdownRequest) + "\n";
        using var reader = new StringReader(input);
        using var writer = new StringWriter();

        EmitWorkerHost.Run(reader, writer);

        string[] lines = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        WorkerResponse? helloResponse = lines
            .Select(l => JsonSerializer.Deserialize<WorkerResponse>(l))
            .FirstOrDefault(r => r?.Id == 42);

        Assert.IsNotNull(helloResponse, "Hello response not found in output.");
        Assert.IsTrue(helloResponse.Success, helloResponse.ErrorMessage);
        Assert.AreEqual(EmitWorkerHost.ProtocolVersion, helloResponse.ProtocolVersion);
    }

    [TestMethod]
    public void Run_Hello_WithMismatchedVersion_RespondsWithFailure()
    {
        int wrongVersion = EmitWorkerHost.ProtocolVersion + 100;
        var helloRequest = new WorkerRequest
        {
            Id = 7,
            Kind = WorkerRequestKind.Hello,
            ProtocolVersion = wrongVersion,
        };

        // Send only the Hello; worker processes it and we check the failure response.
        // Then close the stream — Run exits on end-of-stream after processing the one request.
        string input = JsonSerializer.Serialize(helloRequest) + "\n";
        using var reader = new StringReader(input);
        using var writer = new StringWriter();

        EmitWorkerHost.Run(reader, writer);

        string[] lines = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        WorkerResponse? helloResponse = lines
            .Select(l => JsonSerializer.Deserialize<WorkerResponse>(l))
            .FirstOrDefault(r => r?.Id == 7);

        Assert.IsNotNull(helloResponse, "Hello response not found in output.");
        Assert.IsFalse(helloResponse.Success, "Worker must reject a mismatched protocol version.");
        StringAssert.Contains(helloResponse.ErrorMessage, wrongVersion.ToString());
    }

    // ─── Review #495: return-type comparison in FindMatchingMethod ───────────────

    [TestMethod]
    public void MethodDescriptorDto_FromMethodInfo_CapturesReturnType()
    {
        MethodInfo method = typeof(IMethodIdTestContract).GetMethod(nameof(IMethodIdTestContract.Format))!;
        var descriptor = MethodDescriptorDto.FromMethodInfo(0, method);

        // Return type must use the assembly-qualified stable name, not just FullName.
        Assert.AreEqual(typeof(string).AssemblyQualifiedName, descriptor.ReturnType,
            "ReturnType must be the assembly-qualified stable name of the return type.");
    }

    [TestMethod]
    public void MethodDescriptorDto_FromMethodInfo_VoidReturnType_UsesStableName()
    {
        // Void return type must also round-trip as a stable name.
        MethodInfo method = typeof(ILeaseTestContract).GetMethod(nameof(ILeaseTestContract.DoWork))!;
        var descriptor = MethodDescriptorDto.FromMethodInfo(0, method);

        Assert.AreEqual(typeof(void).AssemblyQualifiedName, descriptor.ReturnType,
            "void return type must be captured as its assembly-qualified stable name.");
    }
}
