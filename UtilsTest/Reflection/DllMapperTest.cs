using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.InteropServices;
using System.Text;
using Utils.Reflection;

namespace UtilsTest.Reflection
{
	public interface IKernel32 : IDisposable
	{
		/*[DllImport("user32.dll", CharSet = CharSet.Auto)]*/
		[External("GetTempPathA")]
		uint GetTempPath(uint nBufferLength, [Out] StringBuilder lpBuffer);
	}

	public class User32 : DllMapper
	{
		private delegate uint GetTempPathDelegate(uint nBufferLength, [Out] StringBuilder lpBuffer);

		[External("GetTempPathA")]
		private GetTempPathDelegate __GetTempPath;

		public uint GetTempPath(uint nBufferLength, [Out] StringBuilder lpBuffer) => __GetTempPath(nBufferLength, lpBuffer);
	}

	[TestClass]
	public class DllMapperTest
	{
		[DllImport("kernel32.dll")]
		static extern uint GetTempPath(uint nBufferLength, [Out] StringBuilder lpBuffer);

		[TestMethod]
		public void MapFromInterfaceTest()
		{
			using (var kernel32 = DllMapper.Emit<IKernel32>("kernel32.dll", CallingConvention.Winapi))
			{
				StringBuilder tempPath = new StringBuilder(' ', 1024);
				var i = kernel32.GetTempPath(261, tempPath);
				Assert.AreEqual((int)i, tempPath.ToString().Length);
			}
		}

		[TestMethod]
		public void MapFromClassTest()
		{
			using (User32 kernel32 = DllMapper.Create<User32>("kernel32.dll"))
			{
				StringBuilder tempPath = new StringBuilder(' ', 1024);
				var i = kernel32.GetTempPath(261, tempPath);
				Assert.AreEqual((int)i, tempPath.ToString().Length);
			}
		}
	}
}
