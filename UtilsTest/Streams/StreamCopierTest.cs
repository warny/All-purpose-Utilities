using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;
using Utils.Streams;

namespace UtilsTest.Streams
{
	[TestClass]
	public class StreamCopierTest
	{
		[TestMethod]
		public void StreamTest1()
		{
			MemoryStream target1 = new MemoryStream();
			MemoryStream target2 = new MemoryStream();

			StreamCopier copier = new StreamCopier(target1, target2);

			copier.WriteByte(0);
			copier.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 0, 9);
			copier.Flush();

			Assert.AreEqual(10, target1.Length);
			Assert.AreEqual(10, target2.Length);
			Assert.AreEqual(10, target1.Position);
			Assert.AreEqual(10, target2.Position);

			target1.Position = 0;
			target2.Position = 0;

			byte[] test1 = new byte[10];
			byte[] test2 = new byte[10];

			target1.Read(test1, 0, 10);
			target2.Read(test2, 0, 10);

			var comparer = new ArrayEqualityComparer<byte>();

			byte[] reference = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

			Assert.IsTrue(comparer.Equals(reference, test1));
			Assert.IsTrue(comparer.Equals(reference, test2));
		}
	}
}
