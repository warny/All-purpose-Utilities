using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Transactions;

namespace UtilsTest.Security.Immutability;

/// <summary>Verifies that <see cref="TransactionException"/> does not expose a mutable rollback-exception collection.</summary>
[TestClass]
public class TransactionExecutorImmutabilityTests
{
    [TestMethod]
    public void TransactionException_RollbackExceptions_AreImmutableSnapshot()
    {
        var primary = new InvalidOperationException("primary");
        var rollback = new InvalidOperationException("rollback");
        var source = new List<Exception> { rollback };

        var exception = new TransactionException(primary, source);
        source.Clear();

        Assert.AreEqual(1, exception.RollbackExceptions.Count);
        Assert.AreSame(rollback, exception.RollbackExceptions[0]);
        Assert.IsFalse(exception.RollbackExceptions is List<Exception>);
        Assert.IsFalse(exception.RollbackExceptions is Exception[]);
        var collection = (ICollection<Exception>)exception.RollbackExceptions;
        Assert.IsTrue(collection.IsReadOnly);
        Assert.ThrowsExactly<NotSupportedException>(() => collection.Clear());
    }
}
