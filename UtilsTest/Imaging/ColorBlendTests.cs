using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class ColorBlendTests
{
	[TestMethod]
	public void MultiplyDouble()
	{
		ColorArgb c1 = new(1, 0.5, 0.25, 0.75);
		ColorArgb c2 = new(1, 0.5, 0.5, 0.5);
		var result = ColorBlend.Multiply(c1, c2);
		Assert.AreEqual(1, result.Alpha, 1e-6);
		Assert.AreEqual(0.25, result.Red, 1e-6);
		Assert.AreEqual(0.125, result.Green, 1e-6);
		Assert.AreEqual(0.375, result.Blue, 1e-6);
	}

	[TestMethod]
	public void AverageDouble()
	{
		ColorArgb c1 = new(0.8, 1, 0, 0);
		ColorArgb c2 = new(0.6, 0, 0, 1);
		var result = ColorBlend.Average(c1, c2);
		Assert.AreEqual(0.7, result.Alpha, 1e-6);
		Assert.AreEqual(0.5, result.Red, 1e-6);
		Assert.AreEqual(0, result.Green, 1e-6);
		Assert.AreEqual(0.5, result.Blue, 1e-6);
	}

	[TestMethod]
	public void ClosestIntensityDouble()
	{
		ColorArgb low = new(1, 0.2, 0.2, 0.2); // intensity ~0.2
		ColorArgb high = new(1, 0.8, 0.8, 0.8); // intensity ~0.8
		var result = ColorBlend.ClosestToIntensity(low, high, 0.3);
		Assert.AreEqual(low.Red, result.Red, 1e-6);
	}

	[TestMethod]
	public void ClosestIntensityByte()
	{
		ColorArgb32 low = new(byte.MaxValue, 50, 50, 50); // ~0.2
		ColorArgb32 high = new(byte.MaxValue, 200, 200, 200); // ~0.78
		var result = ColorBlend.ClosestToIntensity(low, high, 0.7);
		Assert.AreEqual(high.Red, result.Red);
	}

	[TestMethod]
	public void ClosestIntensityUShort()
	{
		ColorArgb64 low = new(ushort.MaxValue, 10000, 10000, 10000); // ~0.15
		ColorArgb64 high = new(ushort.MaxValue, 50000, 50000, 50000); // ~0.76
		var result = ColorBlend.ClosestToIntensity(low, high, 0.6);
		Assert.AreEqual(high.Red, result.Red);
	}

	[TestMethod]
	public void MaskDouble()
	{
		ColorArgb mask = new(1, 0.5, 0.2, 1);
		ColorArgb color = new(1.0, 1.0, 1.0, 1.0);
		var result = ColorBlend.Mask(mask, color);
		Assert.AreEqual(0.5, result.Red, 1e-6);
		Assert.AreEqual(0.2, result.Green, 1e-6);
		Assert.AreEqual(1, result.Blue, 1e-6);
	}

	[TestMethod]
	public void MaskByte()
	{
		ColorArgb32 mask = new(byte.MaxValue, 128, 64, byte.MaxValue);
		ColorArgb32 color = new(byte.MaxValue, 200, 200, 200);
		var result = ColorBlend.Mask(mask, color);
		Assert.AreEqual(100, result.Red);
		Assert.AreEqual(50, result.Green);
		Assert.AreEqual(200, result.Blue);
	}

	[TestMethod]
	public void MaskUShort()
	{
		ColorArgb64 mask = new(ushort.MaxValue, 32768, 16384, ushort.MaxValue);
		ColorArgb64 color = new(ushort.MaxValue, 50000, 50000, 50000);
		var result = ColorBlend.Mask(mask, color);
		Assert.AreEqual(25000, result.Red);
		Assert.AreEqual(12500, result.Green);
		Assert.AreEqual(50000, result.Blue);
	}
}
