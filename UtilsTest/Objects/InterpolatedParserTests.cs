using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Objects;

namespace UtilsTest.Objects;

[TestClass]
public class InterpolatedParserTests
{
    [TestMethod]
    public void Parse_SimplePattern()
    {
        const string input = "Name: John Age: 42";
        bool success = InterpolatedParser.TryParse(input, $"Name: {string.Empty} Age: {0}", out var values);
        Assert.IsTrue(success);
        Assert.AreEqual("John", values[0]);
        Assert.AreEqual(42, values[1]);
    }

    [TestMethod]
    public void Parse_Failure()
    {
        const string input = "Invalid";
        bool success = InterpolatedParser.TryParse(input, $"Name: {string.Empty}", out var values);
        Assert.IsFalse(success);
        Assert.AreEqual(0, values.Length);
    }

    [TestMethod]
    public void Parse_CustomObject()
    {
        const string input = "Name: Jane Age: 30";
        bool success = InterpolatedParser.TryParse<Person>(input, $"Name: {string.Empty} Age: {0}", out var person);
        Assert.IsTrue(success);
        Assert.AreEqual("Jane", person.Name);
        Assert.AreEqual(30, person.Age);
    }

    private record Person(string Name, int Age);
}
