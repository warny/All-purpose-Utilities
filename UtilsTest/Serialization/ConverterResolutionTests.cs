using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

/// <summary>Verifies exact reader covariance rules and deterministic writer contravariance.</summary>
[TestClass]
public sealed class ConverterResolutionTests
{
    /// <summary>Ensures a reader registered for an interface cannot be cast to a concrete reader.</summary>
    [TestMethod]
    public void InterfaceReader_IsRejectedForConcreteType()
    {
        Func<IReader, IAnimal> converter = _ => new Dog();
        var reader = new Reader(new MemoryStream(), [converter]);
        SerializationContractException error = Assert.ThrowsExactly<SerializationContractException>(() => reader.Read<Dog>());
        StringAssert.Contains(error.Message, typeof(IAnimal).FullName!);
        Assert.IsFalse(error.InnerException is InvalidCastException);
    }

    /// <summary>Ensures an exact concrete reader registration remains strongly typed.</summary>
    [TestMethod]
    public void ExactReader_IsSelected()
    {
        var expected = new Dog();
        Func<IReader, Dog> converter = _ => expected;
        var reader = new Reader(new MemoryStream(), [converter]);
        Assert.AreSame(expected, reader.Read<Dog>());
    }

    /// <summary>Ensures an interface writer is adapted safely for a concrete value.</summary>
    [TestMethod]
    public void InterfaceWriter_IsAdaptedWithoutDelegateCast()
    {
        IAnimal? written = null;
        Action<IWriter, IAnimal> converter = (_, value) => written = value;
        var writer = new Writer(new MemoryStream(), [converter]);
        var dog = new Dog();
        writer.Write(dog);
        Assert.AreSame(dog, written);
    }

    /// <summary>Ensures the most specific applicable writer beats a broader interface registration.</summary>
    [TestMethod]
    public void MostSpecificWriter_IsSelected()
    {
        string? selected = null;
        Action<IWriter, IAnimal> broad = (_, _) => selected = "animal";
        Action<IWriter, Mammal> specific = (_, _) => selected = "mammal";
        var writer = new Writer(new MemoryStream(), [broad, specific]);
        writer.Write(new Dog());
        Assert.AreEqual("mammal", selected);
    }

    /// <summary>Ensures equally specific interface writers produce a structured ambiguity.</summary>
    [TestMethod]
    public void EqualInterfaceWriters_AreRejected()
    {
        Action<IWriter, IAnimal> animal = (_, _) => { };
        Action<IWriter, IPet> pet = (_, _) => { };
        var writer = new Writer(new MemoryStream(), [animal, pet]);
        SerializationContractException error = Assert.ThrowsExactly<SerializationContractException>(() => writer.Write(new Dog()));
        StringAssert.Contains(error.Message, "equally specific");
        Assert.IsFalse(error.InnerException is InvalidCastException or System.Reflection.TargetInvocationException);
    }

    /// <summary>Base animal contract.</summary>
    private interface IAnimal { }

    /// <summary>Independent pet role used to create equal interface specificity.</summary>
    private interface IPet { }

    /// <summary>Base mammal implementation.</summary>
    private class Mammal : IAnimal { }

    /// <summary>Concrete animal used by converter-resolution tests.</summary>
    private sealed class Dog : Mammal, IPet { }
}
