using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Utils.Transactions;

namespace UtilsTest.Transactions
{
    [TestClass]
    public class TransactionExecutorTests
    {
        private sealed class TestAction : ITransactionalAction
        {
            private readonly List<string> _log;
            private readonly string _name;
            private readonly bool _fail;

            public TestAction(string name, List<string> log, bool fail = false)
            {
                _name = name;
                _log = log;
                _fail = fail;
            }

            public void Execute()
            {
                _log.Add($"execute {_name}");
                if (_fail)
                {
                    throw new InvalidOperationException();
                }
            }

            public void Commit()
            {
                _log.Add($"commit {_name}");
            }

            public void Rollback()
            {
                _log.Add($"rollback {_name}");
            }
        }

        [TestMethod]
        public void ExecuteCommitsWhenAllSucceed()
        {
            TransactionExecutor executor = new TransactionExecutor();
            List<string> log = [];
            ITransactionalAction[] actions =
            [
                    new TestAction("a", log),
                                new TestAction("b", log),
                        ];

            executor.Execute(actions);

            CollectionAssert.AreEqual(
                    new[]
                    {
                                        "execute a",
                                        "execute b",
                                        "commit a",
                                        "commit b",
                    },
                    log);
        }

        [TestMethod]
        public void ExecuteRollsBackWhenFailureOccurs()
        {
            TransactionExecutor executor = new TransactionExecutor();
            List<string> log = [];
            ITransactionalAction[] actions =
            [
                    new TestAction("a", log),
                                new TestAction("b", log, true),
                                new TestAction("c", log),
                        ];

            Assert.ThrowsException<InvalidOperationException>(() => executor.Execute(actions));

            CollectionAssert.AreEqual(
                    new[]
                    {
                                        "execute a",
                                        "execute b",
                                        "rollback a",
                    },
                    log);
        }

        [TestMethod]
        public void RollbackRunsInReverseOrder()
        {
            TransactionExecutor executor = new TransactionExecutor();
            List<string> log = [];
            ITransactionalAction[] actions =
            [
                    new TestAction("a", log),
                                new TestAction("b", log),
                                new TestAction("c", log, true),
                        ];

            Assert.ThrowsException<InvalidOperationException>(() => executor.Execute(actions));

            CollectionAssert.AreEqual(
                    new[]
                    {
                                        "execute a",
                                        "execute b",
                                        "execute c",
                                        "rollback b",
                                        "rollback a",
                    },
                    log);
        }
    }
}

