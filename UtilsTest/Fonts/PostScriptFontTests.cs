using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;
using Utils.Fonts.PostScript;
using Utils.Fonts.TTF;
using Utils.Fonts;
using Moq;

namespace UtilsTest.Fonts
{
    [TestClass]
    public class PostScriptFontTests
    {
        [TestMethod]
        public void LoadSimplePostScriptFont()
        {
            string txt = 
				"""
				Glyph: A
				Width: 10
				Height: 10
				Baseline: 0
				Path:
				M 0 0
				L 10 0
				L 10 10
				L 0 10
				Z
				EndGlyph
				""";
            using var ms = new MemoryStream(Encoding.ASCII.GetBytes(txt));
            var font = PostScriptFont.Load(ms);
            var glyph = font.GetGlyph('A');
            Assert.IsNotNull(glyph);
            Assert.AreEqual(10f, glyph.Width);
        }

        [TestMethod]
        public void GlyphToGraphicInvokesCommands()
        {
            string txt = 
				"""
				Glyph: B
				Width: 5
				Height: 5
				Baseline: 0
				Path:
				M 0 0
				L 5 0
				Z
				EndGlyph
				""";
            using var ms = new MemoryStream(Encoding.ASCII.GetBytes(txt));
            var font = PostScriptFont.Load(ms);
            var glyph = font.GetGlyph('B');
            var mock = new Mock<IGraphicConverter>(MockBehavior.Strict);
            mock.Setup(g => g.StartAt(0f, 0f));
            mock.Setup(g => g.LineTo(5f, 0f));
            mock.Setup(g => g.LineTo(0f, 0f));
            glyph.ToGraphic(mock.Object);
            mock.VerifyAll();
        }

        [TestMethod]
        public void LoadType3Font()
        {
            string ps = 
				"""
				/CharProcs 1 dict dup begin
				/A { 5 0 0 0 5 5 setcachedevice
				0 0 moveto
				5 0 lineto
				closepath
				} bind def
				end
				""";
            using var ms = new MemoryStream(Encoding.ASCII.GetBytes(ps));
            var font = Type3Font.Load(ms);
            Assert.IsNotNull(font.GetGlyph('A'));
        }

        [TestMethod]
        public void LoadType42Font()
        {
            byte[] ttf = (byte[])Fonts.ResourceManager.GetObject("Arial");
            var hex = new StringBuilder(ttf.Length * 2);
            foreach (byte b in ttf)
                hex.AppendFormat("{0:X2}", b);
            string ps = $"/sfnts [ <{hex}> ]";
            using var ms = new MemoryStream(Encoding.ASCII.GetBytes(ps));
            var font = Type42Font.Load(ms);
            Assert.IsNotNull(font.GetGlyph('A'));
        }

        [TestMethod]
        public void ParseCidKeyedFont()
        {
            // build simple charstring: 0 500 hsbw 50 50 rmoveto endchar
            byte[] cs = new byte[] { 139, 248, 136, 13, 189, 189, 21, 14 };
            byte[] enc = PostScriptFont.DecryptType1(cs, 4330, 0);
            var hex = new StringBuilder(enc.Length * 2);
            foreach (byte b in enc)
                hex.AppendFormat("{0:X2}", b);
            string plain = $"/lenIV 0\n/CharStrings 1 dict dup begin\n97 {cs.Length} RD {hex} ND\nend";
            var method = typeof(CidKeyedFont).GetMethod("ParseCidType1", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var font = (CidKeyedFont)method.Invoke(null, new object[] { plain });
            Assert.IsNotNull(font.GetGlyph('a'));
        }
    }
}
