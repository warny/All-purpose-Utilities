using System;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection;

namespace UtilsTest.Reflection;

/// <summary>
/// Tests for <see cref="DelegateInvoker{TBaseArg,TResult}"/> including interface dispatch and
/// concurrency safety (item 65 audit fix).
/// </summary>
[TestClass]
public class DelegateInvokerTests
{
    // ─── Type hierarchy ──────────────────────────────────────────────────────────

    private interface IAnimal { }
    private interface IMammal : IAnimal { }
    private interface IFlying : IAnimal { }
    private class Animal : IAnimal { }
    private class Dog : Animal, IMammal { }
    private class Bat : Animal, IMammal, IFlying { }

    // ─── Existing behaviour: class hierarchy dispatch ────────────────────────────

    [TestMethod]
    public void Invoke_ExactTypeMatch_SelectedFirst()
    {
        var inv = new DelegateInvoker<Animal, string>();
        inv.Add<Animal>(_ => "animal");
        inv.Add<Dog>(_ => "dog");
        Assert.AreEqual("dog", inv.Invoke(new Dog()));
    }

    [TestMethod]
    public void Invoke_BaseClassFallback_WhenNoExactMatch()
    {
        var inv = new DelegateInvoker<Animal, string>();
        inv.Add<Animal>(_ => "animal");
        Assert.AreEqual("animal", inv.Invoke(new Dog()));
    }

    [TestMethod]
    public void TryInvoke_ReturnsFalse_WhenNoMatch()
    {
        var inv = new DelegateInvoker<Animal, string>();
        bool found = inv.TryInvoke(new Dog(), out _);
        Assert.IsFalse(found);
    }

    // ─── New behaviour: interface dispatch ───────────────────────────────────────

    [TestMethod]
    public void Invoke_MatchesRegisteredInterface_WhenNoClassDelegateExists()
    {
        var inv = new DelegateInvoker<IAnimal, string>();
        inv.Add<IAnimal>(_ => "ianimal");
        Assert.AreEqual("ianimal", inv.Invoke(new Dog()));
    }

    [TestMethod]
    public void Invoke_ClassTakesPrecedenceOverInterface()
    {
        var inv = new DelegateInvoker<IAnimal, string>();
        inv.Add<IAnimal>(_ => "ianimal");
        inv.Add<Animal>(_ => "animal");
        Assert.AreEqual("animal", inv.Invoke(new Dog()));
    }

    [TestMethod]
    public void Invoke_MoreSpecificInterfaceWins_OverLessSpecific()
    {
        // IMammal : IAnimal — IMammal is more specific
        var inv = new DelegateInvoker<IAnimal, string>();
        inv.Add<IAnimal>(_ => "ianimal");
        inv.Add<IMammal>(_ => "imammal");
        Assert.AreEqual("imammal", inv.Invoke(new Dog()));
    }

    [TestMethod]
    public void TryInvoke_ThrowsAmbiguousMatchException_ForUnrelatedInterfaces()
    {
        // Bat implements both IMammal and IFlying — neither is a sub-interface of the other.
        var inv = new DelegateInvoker<IAnimal, string>();
        inv.Add<IMammal>(_ => "imammal");
        inv.Add<IFlying>(_ => "iflying");

        Assert.ThrowsException<AmbiguousMatchException>(
            () => inv.TryInvoke(new Bat(), out _));
    }

    // ─── Concurrency safety ──────────────────────────────────────────────────────

    [TestMethod]
    public void Add_IsThreadSafe_DoesNotThrowUnderConcurrentMutations()
    {
        var inv = new DelegateInvoker<Animal, int>();
        var threads = new System.Threading.Thread[8];
        for (int i = 0; i < threads.Length; i++)
        {
            threads[i] = new System.Threading.Thread(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    inv.Add<Animal>(_ => 1);
                    inv.TryInvoke(new Dog(), out _);
                }
            });
        }
        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();
        // No exception thrown means the test passes.
    }
}
