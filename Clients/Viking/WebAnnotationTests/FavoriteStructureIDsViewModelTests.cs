using Annotation.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Viking.AnnotationServiceTypes.Interfaces;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the add/delete favorite commands of <see cref="FavoriteStructureIDsViewModel"/>, built with the
    /// parameterless constructor (the other constructor reads the static structure type store).
    /// </summary>
    [TestClass]
    public class FavoriteStructureIDsViewModelTests
    {
        /// <summary>Minimal structure type; only <see cref="ID"/> matters to the view model.</summary>
        private sealed class FakeType(ulong id) : IStructureTypeReadOnly
        {
            public ulong ID { get; } = id;
            public ulong? ParentID => null;
            public string Name => "t" + ID;
            public string Code => "c" + ID;
            public string[] Tags => [];
            public bool Equals(IStructureTypeReadOnly other) => other != null && other.ID == ID;
        }

        private static FavoriteStructureIDsViewModel Create(out List<string> changes, params ulong[] ids)
        {
            var vm = new FavoriteStructureIDsViewModel { FavoriteStructureTypeIDs = new ObservableCollection<ulong>(ids) };
            var log = new List<string>();
            vm.PropertyChanged += (_, e) => log.Add(e.PropertyName);
            changes = log;
            return vm;
        }

        [TestMethod]
        public void ParameterlessConstructor_CreatesCommandsAndNoCollections()
        {
            var vm = new FavoriteStructureIDsViewModel();
            Assert.IsNotNull(vm.AddFavoriteCommand);
            Assert.IsNotNull(vm.DeleteFavoriteCommand);
            Assert.IsNull(vm.FavoriteStructureTypeIDs);
            Assert.IsNull(vm.RootStructureTypes);
        }

        [TestMethod]
        public void CanAddAndCanDelete_ForUlongId_AreComplementary()
        {
            var vm = Create(out _, 3);
            Assert.IsFalse(vm.CanAddFavorite(3UL));
            Assert.IsTrue(vm.CanDeleteFavorite(3UL));
            Assert.IsTrue(vm.CanAddFavorite(4UL));
            Assert.IsFalse(vm.CanDeleteFavorite(4UL));
        }

        [TestMethod]
        public void CanAddAndCanDelete_ForStructureType_UseItsId()
        {
            var vm = Create(out _, 3);
            Assert.IsFalse(vm.CanAddFavorite(new FakeType(3)));
            Assert.IsTrue(vm.CanDeleteFavorite(new FakeType(3)));
            Assert.IsTrue(vm.CanAddFavorite(new FakeType(4)));
            Assert.IsFalse(vm.CanDeleteFavorite(new FakeType(4)));
        }

        [TestMethod]
        public void CanAddAndCanDelete_ConvertOtherNumericTypes()
        {
            var vm = Create(out _, 3);
            Assert.IsTrue(vm.CanDeleteFavorite(3));
            Assert.IsFalse(vm.CanAddFavorite(3L));
        }

        [TestMethod]
        public void AddFavorite_AppendsIdAndRaisesPropertyChanged()
        {
            var vm = Create(out var changes, 1);
            vm.AddFavorite(2UL);
            CollectionAssert.AreEqual(new ulong[] { 1, 2 }, vm.FavoriteStructureTypeIDs);
            CollectionAssert.AreEqual(new[] { "FavoriteStructureTypeIDs" }, changes);
        }

        [TestMethod]
        public void DeleteFavorite_RemovesIdAndRaisesPropertyChanged()
        {
            var vm = Create(out var changes, 1, 2);
            vm.DeleteFavorite(1UL);
            CollectionAssert.AreEqual(new ulong[] { 2 }, vm.FavoriteStructureTypeIDs);
            CollectionAssert.AreEqual(new[] { "FavoriteStructureTypeIDs" }, changes);
        }

        [TestMethod]
        public void DeleteFavorite_OfMissingId_LeavesCollectionButStillRaises()
        {
            var vm = Create(out var changes, 1);
            vm.DeleteFavorite(9UL);
            CollectionAssert.AreEqual(new ulong[] { 1 }, vm.FavoriteStructureTypeIDs);
            Assert.AreEqual(1, changes.Count);
        }

        [TestMethod]
        public void Commands_RouteThroughViewModelMethods()
        {
            var vm = Create(out _, 1);

            Assert.IsTrue(vm.AddFavoriteCommand.CanExecute(2UL));
            Assert.IsFalse(vm.AddFavoriteCommand.CanExecute(1UL));
            vm.AddFavoriteCommand.Execute(2UL);
            CollectionAssert.AreEqual(new ulong[] { 1, 2 }, vm.FavoriteStructureTypeIDs);

            Assert.IsTrue(vm.DeleteFavoriteCommand.CanExecute(1UL));
            Assert.IsFalse(vm.DeleteFavoriteCommand.CanExecute(5UL));
            vm.DeleteFavoriteCommand.Execute(1UL);
            CollectionAssert.AreEqual(new ulong[] { 2 }, vm.FavoriteStructureTypeIDs);
        }

        [TestMethod]
        public void FavoriteStructureTypeIDs_SetterRaisesOnlyForNewReference()
        {
            var vm = Create(out var changes, 1);
            var current = vm.FavoriteStructureTypeIDs;

            vm.FavoriteStructureTypeIDs = current;
            Assert.AreEqual(0, changes.Count);

            vm.FavoriteStructureTypeIDs = new ObservableCollection<ulong>(current);
            CollectionAssert.AreEqual(new[] { "FavoriteStructureTypeIDs" }, changes);
        }

        [TestMethod]
        public void RootStructureTypes_SetterRaisesOnlyForNewReference()
        {
            var vm = new FavoriteStructureIDsViewModel();
            var changes = new List<string>();
            vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            var roots = new ObservableCollection<IStructureTypeReadOnly> { new FakeType(1) };

            vm.RootStructureTypes = roots;
            vm.RootStructureTypes = roots;

            CollectionAssert.AreEqual(new[] { "RootStructureTypes" }, changes);
            Assert.AreSame(roots, vm.RootStructureTypes);
        }
    }
}
