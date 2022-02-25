using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;
using Utils.Arrays;
using Utils.Collections;
using Utils.Objects;

namespace UtilsTest.Lists
{
	[Binding]
	public sealed class ListsHooks
	{
		readonly ScenarioContext context = ScenarioContext.Current;

		// For additional details on SpecFlow hooks see http://go.specflow.org/doc-hooks
		DoubleIndexedDictionary<int, string> d;
		readonly KeyValuePairComparer<int, string> kvComparer = new();

		[BeforeScenario(Order = 1)]
		public void FirstBeforeScenario()
		{
		}

		[Given("An empty DoubleIndexedDictionary")]
		public void AnEmptyDoubleIndexedDictionary()
		{
			d = new();
		}

		[Given("A prefilled DoubleIndexedDictionary")]
		public void APreFilledDoubleIndexedDictionary()
		{
			d = new();
			d.Add(1, "A");
			d.Add(2, "B");
			d.Add(3, "C");

		}

		[When(@"I add \((\d+), \""(\w+)\""\)")]
		public void WhenIAddAValue(int i, string s)
		{
			try
			{
				d.Add(i, s);
			}
			catch (System.Exception ex)
			{
				context.Add("Exception", ex);
			}
		}

		[When(@"I add \(\""(\w+)\"", (\d+)\)")]
		public void WhenIAddAValue(string s, int i)
		{
			try
			{
				d.Add(s, i);
			}
			catch (System.Exception ex)
			{
				context.Add("Exception", ex);
			}
		}

		[When(@"I set \((\d+), \""(\w+)\""\)")]
		public void WhenISetAValue(int i, string s)
		{
			try
			{
				d[i] = s;
			}
			catch (System.Exception ex)
			{
				context.Add("Exception", ex);
			}
		}

		[When(@"I set \((\w+), (\d+)\)")]
		public void WhenISetAValue(string s, int i)
		{
			try
			{
				d[s] = i;
			}
			catch (System.Exception ex)
			{
				context.Add("Exception", ex);
			}
		}

		[Then("I except that key value content")]
		public void IExpectedThatDictionaryContent(Table table)
		{
			var expected = table.CreateSet<KeyValuePair<int, string>>().OrderBy(kv=>kv.Key);
			var result = d.OrderBy(kv => kv.Key).ToArray();

			foreach (var c in CollectionUtils.Zip(expected, result))
			{
				Assert.IsTrue(kvComparer.Equals(c.Item1, c.Item2));
			}
		}

		[Then(@"I expect \[(\d+)\] = \""(\w+)\""")]
		public void IExpectThisKeyValue(int key, string value)
		{
			Assert.AreEqual(value, d[key]);
		}

		[Then(@"I expect \[\""(\w+)\""\] = (\d+)")]
		public void IExpectThisKeyValue(string key, int value)
		{
			Assert.AreEqual(value, d[key]);
		}

		[Then(@"I expect \[(\d+)\] throw (\w+)")]
		public void IExpectThisKeyValueThrows(int key, string exceptionTypeName)
		{
			try
			{
				var value = d[key];
			}
			catch (Exception ex)
			{
				Assert.AreEqual(exceptionTypeName, ex.GetType().Name);
			}
		}

		[Then(@"I expect \[\""(\w+)\""\] throw (\w+)")]
		public void IExpectThisKeyValueThrows(string key, string exceptionTypeName)
		{
			try
			{
				var value = d[key];
			}
			catch (Exception ex)
			{
				Assert.AreEqual(exceptionTypeName, ex.GetType().Name);
			}
		}



		[AfterScenario]
		public void AfterScenario()
		{
			
		}
	}

}