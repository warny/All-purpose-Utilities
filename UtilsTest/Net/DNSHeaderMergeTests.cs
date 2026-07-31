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
    public void MergeRecordsFrom_DifferentRecursionDesired_Throws()
    {
        var a = ResponseWithQuestion();
        a.RecursionDesired = true;
        var b = ResponseWithQuestion();
        b.RecursionDesired = false;

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentRecursionPossible_Throws()
    {
        var a = ResponseWithQuestion();
        a.RecursionPossible = true;
        var b = ResponseWithQuestion();
        b.RecursionPossible = false;

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentReservedFlags_Throws()
    {
        // DNSConstants.ReservedZ == 0x0040. The setter applies the mask, so we must pass 0x40
        // (or any byte with bit 6 set) to get a non-zero effective ReservedFlags value.
        var a = ResponseWithQuestion();
        a.ReservedFlags = 0;
        var b = ResponseWithQuestion();
        b.ReservedFlags = 0x40; // bit 6 survives the ReservedZ mask (0x0040)

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
        a.RecursionDesired = true;
        a.RecursionPossible = true;
        a.ReservedFlags = 0;
        a.Responses.Add(ARecord("a.example.com", "192.0.2.1"));

        var b = ResponseWithQuestion();
        b.ErrorCode = DNSError.Ok;
        b.AuthoritativeAnswer = true;
        b.MessageTruncated = false;
        b.AuthenticDatas = false;
        b.CheckingDisabled = false;
        b.RecursionDesired = true;
        b.RecursionPossible = true;
        b.ReservedFlags = 0;
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
    public void MergeRecordsFrom_DifferentQuestionType_Throws()
    {
        var reqA = new DNSRequestRecord("A", "example.com");
        reqA.RequestType = 1;   // A record numeric type
        var a = new DNSHeader { QrBit = DNSQRBit.Response };
        a.Requests.Add(reqA);

        var reqB = new DNSRequestRecord("AAAA", "example.com");
        reqB.RequestType = 28;  // AAAA record numeric type
        var b = new DNSHeader { QrBit = DNSQRBit.Response };
        b.Requests.Add(reqB);

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentQuestionClass_Throws()
    {
        var a = new DNSHeader { QrBit = DNSQRBit.Response };
        a.Requests.Add(new DNSRequestRecord("A", "example.com", DNSClassId.IN));

        var b = new DNSHeader { QrBit = DNSQRBit.Response };
        b.Requests.Add(new DNSRequestRecord("A", "example.com", DNSClassId.ALL));

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentQuestionOrder_Throws()
    {
        var q1 = new DNSRequestRecord("A", "alpha.example.com");
        var q2 = new DNSRequestRecord("A", "beta.example.com");

        var a = new DNSHeader { QrBit = DNSQRBit.Response };
        a.Requests.Add(q1);
        a.Requests.Add(q2);

        var b = new DNSHeader { QrBit = DNSQRBit.Response };
        b.Requests.Add((DNSRequestRecord)q2.Clone());
        b.Requests.Add((DNSRequestRecord)q1.Clone());

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
    }

    [TestMethod]
    public void MergeRecordsFrom_DifferentId_IsAllowed()
    {
        var a = ResponseWithQuestion();
        a.ID = 100;

        var b = ResponseWithQuestion();
        b.ID = 200;
        b.Responses.Add(ARecord("b.example.com", "192.0.2.2"));

        // Different IDs must not prevent the merge.
        a.MergeRecordsFrom(b);

        Assert.AreEqual(1, a.Responses.Count);
    }

    [TestMethod]
    public void MergeRecordsFrom_DoesNotModifySource()
    {
        var a = ResponseWithQuestion();
        a.Responses.Add(ARecord("a.example.com", "192.0.2.1"));

        var b = ResponseWithQuestion();
        b.Responses.Add(ARecord("b.example.com", "192.0.2.2"));
        int sourceCountBefore = b.Responses.Count;

        a.MergeRecordsFrom(b);

        Assert.AreEqual(sourceCountBefore, b.Responses.Count);
    }

    [TestMethod]
    public void MergeRecordsFrom_Failure_DoesNotModifyTarget()
    {
        var a = ResponseWithQuestion("example.com");
        a.Responses.Add(ARecord("a.example.com", "192.0.2.1"));
        int countBefore = a.Responses.Count;

        // Incompatible question section → merge must throw.
        var b = ResponseWithQuestion("other.com");
        b.Responses.Add(ARecord("b.example.com", "192.0.2.2"));

        Assert.ThrowsException<InvalidOperationException>(() => a.MergeRecordsFrom(b));
        Assert.AreEqual(countBefore, a.Responses.Count);
    }

    [TestMethod]
    public void MergeRecordsFrom_PreservesTargetIdAndFlags()
    {
        var a = ResponseWithQuestion();
        a.ID = 42;
        a.AuthoritativeAnswer = true;
        a.RecursionDesired = true;
        ushort originalId = a.ID;

        var b = ResponseWithQuestion();
        b.ID = 99;
        b.AuthoritativeAnswer = true;
        b.RecursionDesired = true;
        b.Responses.Add(ARecord("b.example.com", "192.0.2.2"));

        a.MergeRecordsFrom(b);

        Assert.AreEqual(originalId, a.ID, "ID must be preserved after merge.");
        Assert.IsTrue(a.AuthoritativeAnswer, "AuthoritativeAnswer flag must be preserved.");
        Assert.IsTrue(a.RecursionDesired, "RecursionDesired flag must be preserved.");
    }

}
