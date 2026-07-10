using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Runs inside the isolated Emit worker process. Reads <see cref="WorkerRequest"/> lines from
/// <see cref="TextReader"/>, performs the requested load/call/shutdown, and writes back
/// <see cref="WorkerResponse"/> lines.
/// </summary>
internal static class EmitWorkerHost
{
    /// <summary>
    /// Runs the request/response loop until a <see cref="WorkerRequestKind.Shutdown"/> request is
    /// received or the input stream ends.
    /// </summary>
    /// <param name="input">Reader for incoming JSON-line requests.</param>
    /// <param name="output">Writer for outgoing JSON-line responses.</param>
    internal static void Run(TextReader input, TextWriter output)
    {
        object? nativeInstance = null;
        Type? interfaceType = null;

        string? line;
        while ((line = input.ReadLine()) is not null)
        {
            WorkerRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<WorkerRequest>(line);
            }
            catch (JsonException)
            {
                // A malformed line should never happen with a well-behaved host; ignore it rather
                // than tearing down the worker over a single corrupted message.
                continue;
            }

            if (request is null)
            {
                continue;
            }

            if (request.Kind == WorkerRequestKind.Shutdown)
            {
                WriteResponse(output, new WorkerResponse { Id = request.Id, Success = true });
                return;
            }

            WorkerResponse response;
            try
            {
                response = request.Kind switch
                {
                    WorkerRequestKind.Load => HandleLoad(request, out nativeInstance, out interfaceType),
                    WorkerRequestKind.Call => HandleCall(
                        interfaceType ?? throw new InvalidOperationException("Received a Call request before a successful Load request."),
                        nativeInstance!,
                        request),
                    _ => throw new InvalidOperationException($"Unknown worker request kind '{request.Kind}'."),
                };
            }
            catch (Exception ex)
            {
                Exception effective = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                response = new WorkerResponse
                {
                    Id = request.Id,
                    Success = false,
                    ErrorMessage = effective.Message,
                    ErrorTypeName = effective.GetType().FullName,
                };
            }

            WriteResponse(output, response);
        }
    }

    /// <summary>
    /// Loads the interface's declaring assembly, validates it can be marshaled across the process
    /// boundary, and emits/wires the native mapping instance.
    /// </summary>
    private static WorkerResponse HandleLoad(WorkerRequest request, out object nativeInstance, out Type interfaceType)
    {
        Assembly interfaceAssembly = Assembly.LoadFrom(
            request.InterfaceAssemblyPath ?? throw new InvalidOperationException("Load request is missing the interface assembly path."));

        interfaceType = interfaceAssembly.GetType(
            request.InterfaceTypeFullName ?? throw new InvalidOperationException("Load request is missing the interface type name."),
            throwOnError: true)!;

        CrossProcessMarshaling.EnsureInterfaceIsSupported(interfaceType);

        // Executed inside an isolated worker process: even if a hostile interface definition were
        // to inject code through crafted member names (see EmitDllMappableClass), the blast radius
        // is contained by the process-container permissions this worker was launched with.
#pragma warning disable UTILSREFL001
        nativeInstance = LibraryMapper.EmitCore(
            interfaceType,
            request.DllPath ?? throw new InvalidOperationException("Load request is missing the native DLL path."),
            request.CallingConvention);
#pragma warning restore UTILSREFL001

        return new WorkerResponse { Id = request.Id, Success = true };
    }

    /// <summary>
    /// Resolves the requested interface method by metadata token and invokes it on the native
    /// mapping instance, round-tripping arguments and the return value as JSON.
    /// </summary>
    private static WorkerResponse HandleCall(Type interfaceType, object nativeInstance, WorkerRequest request)
    {
        var method = (MethodInfo)interfaceType.Module.ResolveMethod(request.MethodMetadataToken);
        ParameterInfo[] parameters = method.GetParameters();
        string?[] argumentsJson = request.ArgumentsJson ?? [];
        object?[] arguments = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            Type effectiveType = parameterType.IsByRef ? parameterType.GetElementType()! : parameterType;
            string? argumentJson = i < argumentsJson.Length ? argumentsJson[i] : null;
            arguments[i] = argumentJson is null ? null : JsonSerializer.Deserialize(argumentJson, effectiveType, CrossProcessMarshaling.JsonOptions);
        }

        object? result = method.Invoke(nativeInstance, arguments);

        string?[] byRefValuesJson = new string?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType.IsByRef)
            {
                Type effectiveType = parameters[i].ParameterType.GetElementType()!;
                byRefValuesJson[i] = JsonSerializer.Serialize(arguments[i], effectiveType, CrossProcessMarshaling.JsonOptions);
            }
        }

        return new WorkerResponse
        {
            Id = request.Id,
            Success = true,
            ReturnValueJson = method.ReturnType == typeof(void) ? null : JsonSerializer.Serialize(result, method.ReturnType, CrossProcessMarshaling.JsonOptions),
            ByRefValuesJson = byRefValuesJson,
        };
    }

    private static void WriteResponse(TextWriter output, WorkerResponse response)
    {
        output.WriteLine(JsonSerializer.Serialize(response));
        output.Flush();
    }
}
