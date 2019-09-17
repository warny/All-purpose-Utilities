using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class DateUtilitiesTest
	{
		[TestMethod]
		public void ComputeEasterTest()
		{
			var knownEastern = new DateTime[] {
				new DateTime(2006, 4, 16),
				new DateTime(2018, 4, 1),
				new DateTime(2019, 4, 21),
				new DateTime(2020, 4, 12),
				new DateTime(2021, 4, 4),
				new DateTime(2022, 4, 17),
				new DateTime(2023, 4, 9),
				new DateTime(2024, 3, 31)
			};

			foreach (var knownEaster in knownEastern)
			{
				var easter = DateUtils.ComputeEaster(knownEaster.Year);
				Assert.AreEqual(knownEaster, easter);
			}
		}
	}
}
