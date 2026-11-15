using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;
using Viking.Common.UI;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    public class Structure(StructureObj data) : Viking.Objects.UIObjBase, IEquatable<Structure>, IEqualityComparer<Structure>, IContextMenu
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

            StructureObj? Obj2 = obj as StructureObj;
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
            set => modelObj.Attributes = [.. value];
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
            ObjAttribute attrib = new(tag, null);
            List<ObjAttribute> listAttributes = [.. structObj.Attributes];
            if (listAttributes.Contains(attrib))
            {
                listAttributes.Remove(attrib);
            }
            else
            {
                listAttributes.Add(attrib);
            }

            structObj.Attributes = listAttributes;
        }

        #region IUIObject Members : IUIObject

        public new event PropertyChangedEventHandler ValueChanged
        {
            add => modelObj.PropertyChanged += value;
            remove => modelObj.PropertyChanged -= value;
        }

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

                long? cellId = SbfsemToolsLauncher.GetRootStructureId(modelObj);
                if (cellId.HasValue)
                    SbfsemToolsLauncher.AddOpenMenuItem(menu, cellId.Value, locationId: null);

                // Only cells draw with a color of their own; other structures use their type's color.
                if (modelObj.TypeID == 1)
                {
                    ToolStripMenuItem newColorItem = new("New Color")
                    {
                        ToolTipText = "Gives this cell a new random display color. Only changes what you see; it is not saved."
                    };
                    newColorItem.Click += ContextMenu_OnNewColor;
                    menu.Items.Add(newColorItem);
                }

                menu.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem deleteItem = new("Delete");
                deleteItem.Click += ContextMenu_OnDelete;
                menu.Items.Add(deleteItem);

                return menu;
            }
        }

        public override void Save()
        {
            try
            {
                Store.Structures.Save();
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
            }

        }

        public override Viking.UI.Controls.GenericTreeNode CreateNode() => new Viking.UI.Controls.GenericTreeNode(this);

        public override Type[] AssignableParentTypes => [typeof(StructureObj)];

        public long[] UnfinishedBranches() => Store.Structures.GetUnfinishedBranches(ID);

        #endregion

        protected void ContextMenu_OnMorphology(object sender, EventArgs e) => Global.Export.OpenMorphology(ID);


        protected void ContextMenu_OnProperties(object sender, EventArgs e) => Viking.UI.Forms.PropertySheetForm.Show(this);

        protected void ContextMenu_OnDelete(object sender, EventArgs e) => Delete();

        protected void ContextMenu_OnNewColor(object sender, EventArgs e) => WebAnnotation.UI.StructureColors.NewColor(modelObj);

        public ContextMenuStrip ContextMenu_AddUnverifiedBranchTerminals(ContextMenuStrip menu)
        {
            ToolStripMenuItem menuUnverifiedBranchTerminals = new("Unmarked process terminals");
            menuUnverifiedBranchTerminals.DropDownOpening += OnDropDownOpeningUnverifiedBranchTerminals;
            menu.Items.Add(menuUnverifiedBranchTerminals);


            return menu;
        }

        private void OnDropDownOpeningUnverifiedBranchTerminals(object sender, EventArgs e)
        {
            ToolStripMenuItem? menuUnverifiedBranchTerminals = sender as ToolStripMenuItem;
            menuUnverifiedBranchTerminals.DropDownItems.Clear();
            bool HasMenuItems = _PopulateUnverifiedBranchTerminalsContextMenu(menuUnverifiedBranchTerminals);

            menuUnverifiedBranchTerminals.Enabled = HasMenuItems;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rootMenuItem"></param>
        /// <returns>True if the menu was populated, otherwise false.</returns>
        protected bool _PopulateUnverifiedBranchTerminalsContextMenu(ToolStripMenuItem rootMenuItem)
        {
            //            long[] Loc_Ids = Store.Structures.GetUnfinishedBranches(this.ID);
            //            List<LocationObj> listLocations = Store.Locations.GetObjectsByIDs(Loc_Ids, true);

            AnnotationService.Types.LocationPositionOnly[] LocationArray = Store.Structures.GetUnfinishedBranchesWithPosition(ID);

            Dictionary<double, List<AnnotationService.Types.LocationPositionOnly>> dictSectionToLocations = MapLocationsToSections(LocationArray);

            List<double> levels = [.. dictSectionToLocations.Keys];
            levels.Sort();
            foreach (double level in levels)
            {
                ToolStripMenuItem levelMenus = BuildContextMenusForLevel((long)level, dictSectionToLocations[level]);
                rootMenuItem.DropDownItems.Add(levelMenus);
            }

            return levels.Count > 0;
        }

        private string _LocationToString(AnnotationService.Types.LocationPositionOnly loc) => "Radius: " + loc.Radius.ToString("F1") + " X: " + loc.Position.X.ToString("F0") + " Y: " + loc.Position.Y.ToString("F0");

        private ToolStripMenuItem BuildContextMenusForLevel(long level, List<AnnotationService.Types.LocationPositionOnly> listObjs)
        {
            ToolStripMenuItem? rootMenuItem = null;
            if (listObjs.Count == 1)
            {
                AnnotationService.Types.LocationPositionOnly locObj = listObjs[0];
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
                foreach (AnnotationService.Types.LocationPositionOnly locObj in listObjs)
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

        private Dictionary<double, List<AnnotationService.Types.LocationPositionOnly>> MapLocationsToSections(IEnumerable<AnnotationService.Types.LocationPositionOnly> locations)
        {
            Dictionary<double, List<AnnotationService.Types.LocationPositionOnly>> dictSectionToLocations = [];
            foreach (AnnotationService.Types.LocationPositionOnly loc in locations)
            {
                if (!dictSectionToLocations.ContainsKey(loc.Position.Z))
                {
                    dictSectionToLocations[loc.Position.Z] = [];
                }

                dictSectionToLocations[loc.Position.Z].Add(loc);
            }

            return dictSectionToLocations;
        }

        protected void ContextMenu_SelectUnbranchedLocation(object sender, EventArgs e)
        {
            ToolStripMenuItem? menu = sender as ToolStripMenuItem;
            long locationID = (long)menu.Tag;

            LocationObj loc = Store.Locations.GetObjectByID(locationID);

            AnnotationOverlay.GoToLocation(loc);
        }

        /// <summary>
        /// Deletes this structure on the server now, from the context menu's Delete. The server also deletes every location of
        /// the structure, so the user confirms a question naming the structure and its location count, and a user who is not
        /// the structure's user also needs Review access (<see cref="StructureDeletePrompt"/>). Nothing is sent when the
        /// location count cannot be read. A failed save shows the server's fault and leaves no delete pending, so a later
        /// unrelated structure save cannot send it.
        /// </summary>
        public override void Delete()
        {
            long? locationCount = Store.Structures.NumberOfLocations(ID);
            if (locationCount is null)
                return;

            string name = StructureDeletePrompt.Name(modelObj.Type?.Name, ID, modelObj.Label);
            if (!StructureDeletePrompt.MayDelete(Username, Viking.UI.State.UserCredentials?.UserName, VolumeAccessRoles.HasReviewAccess()))
            {
                MessageBox.Show(StructureDeletePrompt.RefusalMessage(name, Username), "Delete structure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(StructureDeletePrompt.Question(name, locationCount.Value), "Delete structure",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            AnnotationService.Types.DBACTION originalAction = modelObj.DBAction;
            Store.Structures.Remove(modelObj);
            Exception? failure = StructureDeletePrompt.SaveFailure(Store.Structures.Save, () => Store.Structures.LastSaveFault);
            if (failure is null)
                return;

            // The store puts a failed save's objects back in its pending set; without this the DELETE would ride along with
            // the next structure save.
            modelObj.DBAction = originalAction;
            AnnotationOverlay.ShowFaultExceptionMsgBox(failure);
        }

        bool IEquatable<Structure>.Equals(Structure other) => modelObj.ID == other.modelObj.ID;

        public bool Equals(Structure x, Structure y) => x.modelObj.ID == y.modelObj.ID;

        public int GetHashCode(Structure obj) => obj.modelObj.GetHashCode();
    }
}
