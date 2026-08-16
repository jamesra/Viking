using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
#if NETFRAMEWORK
using System.Windows.Forms;
#endif
using Viking.Common;
using Viking.Common.UI;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.ViewModel
{
    public class Structure(StructureObj data) : Viking.Objects.UIObjBase, IEquatable<Structure>, IEqualityComparer<Structure>
#if NETFRAMEWORK
        , IContextMenu
#endif
    {
        public StructureObj modelObj = data;

        public override string ToString() => modelObj.ToString();

        public override int GetHashCode() => modelObj.GetHashCode();

        public override bool Equals(object obj)
        {
            if (obj is Structure Obj)
            {
                return modelObj.Equals(Obj.modelObj);
            }

            StructureObj Obj2 = obj as StructureObj;
            if (Obj2 != null)
            {
                return modelObj.Equals(Obj2);
            }

            return false;
        }

        public Structure Parent
        {
            get
            {
                if (modelObj.Parent is null)
                {
                    return null;
                }

                return new Structure(modelObj.Parent);
            }
        }

        [Column("Label")]
        public string InfoLabel
        {
            get => modelObj.Label;
            set => modelObj.Label = value;
        }

        //        [Column("ID")] This is covered by the ToString method in UI's
        public long ID => modelObj.ID;

        [Column("ParentID")]
        public long? ParentID => modelObj.ParentID;

        [Column("Last Editor")]
        public string Username => modelObj.Username;

        [Column("Num Links")]
        public int NumLinks => modelObj.NumLinks;


        [Column("Verified")]
        public bool Verified
        {
            get => modelObj.Verified;
            set => modelObj.Verified = value;
        }

        [Column("Confidence")]
        public double Confidence
        {
            get => modelObj.Confidence;
            set => modelObj.Confidence = value;
        }

        [Column("Attributes")]
        public IEnumerable<ObjAttribute> Attributes
        {
            get => modelObj.Attributes;
            set => modelObj.SetAttributes(value).Wait();
        }

        [Column("Notes")]
        public string Notes
        {
            get => modelObj.Notes;
            set => modelObj.Notes = value;
        }

        [Column("Type")]
        public StructureType Type => new(modelObj.Type);



        public static void ToggleAttribute(StructureObj structObj, string tag)
        {
            structObj.ToggleAttribute(tag).Wait();
        }

        public LocationObj Center => CenterFromLocations(Store.Locations.GetLocalObjectsForStructure(ID));

        public async System.Threading.Tasks.Task<LocationObj> GetCenterAsync()
        {
            var locations = await Store.Locations.GetStructureLocations(ID, QueryTargets.Server);
            return CenterFromLocations(locations?.ToArray() ?? []);
        }

        static LocationObj CenterFromLocations(LocationObj[] locations)
        {
                if (locations != null && locations.Length > 0)
                {
                    double sumX = 0;
                    double sumY = 0;
                    double sumZ = 0;
                    double sumRadiusSquared = 0;
                    foreach (LocationObj loc in locations)
                    {
                        double RadiusSquared = loc.Radius * loc.Radius;
                        sumX += loc.VolumePosition.X * RadiusSquared;
                        sumY += loc.VolumePosition.Y * RadiusSquared;
                        sumZ += loc.Z * RadiusSquared;
                        sumRadiusSquared += RadiusSquared;
                    }

                    sumX /= sumRadiusSquared;
                    sumY /= sumRadiusSquared;
                    sumZ /= sumRadiusSquared;

                    double meanX = (sumX) * Global.Scale.X;
                    double meanY = (sumY) * Global.Scale.Y;
                    double meanZ = (sumZ) * Global.Scale.Z;

                    Geometry.Vector3 MeanPosition = new(meanX, meanY, meanZ);

                    double minDistance = double.MaxValue;
                    int iClosest = 0;
                    for (int iLoc = 0; iLoc < locations.Length; iLoc++)
                    {
                        Geometry.Vector3 locPosition = new(locations[iLoc].VolumePosition.X * Global.Scale.X,
                                                                                    locations[iLoc].VolumePosition.Y * Global.Scale.Y,
                                                                                    locations[iLoc].Z * Global.Scale.Z);

                        double distance = Geometry.Vector3.Distance(MeanPosition, locPosition);
                        if (distance < minDistance)
                        {
                            iClosest = iLoc;
                            minDistance = distance;
                        }
                    }

                    return locations[iClosest];
                }

                return null;
        }

        #region IUIObject Members : IUIObject

        public new event PropertyChangedEventHandler ValueChanged
        {
            add => modelObj.PropertyChanged += value;
            remove => modelObj.PropertyChanged -= value;
        }

#if NETFRAMEWORK
        public override ContextMenuStrip ContextMenu
        {
            get
            {
                ContextMenuStrip menu = new();
                if (Global.Export != null)
                {
                    ToolStripMenuItem exportItem = new("Export Morphology To Tulip");
                    exportItem.Click += ContextMenu_OnMorphology;
                    menu.Items.Add(exportItem);
                }

                ToolStripMenuItem propertiesItem = new("Properties");
                propertiesItem.Click += ContextMenu_OnProperties;
                menu.Items.Add(propertiesItem);
                menu.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem deleteItem = new("Delete");
                deleteItem.Click += ContextMenu_OnDelete;
                menu.Items.Add(deleteItem);

                return menu;
            }
        }

#endif

        public override void Save()
        {
            try
            {
                Store.Structures.Save();
            }
            catch (System.ServiceModel.FaultException e)
            {
#if NETFRAMEWORK
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
#else
                System.Diagnostics.Trace.WriteLine(e);
#endif
            }

        }

#if NETFRAMEWORK
        public override Viking.UI.Controls.GenericTreeNode CreateNode() => new Viking.UI.Controls.GenericTreeNode(this);
#endif

        public override Type[] AssignableParentTypes => [typeof(StructureObj)];

        public System.Threading.Tasks.Task<long[]> UnfinishedBranches() => Store.Structures.GetUnfinishedBranches(ID);

        #endregion

#if NETFRAMEWORK
        protected void ContextMenu_OnMorphology(object sender, EventArgs e) => Global.Export.OpenMorphology(ID);


        protected void ContextMenu_OnProperties(object sender, EventArgs e) => Viking.UI.Forms.PropertySheetForm.Show(this);

        protected void ContextMenu_OnDelete(object sender, EventArgs e) => Delete();

        public ContextMenuStrip ContextMenu_AddUnverifiedBranchTerminals(ContextMenuStrip menu)
        {
            ToolStripMenuItem menuUnverifiedBranchTerminals = new("Unmarked process terminals");
            menuUnverifiedBranchTerminals.DropDownOpening += OnDropDownOpeningUnverifiedBranchTerminals;
            menu.Items.Add(menuUnverifiedBranchTerminals);


            return menu;
        }

        private async void OnDropDownOpeningUnverifiedBranchTerminals(object sender, EventArgs e)
        {
            ToolStripMenuItem menuUnverifiedBranchTerminals = sender as ToolStripMenuItem;
            menuUnverifiedBranchTerminals.DropDownItems.Clear();
            bool HasMenuItems = await PopulateUnverifiedBranchTerminalsContextMenuAsync(menuUnverifiedBranchTerminals);

            menuUnverifiedBranchTerminals.Enabled = HasMenuItems;
        }

        protected async System.Threading.Tasks.Task<bool> PopulateUnverifiedBranchTerminalsContextMenuAsync(ToolStripMenuItem rootMenuItem)
        {
            WebAnnotationModel.LocationPositionOnly[] LocationArray = await Store.Structures.GetUnfinishedBranchesWithPosition(ID);

            Dictionary<double, List<WebAnnotationModel.LocationPositionOnly>> dictSectionToLocations = MapLocationsToSections(LocationArray);

            List<double> levels = [.. dictSectionToLocations.Keys];
            levels.Sort();
            foreach (double level in levels)
            {
                ToolStripMenuItem levelMenus = BuildContextMenusForLevel((long)level, dictSectionToLocations[level]);
                rootMenuItem.DropDownItems.Add(levelMenus);
            }

            return levels.Count > 0;
        }

        private string _LocationToString(WebAnnotationModel.LocationPositionOnly loc) => "Radius: " + loc.Radius.ToString("F1") + " X: " + loc.Position.X.ToString("F0") + " Y: " + loc.Position.Y.ToString("F0");

        private ToolStripMenuItem BuildContextMenusForLevel(long level, List<WebAnnotationModel.LocationPositionOnly> listObjs)
        {
            ToolStripMenuItem rootMenuItem = null;
            if (listObjs.Count == 1)
            {
                WebAnnotationModel.LocationPositionOnly locObj = listObjs[0];
                //For a single item do not create a submenu
                string locString = _LocationToString(locObj);
                rootMenuItem = new ToolStripMenuItem(level.ToString("D4") + " - " + locString)
                {
                    Tag = locObj.ID
                };
                rootMenuItem.Click += ContextMenu_SelectUnbranchedLocation;
            }
            else
            {
                rootMenuItem = new ToolStripMenuItem(level.ToString("D4"));
                foreach (WebAnnotationModel.LocationPositionOnly locObj in listObjs)
                {
                    string locString = _LocationToString(locObj);
                    ToolStripMenuItem subItem = new(locString)
                    {
                        Tag = locObj.ID
                    };
                    subItem.Click += ContextMenu_SelectUnbranchedLocation;
                    rootMenuItem.DropDownItems.Add(subItem);
                }
            }

            return rootMenuItem;
        }

        private Dictionary<double, List<WebAnnotationModel.LocationPositionOnly>> MapLocationsToSections(IEnumerable<WebAnnotationModel.LocationPositionOnly> locations)
        {
            Dictionary<double, List<WebAnnotationModel.LocationPositionOnly>> dictSectionToLocations = [];
            foreach (WebAnnotationModel.LocationPositionOnly loc in locations)
            {
                if (!dictSectionToLocations.ContainsKey(loc.Position.Z))
                {
                    dictSectionToLocations[loc.Position.Z] = [];
                }

                dictSectionToLocations[loc.Position.Z].Add(loc);
            }

            return dictSectionToLocations;
        }

        protected async void ContextMenu_SelectUnbranchedLocation(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = sender as ToolStripMenuItem;
            long locationID = (long)menu.Tag;

            LocationObj loc = await Store.Locations.GetObjectByID(locationID);

            AnnotationOverlay.GoToLocation(loc);
        }
#endif

        public override void Delete() => _ = Store.Structures.Remove(modelObj);/*
            Structure OriginalParent = this.Parent;
            this.Parent = null;

            DBACTION originalAction = this.DBAction;
            this.DBAction = DBACTION.DELETE;

            bool success = Store.Structures.Save();
            if (!success)
            {
                //Write straight to data since we have an assert to check whether an object is being deleted, but
                //in this case we know it is ok
                this.Data.DBAction = originalAction;
                this.Parent = OriginalParent;
            }
            */

        bool IEquatable<Structure>.Equals(Structure other) => modelObj.ID == other.modelObj.ID;

        public bool Equals(Structure x, Structure y) => x.modelObj.ID == y.modelObj.ID;

        public int GetHashCode(Structure obj) => obj.modelObj.GetHashCode();
    }
}
