using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.WPF.Models;
using Viking.UI.WPF.ViewModels;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the segmentation-service picker view model using only a null bearer token and preloaded services, so no
    /// network call is made and no token or credential handling is exercised.
    /// </summary>
    [TestClass]
    public class SegmentationServiceSelectionViewModelTests
    {
        private static SegmentationServiceSelectionViewModel NoToken(string? preselected = null) =>
            new SegmentationServiceSelectionViewModel(null, "https://identity.invalid", preselected);

        private static SegmentationServiceSelectionViewModel Preloaded(Dictionary<long, object> services, string? preselected = null) =>
            new SegmentationServiceSelectionViewModel(null, "https://identity.invalid", preselected, AsJsonElements(services));

        // Production values arrive as JsonElement. Plain CLR objects hit a separate parse path that loses every field (see the bug candidate in the ledger), so tests feed JsonElement.
        private static Dictionary<long, object> AsJsonElements(Dictionary<long, object> services) =>
            services.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    if (kvp.Value is JsonElement)
                    {
                        return kvp.Value;
                    }
                    using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(kvp.Value));
                    return (object)doc.RootElement.Clone();
                });

        private static object Svc(string? name, string? endpoint) => new Dictionary<string, object?> { ["name"] = name, ["endpoint"] = endpoint };

        private static string[] Names(SegmentationServiceSelectionViewModel vm) => vm.ServiceNodes.Select(n => n.Name).ToArray();

        // ---- no-token construction ----

        [TestMethod]
        public void Constructor_NoTokenNoPreload_ShowsManualEntryAndStatusMessage()
        {
            var vm = NoToken();

            Assert.AreEqual("No authentication token available. Use manual entry or recent services.", vm.StatusMessage);
            Assert.IsTrue(vm.ShowManualEntry);
            Assert.IsFalse(vm.IsLoading);
            Assert.AreEqual(0, vm.ServiceNodes.Count);
            Assert.AreEqual(0, vm.RecentServices.Count);
            Assert.IsNull(vm.SelectedService);
            Assert.AreEqual(string.Empty, vm.SelectedServiceEndpoint);
        }

        [TestMethod]
        public void Constructor_NoTokenNoPreload_LoadCompletesImmediately()
        {
            var vm = NoToken();

            Assert.IsTrue(vm.WhenServicesLoadedAsync().IsCompleted);
        }

        [TestMethod]
        public void Constructor_EmptyPreloadedDictionary_IsTreatedAsNoServices()
        {
            var vm = Preloaded(new Dictionary<long, object>());

            Assert.AreEqual("No authentication token available. Use manual entry or recent services.", vm.StatusMessage);
            Assert.IsTrue(vm.ShowManualEntry);
        }

        [TestMethod]
        public void Constructor_NoToken_PreselectedEndpointFillsManualEntry()
        {
            var vm = NoToken("https://seg.example/api");

            Assert.AreEqual("https://seg.example/api", vm.ManualServiceEndpoint);
            Assert.AreEqual("https://seg.example/api", vm.SelectedServiceEndpoint);
        }

        [TestMethod]
        public void Constructor_BlankPreselectedEndpoint_LeavesManualEntryUnset()
        {
            var vm = NoToken("   ");

            Assert.IsNull(vm.ManualServiceEndpoint);
        }

        // ---- preloaded services ----

        [TestMethod]
        public void Constructor_Preloaded_StatusReportsCountAndHidesManualEntry()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a"), [2] = Svc("B", "http://b") });

            Assert.AreEqual("Loaded 2 segmentation service(s)", vm.StatusMessage);
            Assert.IsFalse(vm.ShowManualEntry);
            Assert.IsFalse(vm.IsLoading);
            Assert.IsTrue(vm.WhenServicesLoadedAsync().IsCompleted);
        }

        [TestMethod]
        public void Constructor_Preloaded_OrdersNodesByNameIgnoringCase()
        {
            var vm = Preloaded(new Dictionary<long, object>
            {
                [1] = Svc("beta", "http://1"),
                [2] = Svc("Alpha", "http://2"),
                [3] = Svc("alpine", "http://3"),
                [4] = Svc("Zulu", "http://4"),
            });

            CollectionAssert.AreEqual(new[] { "Alpha", "alpine", "beta", "Zulu" }, Names(vm));
        }

        [TestMethod]
        public void Constructor_Preloaded_NodeCarriesIdNameEndpointAndIsNotCategory()
        {
            var vm = Preloaded(new Dictionary<long, object> { [42] = new Dictionary<string, object?> { ["name"] = "Seg", ["description"] = "desc", ["endpoint"] = "http://seg" } });

            var node = vm.ServiceNodes.Single();
            Assert.IsFalse(node.IsCategory);
            Assert.AreEqual("Seg", node.Name);
            Assert.AreEqual(42L, node.Service.Id);
            Assert.AreEqual("desc", node.Service.Description);
            Assert.AreEqual("http://seg", node.Service.Endpoint);
        }

        [TestMethod]
        public void Parse_ServiceWithoutName_GetsDefaultNameFromId()
        {
            var vm = Preloaded(new Dictionary<long, object> { [7] = new Dictionary<string, object?> { ["endpoint"] = "http://x" } });

            Assert.AreEqual("Segmentation Service 7", vm.ServiceNodes.Single().Name);
        }

        [TestMethod]
        public void Parse_ServiceWithNullName_GetsDefaultNameFromId()
        {
            var vm = Preloaded(new Dictionary<long, object> { [9] = Svc(null, "http://x") });

            Assert.AreEqual("Segmentation Service 9", vm.ServiceNodes.Single().Name);
        }

        [TestMethod]
        public void Parse_CapitalizedEndpointProperty_IsRead()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = new Dictionary<string, object?> { ["name"] = "A", ["Endpoint"] = "http://cap" } });

            Assert.AreEqual("http://cap", vm.ServiceNodes.Single().Service.Endpoint);
        }

        [TestMethod]
        public void Parse_EndpointInMetadata_IsUsedWhenRootHasNone()
        {
            var vm = Preloaded(new Dictionary<long, object>
            {
                [1] = new Dictionary<string, object?> { ["name"] = "A", ["metadata"] = new Dictionary<string, object?> { ["endpoint"] = "http://meta" } },
            });

            Assert.AreEqual("http://meta", vm.ServiceNodes.Single().Service.Endpoint);
        }

        [TestMethod]
        public void Parse_CapitalizedEndpointInMetadata_IsUsed()
        {
            var vm = Preloaded(new Dictionary<long, object>
            {
                [1] = new Dictionary<string, object?> { ["name"] = "A", ["metadata"] = new Dictionary<string, object?> { ["Endpoint"] = "http://metacap" } },
            });

            Assert.AreEqual("http://metacap", vm.ServiceNodes.Single().Service.Endpoint);
        }

        [TestMethod]
        public void Parse_BlankRootEndpoint_FallsBackToMetadata()
        {
            var vm = Preloaded(new Dictionary<long, object>
            {
                [1] = new Dictionary<string, object?>
                {
                    ["name"] = "A",
                    ["endpoint"] = "  ",
                    ["metadata"] = new Dictionary<string, object?> { ["endpoint"] = "http://fallback" },
                },
            });

            Assert.AreEqual("http://fallback", vm.ServiceNodes.Single().Service.Endpoint);
        }

        [TestMethod]
        public void Parse_RootEndpointWinsOverMetadata()
        {
            var vm = Preloaded(new Dictionary<long, object>
            {
                [1] = new Dictionary<string, object?>
                {
                    ["name"] = "A",
                    ["endpoint"] = "http://root",
                    ["metadata"] = new Dictionary<string, object?> { ["endpoint"] = "http://meta" },
                },
            });

            Assert.AreEqual("http://root", vm.ServiceNodes.Single().Service.Endpoint);
        }

        [TestMethod]
        public void Parse_MetadataThatIsNotAnObject_IsIgnored()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = new Dictionary<string, object?> { ["name"] = "A", ["metadata"] = "text" } });

            Assert.IsNull(vm.ServiceNodes.Single().Service.Endpoint);
        }

        [TestMethod]
        public void Parse_JsonElementValue_IsReadDirectly()
        {
            using JsonDocument doc = JsonDocument.Parse("{\"name\":\"FromJson\",\"description\":\"d\",\"endpoint\":\"http://json\"}");
            var vm = Preloaded(new Dictionary<long, object> { [3] = doc.RootElement.Clone() });

            var service = vm.ServiceNodes.Single().Service;
            Assert.AreEqual("FromJson", service.Name);
            Assert.AreEqual("d", service.Description);
            Assert.AreEqual("http://json", service.Endpoint);
        }

        [TestMethod]
        public void Parse_ServiceWithNoEndpoint_StillListedWithNullEndpoint()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("NoEndpoint", null) });

            Assert.AreEqual(1, vm.ServiceNodes.Count);
            Assert.IsNull(vm.ServiceNodes[0].Service.Endpoint);
        }

        [TestMethod]
        public void Comparer_MixedConstructedAndParsedElementsSortTogetherByName()
        {
            using JsonDocument doc = JsonDocument.Parse("{\"name\":\"Mid\",\"endpoint\":\"http://m\"}");
            var vm = Preloaded(new Dictionary<long, object>
            {
                [1] = Svc("zeta", "http://z"),
                [2] = doc.RootElement.Clone(),
                [3] = Svc("Alpha", "http://a"),
            });

            CollectionAssert.AreEqual(new[] { "Alpha", "Mid", "zeta" }, Names(vm));
        }

        [TestMethod]
        public void Comparer_UnnamedServiceSortsFirst()
        {
            var vm = Preloaded(new Dictionary<long, object>
            {
                [1] = Svc("Alpha", "http://a"),
                [2] = new Dictionary<string, object?> { ["endpoint"] = "http://noname" },
            });

            CollectionAssert.AreEqual(new[] { "Segmentation Service 2", "Alpha" }, Names(vm));
        }

        [TestMethod]
        public void Comparer_EqualNamesIgnoringCase_KeepInDictionaryOrder()
        {
            var vm = Preloaded(new Dictionary<long, object>
            {
                [1] = Svc("same", "http://first"),
                [2] = Svc("SAME", "http://second"),
            });

            Assert.AreEqual("http://first", vm.ServiceNodes[0].Service.Endpoint);
            Assert.AreEqual("http://second", vm.ServiceNodes[1].Service.Endpoint);
        }

        // ---- preselect ----

        [TestMethod]
        public void Preselect_MatchingPreloadedEndpoint_SelectsNodeIgnoringCase()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a"), [2] = Svc("B", "http://b/Seg") }, "HTTP://B/SEG");

            Assert.AreEqual("B", vm.SelectedService.Name);
            Assert.AreEqual("http://b/Seg", vm.SelectedService.Service.Endpoint);
        }

        [TestMethod]
        public void PreselectService_NoMatch_KeepsEndpointAsManualEntry()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a") });

            vm.PreselectService("http://elsewhere");

            Assert.IsNull(vm.SelectedService);
            Assert.AreEqual("http://elsewhere", vm.ManualServiceEndpoint);
            Assert.AreEqual("http://elsewhere", vm.SelectedServiceEndpoint);
        }

        [TestMethod]
        public void PreselectService_Match_SelectsNodeAndClearsManualEntry()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a") });

            vm.PreselectService("http://A");

            Assert.AreEqual("A", vm.SelectedService.Name);
            Assert.AreEqual(string.Empty, vm.ManualServiceEndpoint);
            Assert.AreEqual("http://a", vm.SelectedServiceEndpoint);
        }

        [TestMethod]
        public void PreselectService_BlankEndpoint_ChangesNothing()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a") });
            var changed = new List<string?>();
            vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            vm.PreselectService(" ");
            vm.PreselectService(null);

            Assert.AreEqual(0, changed.Count);
            Assert.IsNull(vm.ManualServiceEndpoint);
            Assert.IsNull(vm.SelectedService);
        }

        // ---- recent services ----

        [TestMethod]
        public void AddRecentService_StoresEndpointNameAndEmptyDescription()
        {
            var vm = NoToken();

            vm.AddRecentService("http://r1", "First");

            var info = vm.RecentServices.Single();
            Assert.AreEqual("http://r1", info.Endpoint);
            Assert.AreEqual("First", info.Name);
            Assert.AreEqual(string.Empty, info.Description);
        }

        [TestMethod]
        public void AddRecentService_BlankName_UsesEndpointAsName()
        {
            var vm = NoToken();

            vm.AddRecentService("http://r1", null);
            vm.AddRecentService("http://r2", "   ");

            Assert.AreEqual("http://r1", vm.RecentServices[0].Name);
            Assert.AreEqual("http://r2", vm.RecentServices[1].Name);
        }

        [TestMethod]
        public void AddRecentService_BlankEndpoint_IsIgnored()
        {
            var vm = NoToken();

            vm.AddRecentService(null, "Name");
            vm.AddRecentService("", "Name");
            vm.AddRecentService("  ", "Name");

            Assert.AreEqual(0, vm.RecentServices.Count);
        }

        [TestMethod]
        public void AddRecentService_KeepsInsertionOrder()
        {
            var vm = NoToken();

            vm.AddRecentService("http://b", "B");
            vm.AddRecentService("http://a", "A");

            CollectionAssert.AreEqual(new[] { "B", "A" }, vm.RecentServices.Select(r => r.Name).ToArray());
        }

        [TestMethod]
        public void AddRecentService_SameEndpointTwice_IsListedTwice()
        {
            // Pins current behavior: there is no de-duplication of the recent list.
            var vm = NoToken();

            vm.AddRecentService("http://dup", "One");
            vm.AddRecentService("http://dup", "Two");

            Assert.AreEqual(2, vm.RecentServices.Count);
        }

        [TestMethod]
        public void SelectMostRecentServiceIfAvailable_Empty_LeavesSelectionNull()
        {
            var vm = NoToken();

            vm.SelectMostRecentServiceIfAvailable();

            Assert.IsNull(vm.SelectedService);
        }

        [TestMethod]
        public void SelectMostRecentServiceIfAvailable_SelectsFirstRecentAsLeafNode()
        {
            var vm = NoToken();
            vm.AddRecentService("http://first", "First");
            vm.AddRecentService("http://second", "Second");

            vm.SelectMostRecentServiceIfAvailable();

            Assert.AreSame(vm.RecentServices[0], vm.SelectedService.Service);
            Assert.AreEqual("First", vm.SelectedService.Name);
            Assert.IsFalse(vm.SelectedService.IsCategory);
            Assert.AreEqual("http://first", vm.SelectedServiceEndpoint);
        }

        // ---- manual endpoint and selected endpoint ----

        [TestMethod]
        public void SelectedServiceEndpoint_ManualEntryWinsOverSelectedService()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a") });
            vm.SelectedService = vm.ServiceNodes[0];

            vm.ManualServiceEndpoint = "http://manual";

            Assert.AreEqual("http://manual", vm.SelectedServiceEndpoint);
        }

        [TestMethod]
        public void SelectedService_ChosenWhileManualEntryPresent_ClearsManualEntry()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a") });
            vm.ManualServiceEndpoint = "http://manual";

            vm.SelectedService = vm.ServiceNodes[0];

            Assert.AreEqual(string.Empty, vm.ManualServiceEndpoint);
            Assert.AreEqual("http://a", vm.SelectedServiceEndpoint);
        }

        [TestMethod]
        public void SelectedService_CategoryNodeWithoutService_KeepsManualEntry()
        {
            var vm = NoToken();
            vm.ManualServiceEndpoint = "http://manual";

            vm.SelectedService = new SegmentationServiceTreeNode { Name = "Category", IsCategory = true };

            Assert.AreEqual("http://manual", vm.ManualServiceEndpoint);
            Assert.AreEqual("http://manual", vm.SelectedServiceEndpoint);
        }

        [TestMethod]
        public void SelectedServiceEndpoint_WhitespaceManualEntry_FallsBackToSelectedService()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a") });
            vm.SelectedService = vm.ServiceNodes[0];

            vm.ManualServiceEndpoint = "   ";

            Assert.AreEqual("http://a", vm.SelectedServiceEndpoint);
        }

        [TestMethod]
        public void ManualServiceEndpoint_ChangeRaisesBothPropertiesOnce_SameValueDoesNot()
        {
            var vm = NoToken();
            var changed = new List<string?>();
            vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            vm.ManualServiceEndpoint = "http://m";
            vm.ManualServiceEndpoint = "http://m";

            CollectionAssert.AreEqual(new[] { nameof(vm.ManualServiceEndpoint), nameof(vm.SelectedServiceEndpoint) }, changed);
        }

        [TestMethod]
        public void SelectedService_ChangeRaisesSelectedServiceAndEndpoint()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a") });
            var changed = new List<string?>();
            vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            vm.SelectedService = vm.ServiceNodes[0];
            vm.SelectedService = vm.ServiceNodes[0];

            CollectionAssert.AreEqual(new[] { nameof(vm.SelectedService), nameof(vm.SelectedServiceEndpoint) }, changed);
        }

        [TestMethod]
        public void ShowManualEntry_ChangeRaisesPropertyChangedOnce()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a") });
            var changed = new List<string?>();
            vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            vm.ShowManualEntry = true;
            vm.ShowManualEntry = true;

            CollectionAssert.AreEqual(new[] { nameof(vm.ShowManualEntry) }, changed);
        }

        [TestMethod]
        public void StatusMessage_SameValueDoesNotRaise()
        {
            var vm = NoToken();
            int changed = 0;
            vm.PropertyChanged += (s, e) => changed++;

            vm.StatusMessage = vm.StatusMessage;
            vm.StatusMessage = "other";

            Assert.AreEqual(1, changed);
        }

        // ---- commands ----

        [TestMethod]
        public void SelectCommand_NoEndpoint_CannotExecuteAndRaisesNothing()
        {
            var vm = NoToken();
            int raised = 0;
            vm.SegmentationServiceSelected += (s, e) => raised++;

            Assert.IsFalse(vm.SelectCommand.CanExecute(null));
            vm.SelectCommand.Execute(null);

            Assert.AreEqual(0, raised);
            Assert.AreEqual("Segmentation service endpoint is not available. Please enter one manually.", vm.StatusMessage);
        }

        [TestMethod]
        public void SelectCommand_ManualEndpoint_RaisesTrimmedEndpointNotNone()
        {
            var vm = NoToken();
            object? sender = null;
            SegmentationServiceSelectedEventArgs? args = null;
            int raised = 0;
            vm.SegmentationServiceSelected += (s, e) => { sender = s; args = e; raised++; };

            vm.ManualServiceEndpoint = "  http://manual/seg  ";
            Assert.IsTrue(vm.SelectCommand.CanExecute(null));
            vm.SelectCommand.Execute(null);

            Assert.AreEqual(1, raised);
            Assert.AreSame(vm, sender);
            Assert.AreEqual("http://manual/seg", args!.Endpoint);
            Assert.IsFalse(args.IsNone);
        }

        [TestMethod]
        public void SelectCommand_SelectedService_RaisesItsEndpoint()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a") });
            SegmentationServiceSelectedEventArgs? args = null;
            vm.SegmentationServiceSelected += (s, e) => args = e;

            vm.SelectedService = vm.ServiceNodes[0];
            vm.SelectCommand.Execute(null);

            Assert.AreEqual("http://a", args!.Endpoint);
        }

        [TestMethod]
        public void SelectCommand_SelectedServiceWithNoEndpoint_CannotExecute()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", null) });

            vm.SelectedService = vm.ServiceNodes[0];

            Assert.IsFalse(vm.SelectCommand.CanExecute(null));
        }

        [TestMethod]
        public void SelectCommand_WhileLoading_CannotExecuteEvenWithEndpoint()
        {
            var vm = NoToken();
            vm.ManualServiceEndpoint = "http://manual";

            vm.IsLoading = true;

            Assert.IsFalse(vm.SelectCommand.CanExecute(null));
            vm.IsLoading = false;
            Assert.IsTrue(vm.SelectCommand.CanExecute(null));
        }

        [TestMethod]
        public void IsLoading_ChangeRaisesPropertyChangedAndCanExecuteChanged_SameValueDoesNot()
        {
            var vm = NoToken();
            var changed = new List<string?>();
            vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
            int canExecuteChanged = 0;
            vm.SelectCommand.CanExecuteChanged += (s, e) => canExecuteChanged++;

            vm.IsLoading = true;
            vm.IsLoading = true;

            CollectionAssert.AreEqual(new[] { nameof(vm.IsLoading) }, changed);
            Assert.AreEqual(1, canExecuteChanged);
        }

        [TestMethod]
        public void SelectCommand_CanExecuteChangedRaisedWhenManualEntryOrSelectionChanges()
        {
            var vm = Preloaded(new Dictionary<long, object> { [1] = Svc("A", "http://a") });
            int select = 0, copy = 0;
            vm.SelectCommand.CanExecuteChanged += (s, e) => select++;
            vm.CopyEndpointCommand.CanExecuteChanged += (s, e) => copy++;

            vm.ManualServiceEndpoint = "http://m";
            vm.SelectedService = vm.ServiceNodes[0];

            // Manual entry change, then the selection change (which also clears the manual entry).
            Assert.AreEqual(3, select);
            Assert.AreEqual(3, copy);
        }

        [TestMethod]
        public void CopyEndpointCommand_CanExecuteOnlyWithAnEndpoint()
        {
            var vm = NoToken();
            Assert.IsFalse(vm.CopyEndpointCommand.CanExecute(null));

            vm.ManualServiceEndpoint = "http://m";

            Assert.IsTrue(vm.CopyEndpointCommand.CanExecute(null));
        }

        [TestMethod]
        public void NoneCommand_AlwaysCanExecuteAndRaisesSkippedOnlyNotSelected()
        {
            var vm = NoToken();
            object? sender = null;
            int skipped = 0, selected = 0, cancelled = 0;
            vm.SegmentationSelectionSkipped += (s, e) => { sender = s; skipped++; };
            vm.SegmentationServiceSelected += (s, e) => selected++;
            vm.SelectionCancelled += (s, e) => cancelled++;

            Assert.IsTrue(vm.NoneCommand.CanExecute(null));
            vm.NoneCommand.Execute(null);

            Assert.AreEqual(1, skipped);
            Assert.AreSame(vm, sender);
            Assert.AreEqual(0, selected);
            Assert.AreEqual(0, cancelled);
        }

        [TestMethod]
        public void CancelCommand_AlwaysCanExecuteAndRaisesCancelledOnly()
        {
            var vm = NoToken();
            object? sender = null;
            int skipped = 0, selected = 0, cancelled = 0;
            vm.SegmentationSelectionSkipped += (s, e) => skipped++;
            vm.SegmentationServiceSelected += (s, e) => selected++;
            vm.SelectionCancelled += (s, e) => { sender = s; cancelled++; };

            Assert.IsTrue(vm.CancelCommand.CanExecute(null));
            vm.CancelCommand.Execute(null);

            Assert.AreEqual(1, cancelled);
            Assert.AreSame(vm, sender);
            Assert.AreEqual(0, skipped);
            Assert.AreEqual(0, selected);
        }

        [TestMethod]
        public void NoneAndCancelCommands_WithoutSubscribers_DoNotThrow()
        {
            var vm = NoToken();

            vm.NoneCommand.Execute(null);
            vm.CancelCommand.Execute(null);
        }

        [TestMethod]
        public void LoadServicesCommand_NoToken_SetsStatusAndDoesNotLoad()
        {
            var vm = NoToken();
            vm.StatusMessage = "stale";

            Assert.IsTrue(vm.LoadServicesCommand.CanExecute(null));
            vm.LoadServicesCommand.Execute(null);

            Assert.AreEqual("No authentication token available", vm.StatusMessage);
            Assert.IsFalse(vm.IsLoading);
            Assert.AreEqual(0, vm.ServiceNodes.Count);
        }

        // ---- tree node ----

        [TestMethod]
        public void TreeNode_PropertiesRaiseOnChangeOnly()
        {
            var node = new SegmentationServiceTreeNode();
            var changed = new List<string?>();
            ((INotifyPropertyChanged)node).PropertyChanged += (s, e) => changed.Add(e.PropertyName);
            var info = new SegmentationServiceInfo { Name = "x" };

            node.Name = "n";
            node.Name = "n";
            node.IsCategory = true;
            node.IsCategory = true;
            node.Service = info;
            node.Service = info;
            node.IsExpanded = true;
            node.IsExpanded = true;

            CollectionAssert.AreEqual(
                new[] { nameof(node.Name), nameof(node.IsCategory), nameof(node.Service), nameof(node.IsExpanded) },
                changed);
            Assert.IsNull(node.Children);
        }

        [TestMethod]
        public void SelectedService_ChosenFromNestedChildren_IsFoundByPreselect()
        {
            var vm = NoToken();
            var leaf = new SegmentationServiceTreeNode { Name = "Leaf", Service = new SegmentationServiceInfo { Name = "Leaf", Endpoint = "http://nested" } };
            var parent = new SegmentationServiceTreeNode
            {
                Name = "Group",
                IsCategory = true,
                Children = new System.Collections.ObjectModel.ObservableCollection<SegmentationServiceTreeNode> { leaf },
            };
            vm.ServiceNodes.Add(parent);

            vm.PreselectService("http://NESTED");

            Assert.AreSame(leaf, vm.SelectedService);
            Assert.AreEqual(string.Empty, vm.ManualServiceEndpoint);
        }

        [TestMethod]
        public void SelectedServiceEventArgs_DefaultsToNoEndpointAndNotNone()
        {
            var args = new SegmentationServiceSelectedEventArgs();

            Assert.IsNull(args.Endpoint);
            Assert.IsFalse(args.IsNone);
        }
    }
}
