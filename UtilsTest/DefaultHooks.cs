using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechTalk.SpecFlow;

namespace UtilsTest
{
	[Binding]
	public sealed class DefaultHooks
	{
		readonly ScenarioContext context = ScenarioContext.Current;

		[Then("I expect an exception")]
		public void ThenIExpectAnException()
		{
			var exception = context["Exception"];
			Assert.IsNotNull(exception);
		}

		[Then(@"I expect an exception (\w+)")]
		public void ThenIExpectAnException(string exceptionTypeName)
		{
			var exception = context["Exception"];
			Assert.IsNotNull(exception);
			Assert.AreEqual(exceptionTypeName, exception.GetType().Name);
		}


	}
}
