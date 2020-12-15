using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;
using Utils.Objects;
using Utils.IO;

namespace UtilsTest.Streams
{
	[TestClass]
	public class StreamCopierTest
	{
		[TestMethod]
		public void StreamTest1()
		{
			using (MemoryStream target1 = new MemoryStream())
			using (MemoryStream target2 = new MemoryStream())
			{
				var r = new Random();
				byte[] reference = r.NextBytes(10, 20);

				StreamCopier copier = new StreamCopier(target1, target2);

				copier.Write(reference, 0, reference.Length);
				copier.Flush();

				Assert.AreEqual(reference.Length, target1.Length);
				Assert.AreEqual(reference.Length, target2.Length);
				Assert.AreEqual(reference.Length, target1.Position);
				Assert.AreEqual(reference.Length, target2.Position);

				byte[] test1 = target1.ToArray();
				byte[] test2 = target2.ToArray();

				var comparer = new ArrayEqualityComparer<byte>();


				Assert.IsTrue(comparer.Equals(reference, test1));
				Assert.IsTrue(comparer.Equals(reference, test2));
			}

		}
	}
}
