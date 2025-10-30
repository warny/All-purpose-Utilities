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
        readonly ScenarioContext context;

        public ListsHooks(ScenarioContext context)
        {
            this.context = context;
        }

        // For additional details on SpecFlow hooks see http://go.specflow.org/doc-hooks
        DoubleIndexedDictionary<int, string> d;
        readonly KeyValuePairComparer<int, string> kvComparer = new();

        [Given("An empty DoubleIndexedDictionary")]
        public void AnEmptyDoubleIndexedDictionary()
        {
            d = [];
        }

        [Given("A prefilled DoubleIndexedDictionary")]
        public void APreFilledDoubleIndexedDictionary()
        {
            d = new()
            {
                { 1, "A" },
                { 2, "B" },
                { 3, "C" }
            };

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
                d.Add(i, s);
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
                d.Left[i] = s;
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
                d.Right[s] = i;
            }
            catch (System.Exception ex)
            {
                context.Add("Exception", ex);
            }
        }

        [Then("I except that key value content")]
        public void IExpectedThatDictionaryContent(Table table)
        {
            var expected = table.CreateSet<KeyValuePair<int, string>>().OrderBy(kv => kv.Key);
            var result = d.OrderBy(kv => kv.Key).ToArray();

            foreach (var c in EnumerableEx.Zip(expected, result))
            {
                Assert.IsTrue(kvComparer.Equals(c.Item1, c.Item2));
            }
        }

        [Then(@"I expect \[(\d+)\] = \""(\w+)\""")]
        public void IExpectThisKeyValue(int key, string value)
        {
            Assert.AreEqual(value, d.Left[key]);
        }

        [Then(@"I expect \[\""(\w+)\""\] = (\d+)")]
        public void IExpectThisKeyValue(string key, int value)
        {
            Assert.AreEqual(value, d.Right[key]);
        }

        [Then(@"I expect \[(\d+)\] throws (\w+)")]
        public void IExpectThisKeyValueThrows(int key, string exceptionTypeName)
        {
            try
            {
                var value = d.Left[key];
            }
            catch (Exception ex)
            {
                Assert.AreEqual(exceptionTypeName, ex.GetType().Name);
            }
        }

        [Then(@"I expect \[\""(\w+)\""\] throws (\w+)")]
        public void IExpectThisKeyValueThrows(string key, string exceptionTypeName)
        {
            try
            {
                var value = d.Right[key];
            }
            catch (Exception ex)
            {
                Assert.AreEqual(exceptionTypeName, ex.GetType().Name);
            }
        }
    }

}