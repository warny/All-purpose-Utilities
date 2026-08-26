using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Transactions;

namespace UtilsTest.Transactions;

[TestClass]
public class TransactionExecutorTests
{
    private sealed class TestAction : ITransactionalAction
    {
        private readonly List<string> _log;
        private readonly string _name;
        private readonly bool _failExecute;
        private readonly bool _failCommit;
        private readonly bool _failRollback;

        public TestAction(string name, List<string> log, bool failExecute = false,
            bool failCommit = false, bool failRollback = false)
        {
            _name = name;
            _log = log;
            _failExecute = failExecute;
            _failCommit = failCommit;
            _failRollback = failRollback;
        }

        public void Execute()
        {
            _log.Add($"execute {_name}");
            if (_failExecute) throw new InvalidOperationException($"execute {_name} failed");
        }

        public void Commit()
        {
            _log.Add($"commit {_name}");
            if (_failCommit) throw new InvalidOperationException($"commit {_name} failed");
        }

        public void Rollback()
        {
            _log.Add($"rollback {_name}");
            if (_failRollback) throw new InvalidOperationException($"rollback {_name} failed");
        }
    }

    // ------------------------------------------------------------------ basic scenarios

    [TestMethod]
    public void ExecuteCommitsWhenAllSucceed()
    {
        var executor = new TransactionExecutor();
        var log = new List<string>();
        ITransactionalAction[] actions = [new TestAction("a", log), new TestAction("b", log)];

        executor.Execute(actions);

        CollectionAssert.AreEqual(
            new[] { "execute a", "execute b", "commit a", "commit b" }, log);
    }

    [TestMethod]
    public void ExecuteRollsBackWhenExecuteFailure()
    {
        var executor = new TransactionExecutor();
        var log = new List<string>();
        ITransactionalAction[] actions =
        [
            new TestAction("a", log),
            new TestAction("b", log, failExecute: true),
            new TestAction("c", log),
        ];

        var ex = Assert.ThrowsExactly<TransactionException>(() => executor.Execute(actions));
        Assert.IsInstanceOfType<InvalidOperationException>(ex.PrimaryException);
        CollectionAssert.AreEqual(
            new[] { "execute a", "execute b", "rollback a" }, log);
    }

    [TestMethod]
    public void RollbackRunsInReverseOrder()
    {
        var executor = new TransactionExecutor();
        var log = new List<string>();
        ITransactionalAction[] actions =
        [
            new TestAction("a", log),
            new TestAction("b", log),
            new TestAction("c", log, failExecute: true),
        ];

        Assert.ThrowsExactly<TransactionException>(() => executor.Execute(actions));

        CollectionAssert.AreEqual(
            new[] { "execute a", "execute b", "execute c", "rollback b", "rollback a" }, log);
    }

    // ------------------------------------------------------------------ #36 commit failure only rolls back uncommitted actions

    [TestMethod]
    public void CommitFailure_OnlyRollsBackUncommittedActions()
    {
        // a commits successfully, b's commit throws.
        // Only b should be rolled back — a already committed and must not be rolled back.
        var executor = new TransactionExecutor();
        var log = new List<string>();
        ITransactionalAction[] actions =
        [
            new TestAction("a", log),
            new TestAction("b", log, failCommit: true),
        ];

        var ex = Assert.ThrowsExactly<TransactionException>(() => executor.Execute(actions));
        Assert.IsInstanceOfType<InvalidOperationException>(ex.PrimaryException);

        // "commit a" succeeds, "commit b" throws → only "b" is rolled back.
        CollectionAssert.AreEqual(
            new[] { "execute a", "execute b", "commit a", "commit b", "rollback b" }, log);
    }

    // ------------------------------------------------------------------ #37 rollback failures are all collected

    [TestMethod]
    public void RollbackFailures_AreAllCollectedAndPreserveOriginalException()
    {
        var executor = new TransactionExecutor();
        var log = new List<string>();
        ITransactionalAction[] actions =
        [
            new TestAction("a", log, failRollback: true),
            new TestAction("b", log, failRollback: true),
            new TestAction("c", log, failExecute: true),
        ];

        var ex = Assert.ThrowsExactly<TransactionException>(() => executor.Execute(actions));

        // Original failure is preserved.
        Assert.IsInstanceOfType<InvalidOperationException>(ex.PrimaryException);
        StringAssert.Contains(ex.PrimaryException.Message, "execute c");

        // Both rollbacks were attempted and both failures collected.
        Assert.AreEqual(2, ex.RollbackExceptions.Count);
        Assert.IsTrue(ex.RollbackExceptions.All(e => e is InvalidOperationException));
    }

    [TestMethod]
    public void RollbackFailure_DoesNotSkipEarlierActions()
    {
        var executor = new TransactionExecutor();
        var log = new List<string>();
        ITransactionalAction[] actions =
        [
            new TestAction("a", log, failRollback: false),
            new TestAction("b", log, failRollback: true),  // fails during rollback
            new TestAction("c", log, failExecute: true),
        ];

        Assert.ThrowsExactly<TransactionException>(() => executor.Execute(actions));

        // Rollback of b throws, but rollback of a must still run.
        Assert.IsTrue(log.Contains("rollback a"), "rollback a must be attempted even after rollback b fails.");
        Assert.IsTrue(log.Contains("rollback b"), "rollback b must be attempted.");
    }

    // ------------------------------------------------------------------ #38 upfront materialization

    [TestMethod]
    public void NullActionInEnumerable_ThrowsBeforeAnyExecution()
    {
        var executor = new TransactionExecutor();
        var log = new List<string>();
        ITransactionalAction[] actions = [new TestAction("a", log), null!];

        Assert.ThrowsExactly<ArgumentException>(() => executor.Execute(actions));
        // No action must have been executed.
        Assert.AreEqual(0, log.Count, "No action should execute before the null is detected.");
    }

    [TestMethod]
    public void LazyEnumerableThatThrows_ThrowsBeforeAnyExecution()
    {
        var executor = new TransactionExecutor();
        var log = new List<string>();

        IEnumerable<ITransactionalAction> Lazy()
        {
            yield return new TestAction("a", log);
            throw new InvalidOperationException("enumeration fault");
        }

        // The executor must materialize the list first; enumeration fault throws before
        // any Execute() is called.
        Assert.ThrowsExactly<InvalidOperationException>(() => executor.Execute(Lazy()));
        Assert.AreEqual(0, log.Count, "No action should execute before enumeration completes.");
    }
}
