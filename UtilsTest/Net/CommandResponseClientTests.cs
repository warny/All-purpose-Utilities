using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="CommandResponseClient"/>.
/// </summary>
[TestClass]
public class CommandResponseClientTests
{
    /// <summary>
    /// Verifies that multiple responses are read until a final status is received.
    /// </summary>
    [TestMethod]
    public async Task SendCommandAsync_ReadsMultipleResponses()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            server.CommandReceived += cmd =>
                Task.FromResult<IEnumerable<ServerResponse>>(cmd == "MULTI"
                    ? new[] { new ServerResponse(100, "Continue"), new ServerResponse(200, "Done") }
                    : System.Array.Empty<ServerResponse>());
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        IReadOnlyList<ServerResponse> responses;
        using (CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan })
        {
            await client.ConnectAsync("127.0.0.1", port);
            responses = await client.SendCommandAsync("MULTI");
            Assert.AreEqual(2, responses.Count);
            Assert.AreEqual(100, responses[0].Code);
            Assert.AreEqual(200, responses[1].Code);
        }
        await serverTask;
    }

    /// <summary>
    /// Verifies that a no-op command is sent after inactivity.
    /// </summary>
    [TestMethod]
    public async Task Client_SendsNoOp_WhenIdle()
    {
        string? received = null;
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            server.CommandReceived += cmd =>
            {
                received = cmd;
                return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse(200, "OK") });
            };
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using (CommandResponseClient client = new()
        {
            NoOpCommand = "NOOP",
            NoOpInterval = TimeSpan.FromMilliseconds(100)
        })
        {
            await client.ConnectAsync("127.0.0.1", port);
            await Task.Delay(300);
        }
        await serverTask;
        Assert.AreEqual("NOOP", received);
    }

    /// <summary>
    /// Verifies that the server processes commands sent before previous responses are fully transmitted.
    /// </summary>
    [TestMethod]
    public async Task Server_ProcessesPipelinedCommands()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            server.CommandReceived += async cmd =>
            {
                if (cmd == "FIRST")
                {
                    await Task.Delay(200);
                }
                return new[] { new ServerResponse(200, cmd) };
            };
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using StreamWriter writer = new(ns) { NewLine = "\r\n", AutoFlush = true };
        using StreamReader reader = new(ns);

        await writer.WriteLineAsync("FIRST");
        await writer.WriteLineAsync("SECOND");

        string? response1 = await reader.ReadLineAsync();
        string? response2 = await reader.ReadLineAsync();

        Assert.AreEqual("200 FIRST", response1);
        Assert.AreEqual("200 SECOND", response2);

        client.Close();
        await serverTask;
    }
}

