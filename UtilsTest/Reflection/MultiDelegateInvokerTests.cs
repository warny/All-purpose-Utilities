using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Threading.Tasks;
using Utils.Reflection;

namespace UtilsTest.Reflection;

[TestClass]
public class MultiDelegateInvokerTests
{
private static int AddOne(int i) => i + 1;
private static int AddTwo(int i) => i + 2;

[TestMethod]
public void Invoke_Returns_All_Results()
{
var invoker = new MultiDelegateInvoker<int, int>();
invoker.Add<int>(AddOne);
invoker.Add<int>(AddTwo);

int[] results = invoker.Invoke(3);
CollectionAssert.AreEqual(new[] { 4, 5 }, results);
}

[TestMethod]
public async Task InvokeAsync_Returns_All_Results()
{
var invoker = new MultiDelegateInvoker<int, int>();
invoker.Add<int>(AddOne);
invoker.Add<int>(AddTwo);

int[] results = await invoker.InvokeAsync(3);
CollectionAssert.AreEqual(new[] { 4, 5 }, results);
}

[TestMethod]
public async Task InvokeParallelAsync_Executes_In_Parallel()
{
var invoker = new MultiDelegateInvoker<int, int>();
invoker.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 1; });
invoker.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 2; });

Stopwatch sw = Stopwatch.StartNew();
int[] results = await invoker.InvokeParallelAsync(3);
sw.Stop();

CollectionAssert.AreEqual(new[] { 4, 5 }, results);
Assert.IsTrue(sw.ElapsedMilliseconds < 190);
}

[TestMethod]
public async Task InvokeSmartAsync_Switches_Based_On_Threshold()
{
var sequential = new MultiDelegateInvoker<int, int>(3);
sequential.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 1; });
sequential.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 2; });
Stopwatch sw1 = Stopwatch.StartNew();
await sequential.InvokeSmartAsync(3);
sw1.Stop();
Assert.IsTrue(sw1.ElapsedMilliseconds >= 190);

var parallel = new MultiDelegateInvoker<int, int>(1);
parallel.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 1; });
parallel.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 2; });
Stopwatch sw2 = Stopwatch.StartNew();
await parallel.InvokeSmartAsync(3);
sw2.Stop();
Assert.IsTrue(sw2.ElapsedMilliseconds < 190);
}
}

