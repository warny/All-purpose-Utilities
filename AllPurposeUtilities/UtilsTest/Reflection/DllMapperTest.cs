using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Utils.Reflection;

namespace UtilsTest.Reflection
{
	public class Shell32 : DllMapper
	{
		public delegate bool PathIsExeDelegate([MarshalAs(UnmanagedType.LPWStr)]string filename);
		[External("PathIsExe")]
		public PathIsExeDelegate PathIsExe;
		//Declare Function ExtractIconEx Lib "shell32.dll" Alias "ExtractIconExA" (ByVal lpszFile As String, ByVal nIconIndex As Long, phiconLarge As Long, phiconSmall As Long, ByVal nIcons As Long) As Long
	}

	[TestClass]
	public class DllMapperTest
	{
		[TestMethod]
		public void FunctionTest()
		{
			using (var shell32 = DllMapper.Create<Shell32>("shell32.dll"))
			{
				Assert.IsFalse(shell32.PathIsExe("test.bin"));
				Assert.IsTrue(shell32.PathIsExe("test.exe"));
			}
		}
	}
}
