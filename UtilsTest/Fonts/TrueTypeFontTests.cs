using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Fonts.TTF;

namespace UtilsTest.Fonts
{
	[TestClass]
	public class TrueTypeFontTests
	{
		[TestMethod]
		public void LoadFontTest()
		{
			TrueTypeFont font = TrueTypeFont.ParseFont((byte[])Fonts.ResourceManager.GetObject("Arial"));
		}

		[TestMethod]
		public void LoadFontGlyphTest()
		{
			TrueTypeFont font = TrueTypeFont.ParseFont((byte[])Fonts.ResourceManager.GetObject("Arial"));
			var glyph = font.GetGlyph('a');
			Assert.IsNotNull(glyph);
		}

	}
}
