using Annotation.ViewModels.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="DelegateCommand"/> and <see cref="DelegateCommand{T}"/>: a missing predicate means
    /// "always executable", the predicate sees the parameter, and Execute forwards the parameter to the action.
    /// </summary>
    [TestClass]
    public class DelegateCommandTests
    {
        [TestMethod]
        public void CanExecute_WithoutPredicate_IsTrue()
        {
            var command = new DelegateCommand(_ => { }, null);
            Assert.IsTrue(command.CanExecute(null));
            Assert.IsTrue(command.CanExecute("anything"));
        }

        [TestMethod]
        public void CanExecute_UsesPredicateAndPassesParameter()
        {
            object seen = null;
            var command = new DelegateCommand(_ => { }, p => { seen = p; return (p as string) == "yes"; });

            Assert.IsTrue(command.CanExecute("yes"));
            Assert.AreEqual("yes", seen);
            Assert.IsFalse(command.CanExecute("no"));
            Assert.AreEqual("no", seen);
        }

        [TestMethod]
        public void Execute_InvokesActionWithParameterEachCall()
        {
            int calls = 0;
            object seen = null;
            var command = new DelegateCommand(p => { calls++; seen = p; }, null);

            command.Execute(5);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(5, seen);

            command.Execute("x");
            Assert.AreEqual(2, calls);
            Assert.AreEqual("x", seen);
        }

        [TestMethod]
        public void Execute_DoesNotConsultPredicate()
        {
            int predicateCalls = 0;
            int executed = 0;
            var command = new DelegateCommand(_ => executed++, _ => { predicateCalls++; return false; });

            command.Execute(null);

            Assert.AreEqual(1, executed);
            Assert.AreEqual(0, predicateCalls);
        }

        [TestMethod]
        public void Execute_PropagatesActionException()
        {
            var command = new DelegateCommand(_ => throw new InvalidOperationException("boom"), null);
            Assert.ThrowsException<InvalidOperationException>(() => command.Execute(null));
        }

        [TestMethod]
        public void Generic_CanExecute_WithoutPredicate_IsTrue()
        {
            var command = new DelegateCommand<int>(_ => { }, null);
            Assert.IsTrue(command.CanExecute(7));
        }

        [TestMethod]
        public void Generic_CanExecute_UsesTypedPredicate()
        {
            var command = new DelegateCommand<int>(_ => { }, n => n > 3);
            Assert.IsTrue(command.CanExecute(4));
            Assert.IsFalse(command.CanExecute(3));
        }

        [TestMethod]
        public void Generic_Execute_PassesTypedParameter()
        {
            string seen = null;
            var command = new DelegateCommand<string>(s => seen = s, null);

            command.Execute("hello");

            Assert.AreEqual("hello", seen);
        }

        [TestMethod]
        public void Generic_WrongParameterType_ThrowsInvalidCast()
        {
            var command = new DelegateCommand<string>(_ => { }, _ => true);
            Assert.ThrowsException<InvalidCastException>(() => command.Execute(5));
            Assert.ThrowsException<InvalidCastException>(() => command.CanExecute(5));
            // Without a predicate there is no cast, so any parameter is accepted.
            Assert.IsTrue(new DelegateCommand<string>(_ => { }, null).CanExecute(5));
        }
    }
}
