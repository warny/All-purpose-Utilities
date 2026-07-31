using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using Utils.Net;

namespace UtilsTest.Net;

[TestClass]
public class NetworkParametersDiscoveryTests
{
    private sealed class FakeGateway : GatewayIPAddressInformation
    {
        private readonly IPAddress _address;
        public FakeGateway(IPAddress address) => _address = address;
        public override IPAddress Address => _address;
    }

    private static IReadOnlyList<GatewayIPAddressInformation> Gateways(params string[] addresses)
    {
        var list = new List<GatewayIPAddressInformation>();
        foreach (var a in addresses)
            list.Add(new FakeGateway(IPAddress.Parse(a)));
        return list;
    }

    private static IReadOnlyList<IPAddress> Dns(params string[] addresses)
    {
        var list = new List<IPAddress>();
        foreach (var a in addresses)
            list.Add(IPAddress.Parse(a));
        return list;
    }

    private static NetworkInterfaceSnapshot Up(int? metric, IReadOnlyList<IPAddress> dns, IReadOnlyList<GatewayIPAddressInformation> gateways)
        => new(OperationalStatus.Up, metric, dns, gateways);

    [TestMethod]
    public void DiscoverDns_UpInterfaceWithoutGateway_IsIncluded()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(10, Dns("192.0.2.53"), Gateways()),
        });

        CollectionAssert.Contains(result, IPAddress.Parse("192.0.2.53"));
    }

    [TestMethod]
    public void DiscoverDns_VpnInterfaceWithoutGateway_IsIncluded()
    {
        // A VPN interface (no gateway) still contributes its resolver.
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(5, Dns("10.10.0.53"), Gateways()),
        });

        CollectionAssert.Contains(result, IPAddress.Parse("10.10.0.53"));
    }

    [TestMethod]
    public void DiscoverDns_DownInterface_IsExcluded()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            new NetworkInterfaceSnapshot(OperationalStatus.Down, 1, Dns("192.0.2.53"), Gateways("192.0.2.1")),
        });

        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public void DiscoverDns_Duplicates_AreRemovedPreservingPriority()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(10, Dns("192.0.2.53", "192.0.2.54"), Gateways("192.0.2.1")), // gateway → priority
            Up(5, Dns("192.0.2.53"), Gateways()),                            // no gateway, duplicate
        });

        CollectionAssert.AreEqual(
            new[] { IPAddress.Parse("192.0.2.53"), IPAddress.Parse("192.0.2.54") },
            result);
    }

    [TestMethod]
    public void DiscoverDns_AnyAndMulticast_AreExcluded()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(10, Dns("0.0.0.0", "224.0.0.251", "192.0.2.53"), Gateways("192.0.2.1")),
        });

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.53") }, result);
    }

    [TestMethod]
    public void DiscoverDns_Ipv4AndIpv6Resolvers_AreAccepted()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(10, Dns("192.0.2.53", "2001:db8::53"), Gateways("192.0.2.1")),
        });

        CollectionAssert.AreEqual(
            new[] { IPAddress.Parse("192.0.2.53"), IPAddress.Parse("2001:db8::53") },
            result);
    }

    [TestMethod]
    public void DiscoverDns_NoServers_ReturnsEmptySnapshotAndNullPrimaryDns()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(10, Dns(), Gateways("192.0.2.1")),
        });

        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public void PrimaryDns_EqualsFirstOrderedDns()
    {
        // Interface with a gateway is preferred over one without, so its DNS is first.
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(5, Dns("10.0.0.53"), Gateways()),                  // no gateway
            Up(10, Dns("192.0.2.53"), Gateways("192.0.2.1")),    // gateway → wins
        });

        Assert.AreEqual(IPAddress.Parse("192.0.2.53"), result[0]);
    }

    [TestMethod]
    public void DnsServers_GetterReturnsDefensiveCopy()
    {
        var parameters = new NetworkParameters();
        IPAddress[] first = parameters.DnsServers;
        IPAddress[] second = parameters.DnsServers;
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public void SelectDnsServers_OrdersByMetricThenOsOrder()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(20, Dns("192.0.2.20"), Gateways("192.0.2.1")),
            Up(10, Dns("192.0.2.10"), Gateways("192.0.2.1")),
        });

        // Lower metric first.
        CollectionAssert.AreEqual(
            new[] { IPAddress.Parse("192.0.2.10"), IPAddress.Parse("192.0.2.20") },
            result);
    }
}
