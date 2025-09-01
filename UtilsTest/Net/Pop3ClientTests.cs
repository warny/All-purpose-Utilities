using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="Pop3Client"/>.
/// </summary>
[TestClass]
public class Pop3ClientTests
{
    /// <summary>
    /// Verifies basic POP3 interactions including authentication, listing and retrieving messages.
    /// </summary>
    [TestMethod]
    public async Task Pop3Client_BasicFlow()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream ns = serverClient.GetStream();
            using StreamWriter writer = new(ns) { NewLine = "\r\n", AutoFlush = true };
            using StreamReader reader = new(ns);
            await writer.WriteLineAsync("+OK POP3 ready");
            bool quit = false;
            while (!quit)
            {
                string? line = await reader.ReadLineAsync();
                if (line is null) break;
                if (line.StartsWith("USER"))
                {
                    await writer.WriteLineAsync("+OK user accepted");
                }
                else if (line.StartsWith("PASS"))
                {
                    await writer.WriteLineAsync("+OK pass accepted");
                }
                else if (line == "STAT")
                {
                    await writer.WriteLineAsync("+OK 2 40");
                }
                else if (line == "LIST")
                {
                    await writer.WriteLineAsync("+OK");
                    await writer.WriteLineAsync("1 20");
                    await writer.WriteLineAsync("2 20");
                    await writer.WriteLineAsync(".");
                }
                else if (line == "RETR 1")
                {
                    await writer.WriteLineAsync("+OK message follows");
                    await writer.WriteLineAsync("line1");
                    await writer.WriteLineAsync("..leading dot");
                    await writer.WriteLineAsync("line3");
                    await writer.WriteLineAsync(".");
                }
                else if (line == "DELE 1")
                {
                    await writer.WriteLineAsync("+OK deleted");
                }
                else if (line == "NOOP")
                {
                    await writer.WriteLineAsync("+OK");
                }
                else if (line == "QUIT")
                {
                    await writer.WriteLineAsync("+OK bye");
                    quit = true;
                }
                else
                {
                    await writer.WriteLineAsync("-ERR");
                }
            }
            listener.Stop();
        });

        using Pop3Client client = new() { NoOpInterval = TimeSpan.FromMilliseconds(100) };
        await client.ConnectAsync("127.0.0.1", port);
        await client.AuthenticateAsync("user", "pass");
        (int count, int size) stat = await client.GetStatAsync();
        Assert.AreEqual(2, stat.count);
        IReadOnlyDictionary<int, int> list = await client.ListAsync();
        Assert.AreEqual(2, list.Count);
        string message = await client.RetrieveAsync(1);
        StringAssert.Contains(message, "line1");
        StringAssert.Contains(message, ".leading dot");
        await client.DeleteAsync(1);
        await client.NoOpAsync();
        await client.QuitAsync();
        await serverTask;
    }

    /// <summary>
    /// Ensures that the POP3 client preserves server response codes without numeric conversion.
    /// </summary>
    [TestMethod]
    public async Task Pop3Client_RawCodes()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream ns = serverClient.GetStream();
            using StreamWriter writer = new(ns) { NewLine = "\r\n", AutoFlush = true };
            using StreamReader reader = new(ns);
            await writer.WriteLineAsync("+OK ready");
            bool quit = false;
            while (!quit && await reader.ReadLineAsync() is string line)
            {
                switch (line)
                {
                    case "NOOP":
                        await writer.WriteLineAsync("+OK");
                        break;
                    case "FAIL":
                        await writer.WriteLineAsync("-ERR failure");
                        break;
                    case "QUIT":
                        await writer.WriteLineAsync("+OK bye");
                        quit = true;
                        break;
                    default:
                        await writer.WriteLineAsync("-ERR");
                        break;
                }
            }
            listener.Stop();
        });

        using Pop3Client client = new();
        await client.ConnectAsync("127.0.0.1", port);
        IReadOnlyList<ServerResponse> ok = await client.SendCommandAsync("NOOP");
        Assert.AreEqual("+OK", ok[0].Code);
        IReadOnlyList<ServerResponse> err = await client.SendCommandAsync("FAIL");
        Assert.AreEqual("-ERR", err[0].Code);
        await client.QuitAsync();
        await serverTask;
    }
}
