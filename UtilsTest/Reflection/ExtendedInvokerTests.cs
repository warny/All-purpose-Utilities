using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Reflection;

namespace UtilsTest.Reflection
{
    [TestClass]
    public class ExtendedInvokerTests
    {
        private static string FromInt(int i) => "int";
        private static string FromDouble(double d) => "double";
        private static string FromStringInt(string s, int i) => "string-int";
        private static string FromObject(object o) => "object";

        [TestMethod]
        public void Invoke_Selects_Best_Delegate()
        {
            var invoker = new ExtendedInvoker<string>();
            invoker.Add((Func<int, string>)FromInt);
            invoker.Add((Func<double, string>)FromDouble);
            invoker.Add((Func<string, int, string>)FromStringInt);
            invoker.Add((Func<object, string>)FromObject);

            Assert.AreEqual("int", invoker.Invoke(10));
            Assert.AreEqual("double", invoker.Invoke(2.5));
            Assert.AreEqual("string-int", invoker.Invoke("foo", 3));
            Assert.AreEqual("object", invoker.Invoke(new object()));
        }

        [TestMethod]
        public void TryInvoke_Returns_False_When_No_Match()
        {
            var invoker = new ExtendedInvoker<string>();
            invoker.Add((Func<int, string>)FromInt);
            var ok = invoker.TryInvoke(["foo"], out var result);

            Assert.IsFalse(ok);
            Assert.IsNull(result);
        }
    }
}
