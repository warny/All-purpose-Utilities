using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;

namespace UtilsTest.Net;

[TestClass]
public class DNSTextParserTests
{
    [TestMethod]
    public void ParseZoneFile()
    {
        var lines = new List<string>
        {
            "example.com. 3600 IN A 192.0.2.1",
            "example.com. 3600 IN MX 10 mail.example.com.",
            "example.com. 3600 IN TXT \"hello world\""
        };
        var path = Path.GetTempFileName();
        File.WriteAllLines(path, lines);
        var records = DNSText.ParseFile(path);
        Assert.AreEqual(3, records.Count);
        Assert.IsInstanceOfType(records[0].RData, typeof(Address));
        Assert.AreEqual("192.0.2.1", ((Address)records[0].RData).IPAddress.ToString());
        Assert.IsInstanceOfType(records[1].RData, typeof(MX));
        Assert.AreEqual((ushort)10, ((MX)records[1].RData).Preference);
        Assert.AreEqual("mail.example.com.", ((MX)records[1].RData).Exchange.Value);
        Assert.IsInstanceOfType(records[2].RData, typeof(TXT));
        Assert.AreEqual("hello world", ((TXT)records[2].RData).Text);
    }

    [TestMethod]
    public void WriteZoneFile()
    {
        var header = new DNSHeader();
        var record = new DNSResponseRecord("example.com.", 3600, new TXT { Text = "hello world" })
        {
            Class = DNSClass.IN
        };
        header.Responses.Add(record);
        var text = DNSText.Default.Write(header).Trim();
        Assert.AreEqual("example.com. 3600 IN TXT \"hello world\"", text);
    }

    [TestMethod]
    public void ParseAndWriteWKS()
    {
        var line = "services.example.com. 3600 IN WKS 192.0.2.1 6 AAE=";
        var rec = DNSText.ParseLine(line);
        Assert.IsInstanceOfType(rec.RData, typeof(WKS));
        var wks = (WKS)rec.RData;
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x01 }, wks.Bitmap);
        Assert.AreEqual((byte)6, wks.Protocol);
        Assert.AreEqual("192.0.2.1", wks.IPAddress.ToString());

        var header = new DNSHeader();
        header.Responses.Add(rec);
        var round = DNSText.Default.Write(header).Trim();
        Assert.AreEqual(line, round);
    }

    [TestMethod]
    public void ParseNullRecord()
    {
        var line = "null.example.com. 3600 IN NULL AQID"; // base64 for {1,2,3}
        var rec = DNSText.ParseLine(line);
        Assert.IsInstanceOfType(rec.RData, typeof(NULL));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, ((NULL)rec.RData).Datas);
    }
}
