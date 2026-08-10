using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Utils.Data.Sql;
using Utils.IO.Serialization;
using Utils.Net;
using Utils.Objects;

namespace UtilsTest.Immutability;

/// <summary>
/// Verifies defensive collection snapshots used by immutable types outside the parser projects.
/// </summary>
[TestClass]
public sealed class NonParserImmutabilityTests
{
    /// <summary>Verifies that SMTP recipients are detached from array and list sources.</summary>
    [TestMethod]
    public void SmtpMessage_RecipientsAreImmutableSnapshot()
    {
        string[] array = ["first@example.com", "second@example.com"];
        var message = new SmtpMessage("sender@example.com", array, "data");
        array[0] = "changed@example.com";

        var list = new List<string> { "list@example.com" };
        var fromList = new SmtpMessage("sender@example.com", list, "data");
        list.Clear();

        CollectionAssert.AreEqual(
            new[] { "first@example.com", "second@example.com" },
            message.Recipients.ToArray());
        Assert.AreEqual("list@example.com", fromList.Recipients[0]);
        Assert.IsFalse(message.Recipients is string[]);
        Assert.IsFalse(fromList.Recipients is List<string>);
    }

    /// <summary>Verifies that record cloning normalizes replacement SMTP recipients.</summary>
    [TestMethod]
    public void SmtpMessage_WithRecipientsCreatesImmutableSnapshot()
    {
        var original = new SmtpMessage("sender@example.com", ["original@example.com"], "data");
        string[] replacement = ["replacement@example.com"];

        SmtpMessage copy = original with { Recipients = replacement };
        replacement[0] = "changed@example.com";

        Assert.AreEqual("replacement@example.com", copy.Recipients[0]);
        Assert.IsFalse(copy.Recipients is string[]);
    }

    /// <summary>Verifies that DNS failures retain source order without exposing mutable storage.</summary>
    [TestMethod]
    public void DnsLookupException_FailuresAreImmutableSnapshot()
    {
        DnsServerFailure first = DnsFailure("192.0.2.1");
        DnsServerFailure second = DnsFailure("192.0.2.2");
        var source = new List<DnsServerFailure> { first, second };

        var exception = new DnsLookupException(source);
        source.Clear();

        CollectionAssert.AreEqual(new[] { first, second }, exception.Failures.ToArray());
        Assert.IsFalse(exception.Failures is DnsServerFailure[]);
        StringAssert.Contains(exception.Message, "2 configured DNS server attempt(s)");
    }

    /// <summary>Verifies that NTP failures retain source order without exposing mutable storage.</summary>
    [TestMethod]
    public void NtpQueryException_FailuresAreImmutableSnapshot()
    {
        NtpEndpointFailure first = NtpFailure("192.0.2.1");
        NtpEndpointFailure second = NtpFailure("192.0.2.2");
        NtpEndpointFailure[] source = [first, second];

        var exception = new NtpQueryException("query failed", source);
        source[0] = NtpFailure("192.0.2.3");

        CollectionAssert.AreEqual(new[] { first, second }, exception.Failures.ToArray());
        Assert.IsFalse(exception.Failures is NtpEndpointFailure[]);
        Assert.AreEqual("query failed", exception.Message);
    }

    /// <summary>Verifies that SQL identifier prefixes cannot be mutated through either source or getter.</summary>
    [TestMethod]
    public void SqlSyntaxOptions_IdentifierPrefixesAreImmutableSnapshot()
    {
        var source = new HashSet<char> { '@', ':' };
        var options = new SqlSyntaxOptions(source, '@');
        source.Clear();

        Assert.IsTrue(options.IsIdentifierPrefix('@'));
        Assert.IsTrue(options.IsIdentifierPrefix(':'));
        Assert.IsFalse(options.IdentifierPrefixes is HashSet<char>);
    }

    /// <summary>Verifies that network-interface snapshots normalize construction and with-expression inputs.</summary>
    [TestMethod]
    public void NetworkInterfaceSnapshot_DnsAddressesAreImmutableSnapshot()
    {
        IPAddress first = IPAddress.Parse("192.0.2.1");
        IPAddress second = IPAddress.Parse("192.0.2.2");
        IPAddress[] source = [first];
        var snapshot = new NetworkInterfaceSnapshot(OperationalStatus.Up, source);
        source[0] = second;

        IPAddress[] replacement = [first];
        NetworkInterfaceSnapshot copy = snapshot with { DnsAddresses = replacement };
        replacement[0] = second;

        Assert.AreEqual(first, snapshot.DnsAddresses[0]);
        Assert.AreEqual(first, copy.DnsAddresses[0]);
        Assert.IsFalse(snapshot.DnsAddresses is IPAddress[]);
        Assert.IsFalse(copy.DnsAddresses is IPAddress[]);
    }

    /// <summary>Verifies that reflection contracts detach and normalize ordered member collections.</summary>
    [TestMethod]
    public void ReflectionSerializationContract_MembersAreImmutableSnapshot()
    {
        var first = new SerializableMemberContract(typeof(ContractModel).GetProperty(nameof(ContractModel.First))!, typeof(int), 1);
        var second = new SerializableMemberContract(typeof(ContractModel).GetProperty(nameof(ContractModel.Second))!, typeof(int), 2);
        var source = new List<SerializableMemberContract> { first, second };
        var contract = new ReflectionSerializationContract(typeof(ContractModel), source);
        source.Clear();

        SerializableMemberContract[] replacement = [second, first];
        ReflectionSerializationContract copy = contract with { Members = replacement };
        replacement[0] = first;

        CollectionAssert.AreEqual(new[] { first, second }, contract.Members.ToArray());
        CollectionAssert.AreEqual(new[] { second, first }, copy.Members.ToArray());
        Assert.IsFalse(contract.Members is SerializableMemberContract[]);
        Assert.IsFalse(copy.Members is SerializableMemberContract[]);
    }

    /// <summary>Verifies that conversion from an array and conversion back do not alias a Bytes value.</summary>
    [TestMethod]
    public void Bytes_ArrayConversionsAreDefensive()
    {
        byte[] source = [1, 2, 3];
        Bytes value = source;
        source[0] = 9;

        byte[] copy = value.ToArray();
        copy[0] = 8;

        Assert.AreEqual((byte)1, value[0]);
    }

    /// <summary>Verifies the existing empty, null, and default Bytes contracts.</summary>
    [TestMethod]
    public void Bytes_EmptyNullAndDefaultRemainEmpty()
    {
        byte[] nullSource = null!;
        Bytes fromNull = nullSource;
        Bytes fromEmpty = System.Array.Empty<byte>();
        Bytes defaultValue = default;

        Assert.AreEqual(0, fromNull.Count);
        Assert.AreEqual(0, fromEmpty.Count);
        Assert.AreEqual(0, defaultValue.Count);
        Assert.AreEqual(0, defaultValue.ToArray().Length);
    }

    /// <summary>Creates a deterministic DNS failure for collection tests.</summary>
    private static DnsServerFailure DnsFailure(string address) =>
        new(new IPEndPoint(IPAddress.Parse(address), 53), DnsTransport.Udp, DnsFailureKind.Timeout, null, "timeout");

    /// <summary>Creates a deterministic NTP failure for collection tests.</summary>
    private static NtpEndpointFailure NtpFailure(string address) =>
        new(new IPEndPoint(IPAddress.Parse(address), 123), AddressFamily.InterNetwork, NtpPhase.Exchange, null, "failure");

    /// <summary>Provides reflected members for serialization contract tests.</summary>
    private sealed class ContractModel
    {
        /// <summary>Gets or sets the first value.</summary>
        public int First { get; set; }

        /// <summary>Gets or sets the second value.</summary>
        public int Second { get; set; }
    }
}
