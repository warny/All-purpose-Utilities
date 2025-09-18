using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Objects;

namespace UtilsTest.Objects;

[TestClass]
public class ConstrainedValueTests
{
    private sealed class PositiveIntValue : ConstrainedValue<int>
    {
        public PositiveIntValue(int value)
            : base(value)
        {
        }

        protected override void CheckValue(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }

    private sealed class NullableTextValue : ConstrainedValue<string>
    {
        public NullableTextValue(string value)
            : base(value)
        {
        }

        protected override void CheckValue(string value)
        {
            // All values accepted for this test helper.
        }
    }

    [TestMethod]
    public void ConstructorThrowsWhenValueInvalid()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PositiveIntValue(0));
    }

    [TestMethod]
    public void ImplicitConversionReturnsUnderlyingValue()
    {
        PositiveIntValue positive = new(10);

        int value = positive;

        Assert.AreEqual(10, value);
        Assert.AreEqual("10", positive.ToString());
    }

    [TestMethod]
    public void ToStringReturnsEmptyWhenUnderlyingValueIsNull()
    {
        var value = new NullableTextValue(null);

        Assert.AreEqual(string.Empty, value.ToString());
    }
}
