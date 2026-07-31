using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Verifies that the failure collections on <see cref="DnsLookupException"/> and
/// <see cref="NtpQueryException"/> are defensively copied and cannot be mutated
/// by callers who hold a reference to the original list.
/// </summary>
[TestClass]
public class ExceptionImmutabilityTests
{
    [TestMethod]
    public void DnsLookupException_CopiesFailureCollection()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("192.0.2.1"), 53);
        var failure = new DnsServerFailure(endpoint, DnsTransport.Udp, DnsFailureKind.Timeout, null, "timeout");
        var mutable = new List<DnsServerFailure> { failure };

        var ex = new DnsLookupException(mutable);

        // Mutate the original list — the exception should not be affected.
        mutable.Clear();

        Assert.AreEqual(1, ex.Failures.Count);
        Assert.AreSame(failure, ex.Failures[0]);
    }

    [TestMethod]
    public void NtpQueryException_CopiesFailureCollection()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("203.0.113.1"), 123);
        var failure = new NtpEndpointFailure(
            endpoint,
            System.Net.Sockets.AddressFamily.InterNetwork,
            NtpPhase.Exchange,
            null,
            "timed out");
        var mutable = new List<NtpEndpointFailure> { failure };

        var ex = new NtpQueryException("all failed", mutable);

        // Mutate the original list — the exception should not be affected.
        mutable.Clear();

        Assert.AreEqual(1, ex.Failures.Count);
        Assert.AreSame(failure, ex.Failures[0]);
    }
}
