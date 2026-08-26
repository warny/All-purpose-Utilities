using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using Utils.Net;

namespace UtilsTest.Net;

[TestClass]
public class NetworkParametersDiscoveryTests
{
    private static IReadOnlyList<IPAddress> Dns(params string[] addresses)
    {
        var list = new List<IPAddress>();
        foreach (var a in addresses)
            list.Add(IPAddress.Parse(a));
        return list;
    }

    private static NetworkInterfaceSnapshot Up(IReadOnlyList<IPAddress> dns)
        => new(OperationalStatus.Up, dns);

    [TestMethod]
    public void DiscoverDns_UpInterfaceWithoutGateway_IsIncluded()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(Dns("192.0.2.53")),
        });

        CollectionAssert.Contains(result, IPAddress.Parse("192.0.2.53"));
    }

    [TestMethod]
    public void DiscoverDns_VpnInterfaceWithoutGateway_IsIncluded()
    {
        // A VPN interface (no gateway) still contributes its resolver.
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(Dns("10.10.0.53")),
        });

        CollectionAssert.Contains(result, IPAddress.Parse("10.10.0.53"));
    }

    [TestMethod]
    public void DiscoverDns_DownInterface_IsExcluded()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            new NetworkInterfaceSnapshot(OperationalStatus.Down, Dns("192.0.2.53")),
        });

        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public void DiscoverDns_Duplicates_AreRemovedPreservingOsOrder()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(Dns("192.0.2.53", "192.0.2.54")), // first interface
            Up(Dns("192.0.2.53")),                // duplicate from second interface
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
            Up(Dns("0.0.0.0", "224.0.0.251", "192.0.2.53")),
        });

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.53") }, result);
    }

    [TestMethod]
    public void DiscoverDns_Ipv4AndIpv6Resolvers_AreAccepted()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(Dns("192.0.2.53", "2001:db8::53")),
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
            Up(Dns()),
        });

        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public void PrimaryDns_EqualsFirstDnsInOsEnumerationOrder()
    {
        // OS enumeration order is preserved: the first interface's DNS comes first.
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(Dns("10.0.0.53")),     // listed first → its DNS is first
            Up(Dns("192.0.2.53")),    // listed second
        });

        Assert.AreEqual(IPAddress.Parse("10.0.0.53"), result[0]);
    }

    /// <summary>
    /// Verifies that <see cref="NetworkParameters.NetworkInterfaces"/> returns the same cached
    /// <see cref="IReadOnlyList{T}"/> instance on every call.
    /// </summary>
    [TestMethod]
    public void NetworkInterfaces_GetterReturnsReadOnlyView()
    {
        var parameters = new NetworkParameters();
        IReadOnlyList<NetworkInterface> first = parameters.NetworkInterfaces;
        IReadOnlyList<NetworkInterface> second = parameters.NetworkInterfaces;

        Assert.IsNotNull(first);
        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void SelectDnsServers_PreservesOsEnumerationOrder()
    {
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(Dns("192.0.2.20")), // first in OS order
            Up(Dns("192.0.2.10")), // second in OS order
        });

        // OS enumeration order is strictly preserved regardless of any routing attributes.
        CollectionAssert.AreEqual(
            new[] { IPAddress.Parse("192.0.2.20"), IPAddress.Parse("192.0.2.10") },
            result);
    }

    [TestMethod]
    public void SelectDnsServers_NoMetricOrGateway_PreservesOsEnumerationOrder()
    {
        // When no routing attributes are available, OS enumeration order is preserved
        // for all interfaces regardless of whether they have a default gateway.
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(Dns("192.0.2.1")), // first in OS order
            Up(Dns("192.0.2.2")), // second in OS order
        });

        // First-seen order preserved.
        CollectionAssert.AreEqual(
            new[] { IPAddress.Parse("192.0.2.1"), IPAddress.Parse("192.0.2.2") },
            result);
    }

    [TestMethod]
    public void SelectDnsServers_GatewaylessInterface_IncludedInOsOrder()
    {
        // Interfaces without a default gateway are included in OS enumeration order —
        // no gateway-based reordering occurs. The first listed interface's DNS comes first.
        var result = NetworkParameters.SelectDnsServers(new[]
        {
            Up(Dns("10.0.0.53")),      // no gateway, listed first
            Up(Dns("192.0.2.53")),     // has or has not a gateway, listed second
        });

        Assert.AreEqual(IPAddress.Parse("10.0.0.53"), result[0]);
        Assert.AreEqual(IPAddress.Parse("192.0.2.53"), result[1]);
    }
}
