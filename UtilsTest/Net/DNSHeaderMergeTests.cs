using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;

namespace UtilsTest.Net;

[TestClass]
public class DNSHeaderMergeTests
{
    private static DNSResponseRecord ARecord(string name, string ip)
        => new(name, 300, new Address { IPAddress = IPAddress.Parse(ip) });

    private static DNSHeader ResponseWithQuestion(string name = "example.com")
    {
        var header = new DNSHeader { QrBit = DNSQRBit.Response };
        header.Requests.Add(new DNSRequestRecord("A", name));
        return header;
    }

    [TestMethod]
    public void MergeRecordsFrom_Null_Throws()
    {
        var header = ResponseWithQuestion();
        Assert.ThrowsException<ArgumentNullException>(() => header.MergeRecordsFrom(null!));
    }

    [TestMethod]
    public void MergeRecordsFrom_CompatibleHeaders_MergesDistinctRecords()
    {
        var a = ResponseWithQuestion();
        a.Responses.Add(ARecord("a.example.com", "192.0.2.1"));

        var b = ResponseWithQuestion();
        b.Responses.Add(ARecord("b.example.com", "192.0.2.2"));

        a.MergeRecordsFrom(b);
        Assert.AreEqual(2, a.Responses.Count);
    }

    [TestMethod]
    public void MergeRecordsFrom_DuplicateRecords_DoesNotDuplicate()
    {
        var a = ResponseWithQuestion();
        a.Responses.Add(ARecord("example.com", "192.0.2.1"));

        var b = ResponseWithQuestion();
        b.Responses.Add(ARecord("example.com", "192.0.2.1"));

        a.MergeRecordsFrom(b);
        Assert.AreEqual(1, a.Responses.Count);
    }

    [TestMethod]
    public void MergeRecordsFrom_ClonesRecords()
    {
        var a = ResponseWithQuestion();
        var b = ResponseWithQuestion();
        var record = ARecord("example.com", "192.0.2.9");
        b.Responses.Add(record);

        a.MergeRecordsFrom(b);
        Assert.AreNotSame(record, a.Responses[0]);
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentQuestions_Throws()
    {
        var a = ResponseWithQuestion("example.com");
        var b = ResponseWithQuestion("other.com");
        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentOpcode_Throws()
    {
        var a = ResponseWithQuestion();
        var b = ResponseWithQuestion();
        b.OpCode = DNSOpCode.Inverse;
        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_QueryAndResponse_Throws()
    {
        var a = ResponseWithQuestion();          // Response
        var b = ResponseWithQuestion();
        b.QrBit = DNSQRBit.Question;              // Question
        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentErrorCode_Throws()
    {
        var a = ResponseWithQuestion();
        a.ErrorCode = DNSError.Ok;
        var b = ResponseWithQuestion();
        b.ErrorCode = DNSError.ServerFailure;

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentAuthoritativeAnswer_Throws()
    {
        var a = ResponseWithQuestion();
        a.AuthoritativeAnswer = true;
        var b = ResponseWithQuestion();
        b.AuthoritativeAnswer = false;

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentMessageTruncated_Throws()
    {
        var a = ResponseWithQuestion();
        a.MessageTruncated = false;
        var b = ResponseWithQuestion();
        b.MessageTruncated = true;

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentAuthenticDatas_Throws()
    {
        var a = ResponseWithQuestion();
        a.AuthenticDatas = true;
        var b = ResponseWithQuestion();
        b.AuthenticDatas = false;

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentCheckingDisabled_Throws()
    {
        var a = ResponseWithQuestion();
        a.CheckingDisabled = false;
        var b = ResponseWithQuestion();
        b.CheckingDisabled = true;

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_AllFlagsIdentical_MergesRecords()
    {
        // When all checked flags are identical, the merge must succeed.
        var a = ResponseWithQuestion();
        a.ErrorCode = DNSError.Ok;
        a.AuthoritativeAnswer = true;
        a.MessageTruncated = false;
        a.AuthenticDatas = false;
        a.CheckingDisabled = false;
        a.Responses.Add(ARecord("a.example.com", "192.0.2.1"));

        var b = ResponseWithQuestion();
        b.ErrorCode = DNSError.Ok;
        b.AuthoritativeAnswer = true;
        b.MessageTruncated = false;
        b.AuthenticDatas = false;
        b.CheckingDisabled = false;
        b.Responses.Add(ARecord("b.example.com", "192.0.2.2"));

        a.MergeRecordsFrom(b);

        Assert.AreEqual(2, a.Responses.Count);
    }

    [TestMethod]
    public void MergeRecordsFrom_DoesNotOverwriteId()
    {
        var a = ResponseWithQuestion();
        ushort originalId = a.ID;

        var b = ResponseWithQuestion();

        a.MergeRecordsFrom(b);

        Assert.AreEqual(originalId, a.ID);
    }

    [TestMethod]
    public void Append_CompatibilityBehavior_IsDocumentedAndTested()
    {
        // The obsolete Append still merges distinct records and rejects a shared ID.
        var a = ResponseWithQuestion();
        a.ID = 1;
        a.Responses.Add(ARecord("a.example.com", "192.0.2.1"));

        var b = ResponseWithQuestion();
        b.ID = 2;
        b.Responses.Add(ARecord("b.example.com", "192.0.2.2"));

#pragma warning disable CS0618 // testing the obsolete member on purpose
        a.Append(b);

        var same = ResponseWithQuestion();
        same.ID = 1;
        Assert.ThrowsException<InvalidOperationException>(() => a.Append(same));
#pragma warning restore CS0618

        Assert.AreEqual(2, a.Responses.Count);
    }
}
