using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Tokens;
using Viking.UI.WPF.ViewModels;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the annotation-server picker view model: which servers are listed, their order, the preselection,
    /// the status text, and the Select/Cancel commands. Contains no token or credential handling.
    /// </summary>
    [TestClass]
    public class AnnotationServerSelectionViewModelTests
    {
        private static AnnotationServerChoice Server(string name, string endpoint = "https://x/annotation", bool isDefault = false) =>
            new AnnotationServerChoice { Name = name, AnnotationEndpoint = endpoint, IsDefault = isDefault };

        private static string[] Names(AnnotationServerSelectionViewModel vm) => vm.Servers.Select(s => s.Name).ToArray();

        [TestMethod]
        public void Constructor_NullServers_IsEmptyWithNoSelectionAndNoServersMessage()
        {
            var vm = new AnnotationServerSelectionViewModel(null);

            Assert.AreEqual(0, vm.Servers.Count);
            Assert.IsNull(vm.SelectedServer);
            Assert.AreEqual("No annotation servers were listed for this volume.", vm.StatusMessage);
        }

        [TestMethod]
        public void Constructor_DropsNullAndBlankEndpointServers()
        {
            var vm = new AnnotationServerSelectionViewModel(new List<AnnotationServerChoice>
            {
                null,
                Server("NullEndpoint", null),
                Server("Empty", ""),
                Server("Spaces", "   "),
                Server("Keep"),
            });

            CollectionAssert.AreEqual(new[] { "Keep" }, Names(vm));
        }

        [TestMethod]
        public void Constructor_AllServersInvalid_BehavesAsEmpty()
        {
            var vm = new AnnotationServerSelectionViewModel(new[] { Server("A", ""), null });

            Assert.AreEqual(0, vm.Servers.Count);
            Assert.IsNull(vm.SelectedServer);
            Assert.AreEqual("No annotation servers were listed for this volume.", vm.StatusMessage);
        }

        [TestMethod]
        public void Constructor_OrdersDefaultFirstThenNameIgnoringCase()
        {
            var vm = new AnnotationServerSelectionViewModel(new[]
            {
                Server("beta"),
                Server("Zulu", isDefault: true),
                Server("Alpha"),
                Server("alpine"),
            });

            CollectionAssert.AreEqual(new[] { "Zulu", "Alpha", "alpine", "beta" }, Names(vm));
        }

        [TestMethod]
        public void Constructor_NamesTieIgnoringCase_KeepsInputOrder()
        {
            var first = Server("same");
            var second = Server("SAME");

            var vm = new AnnotationServerSelectionViewModel(new[] { first, second });

            Assert.AreSame(first, vm.Servers[0]);
            Assert.AreSame(second, vm.Servers[1]);
        }

        [TestMethod]
        public void Constructor_PreselectsDefault()
        {
            var def = Server("Zulu", isDefault: true);

            var vm = new AnnotationServerSelectionViewModel(new[] { Server("Alpha"), def });

            Assert.AreSame(def, vm.SelectedServer);
        }

        [TestMethod]
        public void Constructor_NoDefault_PreselectsFirstByName()
        {
            var alpha = Server("Alpha");

            var vm = new AnnotationServerSelectionViewModel(new[] { Server("Beta"), alpha });

            Assert.AreSame(alpha, vm.SelectedServer);
        }

        [TestMethod]
        public void Constructor_StatusMessageReportsCount()
        {
            var vm = new AnnotationServerSelectionViewModel(new[] { Server("A"), Server("B"), Server("C") });

            Assert.AreEqual("Choose an annotation server (3 available).", vm.StatusMessage);
        }

        [TestMethod]
        public void Constructor_StatusMessageCountExcludesDroppedServers()
        {
            var vm = new AnnotationServerSelectionViewModel(new[] { Server("A"), Server("B", ""), null });

            Assert.AreEqual("Choose an annotation server (1 available).", vm.StatusMessage);
        }

        [TestMethod]
        public void SelectCommand_Empty_CannotExecuteAndDoesNotRaise()
        {
            var vm = new AnnotationServerSelectionViewModel(null);
            int raised = 0;
            vm.ServerSelected += (s, e) => raised++;

            Assert.IsFalse(vm.SelectCommand.CanExecute(null));
            vm.SelectCommand.Execute(null);

            Assert.AreEqual(0, raised);
        }

        [TestMethod]
        public void SelectCommand_WithSelection_RaisesServerSelectedWithSenderAndServer()
        {
            var chosen = Server("Only");
            var vm = new AnnotationServerSelectionViewModel(new[] { chosen });
            object sender = null;
            AnnotationServerChoice arg = null;
            int raised = 0;
            vm.ServerSelected += (s, e) => { sender = s; arg = e; raised++; };

            Assert.IsTrue(vm.SelectCommand.CanExecute(null));
            vm.SelectCommand.Execute(null);

            Assert.AreEqual(1, raised);
            Assert.AreSame(vm, sender);
            Assert.AreSame(chosen, arg);
        }

        [TestMethod]
        public void SelectCommand_AfterChangingSelection_RaisesWithNewServer()
        {
            var a = Server("A", isDefault: true);
            var b = Server("B");
            var vm = new AnnotationServerSelectionViewModel(new[] { a, b });
            AnnotationServerChoice arg = null;
            vm.ServerSelected += (s, e) => arg = e;

            vm.SelectedServer = b;
            vm.SelectCommand.Execute(null);

            Assert.AreSame(b, arg);
        }

        [TestMethod]
        public void SelectCommand_ClearingSelection_DisablesCommandAndRaisesCanExecuteChanged()
        {
            var vm = new AnnotationServerSelectionViewModel(new[] { Server("A") });
            int canExecuteChanged = 0;
            vm.SelectCommand.CanExecuteChanged += (s, e) => canExecuteChanged++;

            vm.SelectedServer = null;

            Assert.AreEqual(1, canExecuteChanged);
            Assert.IsFalse(vm.SelectCommand.CanExecute(null));
        }

        [TestMethod]
        public void CancelCommand_AlwaysCanExecuteAndRaisesSelectionCancelled()
        {
            var empty = new AnnotationServerSelectionViewModel(null);
            object sender = null;
            int raised = 0;
            empty.SelectionCancelled += (s, e) => { sender = s; raised++; };

            Assert.IsTrue(empty.CancelCommand.CanExecute(null));
            empty.CancelCommand.Execute(null);

            Assert.AreEqual(1, raised);
            Assert.AreSame(empty, sender);
        }

        [TestMethod]
        public void SelectedServer_ChangeRaisesPropertyChangedOnce_SameReferenceDoesNot()
        {
            var a = Server("A", isDefault: true);
            var b = Server("B");
            var vm = new AnnotationServerSelectionViewModel(new[] { a, b });
            var changed = new List<string>();
            vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            vm.SelectedServer = b;
            vm.SelectedServer = b;

            CollectionAssert.AreEqual(new[] { nameof(vm.SelectedServer) }, changed);
        }

        [TestMethod]
        public void SelectedServer_UsesReferenceEqualityNotValueEquality()
        {
            var a = Server("A");
            var lookalike = Server("A");
            var vm = new AnnotationServerSelectionViewModel(new[] { a });
            int changed = 0;
            vm.PropertyChanged += (s, e) => changed++;

            vm.SelectedServer = lookalike;

            Assert.AreEqual(1, changed);
            Assert.AreSame(lookalike, vm.SelectedServer);
        }

        [TestMethod]
        public void StatusMessage_ChangeRaisesPropertyChangedOnce_SameValueDoesNot()
        {
            var vm = new AnnotationServerSelectionViewModel(null);
            var changed = new List<string>();
            ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            vm.StatusMessage = "Signing in";
            vm.StatusMessage = "Signing in";

            CollectionAssert.AreEqual(new[] { nameof(vm.StatusMessage) }, changed);
            Assert.AreEqual("Signing in", vm.StatusMessage);
        }
    }
}
