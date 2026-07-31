using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

/// <summary>Verifies structural runtime serialization contract rules.</summary>
[TestClass]
public sealed class SerializationContractTests
{
    /// <summary>Ensures duplicate wire orders are rejected before expression compilation.</summary>
    [TestMethod]
    public void DuplicateOrders_AreAggregated()
    {
        var reader = new Reader(new MemoryStream());
        SerializationContractException error = Assert.ThrowsException<SerializationContractException>(() => reader.Read<DuplicateOrderModel>());
        StringAssert.Contains(error.Message, "UIORT004");
        StringAssert.Contains(error.Message, nameof(DuplicateOrderModel.First));
        StringAssert.Contains(error.Message, nameof(DuplicateOrderModel.Second));
    }

    /// <summary>Ensures direct and indirect recursive contract graphs produce structured failures.</summary>
    [TestMethod]
    public void RecursiveContracts_AreRejected()
    {
        SerializationContractException direct = Assert.ThrowsException<SerializationContractException>(() => new Reader(new MemoryStream()).Read<DirectRecursiveModel>());
        SerializationContractException indirect = Assert.ThrowsException<SerializationContractException>(() => new Writer(new MemoryStream()).Write(new IndirectRecursiveA()));
        StringAssert.Contains(direct.Message, "UIORT007");
        StringAssert.Contains(indirect.Message, nameof(IndirectRecursiveA));
        StringAssert.Contains(indirect.Message, nameof(IndirectRecursiveB));
    }

    /// <summary>Ensures concurrent callers observe the failure cached by one logical build.</summary>
    [TestMethod]
    public async Task FailedContractBuild_IsSharedAcrossCallers()
    {
        var reader = new Reader(new MemoryStream());
        Task<SerializationContractException>[] calls = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            Assert.ThrowsException<SerializationContractException>(() => reader.Read<NoDefaultConstructorModel>()))).ToArray();
        SerializationContractException[] failures = await Task.WhenAll(calls);
        Assert.IsTrue(failures.All(error => ReferenceEquals(error, failures[0])));
    }

    /// <summary>Ensures primitive EOF can never be converted into a valid value.</summary>
    [TestMethod]
    public void PrimitiveTruncation_ThrowsEndOfStreamException()
    {
        var reader = new Reader(new MemoryStream(new byte[] { 1, 2, 3 }));
        EndOfStreamException error = Assert.ThrowsException<EndOfStreamException>(() => reader.Read<uint>());
        StringAssert.Contains(error.Message, "expected 4 bytes, received 3");
        Assert.ThrowsException<EndOfStreamException>(() => new Reader(new MemoryStream()).Read<byte>());
    }

    /// <summary>Ensures a failed seek neither leaves a phantom stack entry nor a changed position.</summary>
    [TestMethod]
    public void PushFailure_IsTransactional()
    {
        using var stream = new MutatingFailingSeekStream(new byte[8]);
        var reader = new Reader(stream);
        Assert.ThrowsException<IOException>(() => reader.Push(3, SeekOrigin.Begin));
        Assert.AreEqual(0, stream.Position);
        Assert.ThrowsException<InvalidOperationException>(() => reader.Pop());
    }

    /// <summary>Model with an invalid duplicate order.</summary>
    private sealed class DuplicateOrderModel
    {
        [Field(1)] public int First { get; set; }
        [Field(1)] public int Second { get; set; }
    }

    /// <summary>Direct recursive model.</summary>
    private sealed class DirectRecursiveModel
    {
        [Field(0)] public DirectRecursiveModel Child { get; set; } = null!;
    }

    /// <summary>First node of an indirect recursive model.</summary>
    private sealed class IndirectRecursiveA
    {
        [Field(0)] public IndirectRecursiveB Child { get; set; } = new();
    }

    /// <summary>Second node of an indirect recursive model.</summary>
    private sealed class IndirectRecursiveB
    {
        [Field(0)] public IndirectRecursiveA Parent { get; set; } = null!;
    }

    /// <summary>Invalid model without a parameterless constructor.</summary>
    private sealed class NoDefaultConstructorModel
    {
        /// <summary>Initializes the model with a required value.</summary>
        public NoDefaultConstructorModel(int value) => Value = value;
        [Field(0)] public int Value { get; set; }
    }

    /// <summary>Seekable stream that mutates its position and then fails.</summary>
    private sealed class MutatingFailingSeekStream : MemoryStream
    {
        /// <summary>Initializes the test stream.</summary>
        internal MutatingFailingSeekStream(byte[] bytes) : base(bytes) { }

        /// <summary>Mutates position before simulating a broken seek operation.</summary>
        public override long Seek(long offset, SeekOrigin loc)
        {
            Position = offset;
            throw new IOException("Injected seek failure.");
        }
    }
}
