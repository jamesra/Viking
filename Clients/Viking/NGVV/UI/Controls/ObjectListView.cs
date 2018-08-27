using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
using Viking.Common;
using Viking.Common.UI;


namespace Viking.UI.BaseClasses
{
    public partial class ObjectListView : System.Windows.Forms.ListView
    {
        [Browsable(true)]
		[Category("Data")]
		public System.Type DisplayType
		{
			get { return _DisplayType; }
			set 
			{
				if(_DisplayType != value)
				{
					_DisplayType = value;
					SetDisplayType(value);
				}
			}
		}

		[Serializable()]
		public struct ColumnVisibilitySetting
		{
			public ColumnVisibilitySetting(bool isVisibile, int width)
			{
				this.isVisibile = isVisibile;
				this.width = width;
			}
			public bool isVisibile;
			public int width;
		}

        [Browsable(true)]
        public bool ShowPropertiesOnDoubleClick = true;

		public System.EventHandler OnContextMenuNewClick = null;
		public System.EventHandler OnContextMenuRemoveClick = null;

		private PropertyInfo[] ColumnProperties = new PropertyInfo[0]; 
			
		private System.EventHandler OnObjectSaveHandler = null; 
		private System.EventHandler OnObjectDeleteHandler = null;
        private System.ComponentModel.PropertyChangedEventHandler OnObjectValueChangedHandler = null;
		private EventHandler OnContextColumnMenuHandler = null;

		private System.Type _DisplayType = null;

		/// <summary>
		/// holds the menu items from when the context menu is assigned to
		/// </summary>
		private MenuItem[] menuItemsFromHost = new MenuItem[0];
		/// <summary>
		/// saves the args from the last mouse up event
		/// </summary>
		private MouseEventArgs lastMouseUpEventArgs = null;
		
		/// <summary>
		/// stores the columnsetting structs for the columns
		/// </summary>
		private Dictionary<string, ColumnVisibilitySetting> _ColumnSettingsHashtable = null;

		private int _ColumnDefaultWidth = 75;

		public ObjectListView()
		{
			OnObjectSaveHandler = new System.EventHandler(this.OnObjectSave);
			OnObjectDeleteHandler = new System.EventHandler(this.OnObjectDelete);
            OnObjectValueChangedHandler = new System.ComponentModel.PropertyChangedEventHandler(this.OnObjectValueChanged);
			OnContextColumnMenuHandler = new EventHandler(this.OnContextMenuColumnVisibility);

//			this.SmallImageList = SharedResources.SmallIconImageList; 

			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitForm call
			this.View = System.Windows.Forms.View.Details;
			this.FullRowSelect = true; 
			this.AllowColumnReorder = true; 

			// create our handler for when the parent form is closing
			OnParentFormClosing = new CancelEventHandler(parentForm_Closing);
		}

        /// <summary>
        /// Configures the display of the list view control based upon the attributes exposed by the object
        /// </summary>
        /// <param name="ObjType"></param>
		public void SetDisplayType(System.Type ObjType)
		{
			List<string> ColumnList = new List<String>(); 
			PropertyInfo[] Properties = ObjType.GetProperties();
            List<PropertyInfo> listPropInfoForColumn = new List<PropertyInfo>(); 
			foreach(PropertyInfo Property in Properties)
			{
                ColumnAttribute[] Attributes = Property.GetCustomAttributes(typeof(ColumnAttribute), true) as ColumnAttribute[];
                foreach (ColumnAttribute Attrib in Attributes)
				{
					ColumnList.Add(Attrib.ColumnName);
                    listPropInfoForColumn.Add(Property); 
				}

                if (Property.GetCustomAttributes(typeof(ThisToOneRelationAttribute), true).Length > 0)
                {
                    ColumnList.Add(Property.Name);
                    listPropInfoForColumn.Add(Property); 
                }
			}

			Trace.WriteLineIf(ColumnList.Count == 0, "No columns defined for type: " + ObjType.ToString(), "UI");
			
			this.BeginUpdate();

			this.Items.Clear();
			this.Columns.Clear();

			this.Columns.Add("Name", 150, HorizontalAlignment.Left); 

			foreach(string ColumnName in ColumnList)
			{
				this.Columns.Add(ColumnName, _ColumnDefaultWidth, HorizontalAlignment.Left); 
			}

			this.EndUpdate();

            this.ColumnProperties = listPropInfoForColumn.ToArray(); 
            /*
			this.ColumnProperties = new PropertyInfo[ColumnList.Count]; 
			for(int iColumn = 0; iColumn < ColumnList.Count; iColumn++)
			{
				ColumnHeader Column = this.Columns[iColumn + 1];
				ColumnProperties[iColumn] = ObjType.GetProperty(listPropInfoForColumn[iColumn].Name);
				Debug.Assert(ColumnProperties[iColumn] != null);
			}
             */

			LoadColumnVisibilitySettings();
		}

		protected System.Type GetTypeForArray(object[] Objects)
		{
			if(Objects == null)
				return null; 

			return Objects.GetType().GetElementType(); 
		}

		[Browsable(false)]
		public Viking.Common.IUIObject[] SelectedObjects
		{
			get
			{
                IUIObject[] Objs = new IUIObject[this.SelectedItems.Count]; 
				
				for(int i = 0; i < SelectedItems.Count; i++)
				{
                    Objs[i] = SelectedItems[i].Tag as IUIObject; 
				}	

				return Objs; 
			}
		}	

		[Browsable(false)]
        public Viking.Common.IUIObject[] Objects
		{
			get
			{
                Viking.Common.IUIObject[] Objs = new Viking.Common.IUIObject[this.Items.Count]; 
				
				for(int i = 0; i < this.Items.Count; i++)
				{
                    Objs[i] = Items[i].Tag as Viking.Common.IUIObject; 
				}	

				return Objs; 
			}
			set
			{
				DisplayObjects(value); 
			}
		}

        public void SelectObject(object Object)
		{
			foreach(ListViewItem Item in this.Items)
			{
				if(Item.Tag.Equals(Object) == true)
				{
					Item.Selected = true;
				}
				else
					Item.Selected = false; 
			}
		}

        protected ListViewItem ItemForObject(object Object)
		{
			foreach(ListViewItem Item in this.Items)
			{
				if(Item.Tag.Equals(Object) == true)
				{
					return Item; 
				}
			}

			return null;
		}

        private void AddObjectEvents(object Object)
		{
            IUIObject uiObj = Object as IUIObject;
            Debug.Assert(uiObj != null);
            uiObj.AfterSave += this.OnObjectSaveHandler;
            uiObj.BeforeDelete += this.OnObjectDeleteHandler;
            uiObj.ValueChanged += this.OnObjectValueChangedHandler; 
		}

        private void RemoveObjectEvents(object Object)
		{
            IUIObject uiObj = Object as IUIObject;
            Debug.Assert(uiObj != null);
            uiObj.AfterSave -= this.OnObjectSaveHandler;
            uiObj.BeforeDelete -= this.OnObjectDeleteHandler;
            uiObj.ValueChanged -= this.OnObjectValueChangedHandler; 
		}

        public ListViewItem AddObject(object Object)
		{
			Debug.Assert(Object != null); 
			if(Object == null)
				return null;

            //If we haven't initialized the columns then do so.
            if (this.Columns.Count == 0)
            {
                SetDisplayType(typeof(Object)); 
            }

			//Create SubItems, the first SubItem is the Object.ToString method. So the index is offset by one in the loop
			ListViewItem.ListViewSubItem[] SubItems = new ListViewItem.ListViewSubItem[this.Columns.Count];

			Debug.Assert(this.Columns.Count > 0); 

			SubItems[0] = new ListViewItem.ListViewSubItem(); 
			SubItems[0].Text = Object.ToString(); 

			for(int iColumn = 0; iColumn < this.ColumnProperties.Length; iColumn++)
			{
				PropertyInfo Property = ColumnProperties[iColumn];
                //TODO: Does this work if Object is cast to an interface?
				object Value = Property.GetValue(Object, null);

				SubItems[iColumn + 1] = new ListViewItem.ListViewSubItem();
				SubItems[iColumn + 1].Text = "";

                if (Value != null)
                {
                    SubItems[iColumn + 1].Text = Value.ToString();

                    if(Property.PropertyType.IsValueType)
                    {
                        SubItems[iColumn + 1].Tag = Value;
                    }
                }
			}

            //Removed until I add images again
//			ListViewItem Item = new ListViewItem(SubItems, Object.TreeImageIndex);
            ListViewItem Item = new ListViewItem(SubItems, null);
			Item.Tag = Object;
			this.Items.Add(Item);

			AddObjectEvents(Object); 

			return Item;
		}

        public void RemoveObject(Viking.Common.IUIObject Object)
		{
			Debug.Assert(Object != null); 
			if(Object == null)
				return;

			ListViewItem Item = ItemForObject(Object); 
			if(Item == null)
			{
				Debug.Write("Calling DataObjectListView::RemoveObject for object not in list"); 
				return; 
			}

            IUIObject Obj = Item.Tag as IUIObject; 
			RemoveObjectEvents(Obj);

			Item.Remove(); 
			Item.Tag = null;

			return;
		}

        public void RemoveAllObjects()
        {
            IUIObject[] objects = this.Objects;
            foreach(IUIObject obj in objects)
            {
                RemoveObjectEvents(obj); 
            }
            
            this.Clear();
        }

        public void DisplayObjects(object[] Objects)
		{
            

			SetDisplayType( GetTypeForArray(Objects) );


			this.BeginUpdate();
			this.Items.Clear();
			//PlantMap.Database.Store.OpenConnection();

			for(int i = 0; i < Objects.Length; i++)
			{
				AddObject(Objects[i]);

				Application.DoEvents();

                if (this.IsDisposed)
                    return; 
			}

			//PlantMap.Database.Store.CloseConnection();
			this.EndUpdate();
		}

		protected override void OnMouseUp(System.Windows.Forms.MouseEventArgs e)
		{
			// save these args for the column click and context menu
			lastMouseUpEventArgs = e;

			if(e.Button == MouseButtons.Right)
			{
				this.ContextMenu.Show(this, new Point(e.X, e.Y));
			}

			base.OnMouseUp(e);
		}

		
		private System.Windows.Forms.ContextMenu _ContextMenu = null;
		public override System.Windows.Forms.ContextMenu ContextMenu
		{
			get
			{	
				if(_ContextMenu == null)
					_ContextMenu = new ContextMenu();

				// clear our menu items so they don't get added twice
				_ContextMenu.MenuItems.Clear();
				
				// find the item we clicked on
				ListViewItem listItem = null;
				if(lastMouseUpEventArgs != null)
					listItem = GetItemAt(lastMouseUpEventArgs.X, lastMouseUpEventArgs.Y);
				
				// if there was an item for that location
				if(listItem != null)
				{
					IUIObject ContextObj = ObjectForItem(listItem);
                    if (ContextObj != null)
                    {
                        using (ContextMenu ObjectContextMenu = ContextObj.ContextMenu)
                        {
                            if (ObjectContextMenu != null)
                                _ContextMenu.MergeMenu(ObjectContextMenu);
                        } 
                    }
				}
				
				// if someone is going to handle the click for New add that
				if(OnContextMenuNewClick != null)
					_ContextMenu.MenuItems.Add("New", this.OnContextMenuNewClick);

				// if someone is handling remove add the menu item
				if(OnContextMenuRemoveClick != null)
					_ContextMenu.MenuItems.Add("Remove", this.OnContextMenuRemoveClick);

				// add the original menu items back to the menu
				foreach(MenuItem item in menuItemsFromHost)
					_ContextMenu.MenuItems.Add(item);

				// add our menu to show/hide columns
                using (ContextMenu ColumnMenu = this.ColumnMenu)
                {
                    if (null != ColumnMenu)
                    {
                        if (ColumnMenu.MenuItems.Count > 1)
                        {
                            MenuItem ColumnMenuItem = new MenuItem("Columns");
                            ColumnMenuItem.MergeMenu(ColumnMenu);
                            _ContextMenu.MenuItems.Add(ColumnMenuItem);
                        }
                    }
                }

				return _ContextMenu;
			}
			set
			{
                if (value == null)
                {
                    _ContextMenu = new ContextMenu();
                }
                else
                {
                    menuItemsFromHost = new MenuItem[value.MenuItems.Count];
                    value.MenuItems.CopyTo(menuItemsFromHost, 0);
                    _ContextMenu = value;
                }
			}
		}

		private ContextMenu ColumnMenu
		{
			get	
			{
				if(_ColumnSettingsHashtable == null)
					return null;

				// a list to sort our columns
				List<string> list = new List<string>(this.Columns.Count);

				// update the visibility settings and add the columns to the list
				foreach(ColumnHeader column in this.Columns)
				{
					list.Add(column.Text);

					// setting for this column
					ColumnVisibilitySetting setting = (ColumnVisibilitySetting)_ColumnSettingsHashtable[column.Text];

					// updating the settings if they changed the visibility of a column by means other
					// then the context menu
					setting.isVisibile = (column.Width > 0);
					if(column.Width > 0)
						setting.width = column.Width;

					_ColumnSettingsHashtable[column.Text] = setting;
				}
				
				list.Sort();

				ContextMenu menu = new ContextMenu();

				// create the menu items
				foreach(object obj in list)
				{
					string str = obj as string;
					MenuItem newMenuItem = new MenuItem(str, OnContextColumnMenuHandler);

					// get the setting so we know if the column is visibile
					ColumnVisibilitySetting setting = (ColumnVisibilitySetting)_ColumnSettingsHashtable[str];

					// checked?
					newMenuItem.Checked = setting.isVisibile;

					menu.MenuItems.Add(newMenuItem);
				}

				return menu;
			}
		}

		protected override void OnDoubleClick(System.EventArgs e)
		{
            if (ShowPropertiesOnDoubleClick)
            {
                Point ClientPoint = this.PointToClient(Control.MousePosition);

                ListViewItem Item = this.GetItemAt(ClientPoint.X, ClientPoint.Y);
                IUIObject Obj = ObjectForItem(Item);
                Obj.ShowProperties();
            }

			base.OnDoubleClick(e);
		}

        private Viking.Common.IUIObject ObjectForItem(ListViewItem Item)
		{
			if(Item == null)
				return null;

            return Item.Tag as IUIObject;
		}

		protected override void OnColumnClick( System.Windows.Forms.ColumnClickEventArgs e)
		{
            ListViewColumnSorter Sorter = this.ListViewItemSorter as ListViewColumnSorter;
            Type ColumnType = null;
            if (e.Column > 0)
                ColumnType = this.ColumnProperties[e.Column - 1]?.PropertyType;

            if (Sorter == null)
			{
                
				Sorter = new ListViewColumnSorter(e.Column, ColumnType);
				this.ListViewItemSorter = Sorter as System.Collections.IComparer; 
			}
			else
			{
				if(Sorter.SortIndex == e.Column)
					Sorter.AscendingSort = !Sorter.AscendingSort;

				Sorter.SortIndex = e.Column;
                Sorter.ColumnType = ColumnType;

                this.Sort();
			}            

			base.OnColumnClick(e);
		}

		protected void OnObjectSave(object sender, System.EventArgs e)
		{
			IUIObject Obj = sender as IUIObject; 
			Debug.Assert(Obj != null);

			if(this.ItemForObject(Obj) == null)
				return; 

			this.BeginUpdate();

			this.RemoveObject(Obj);
			this.AddObject(Obj); 

			this.EndUpdate(); 
			
			this.Sort(); 
		}

        protected void OnObjectValueChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
            IUIObject Obj = sender as IUIObject; 
			Debug.Assert(Obj != null);

			if(this.ItemForObject(Obj) == null)
				return; 

			this.BeginUpdate();

			this.RemoveObject(Obj);
			this.AddObject(Obj); 

			this.EndUpdate(); 
			
			this.Sort(); 
		}

		protected void OnObjectDelete(object sender, System.EventArgs e)
		{
            IUIObject Obj = sender as IUIObject; 
			Debug.Assert(Obj != null);

			this.RemoveObject(Obj);
		}

		protected override void OnItemDrag(ItemDragEventArgs e)
		{
			IUIObject DragObject = this.ObjectForItem(e.Item as ListViewItem); 

			UI.State.DragDropObject = DragObject; 
			DoDragDrop(DragObject, DragDropEffects.All); 
			UI.State.DragDropObject = null; 
		}


		protected void OnContextMenuColumnVisibility(object sender, EventArgs e)
		{
			MenuItem item = sender as MenuItem;

			Debug.Assert(item != null);
			Debug.Assert(_ColumnSettingsHashtable.ContainsKey(item.Text));

			ColumnVisibilitySetting visSetting = 
				(ColumnVisibilitySetting)_ColumnSettingsHashtable[item.Text];

			// toggle the visibility of the column
			visSetting.isVisibile = ! visSetting.isVisibile;

			// save our changes
			_ColumnSettingsHashtable[item.Text] = visSetting;

			// find the column and update it's width
			foreach(ColumnHeader column in this.Columns)
			{
				// is this our column?
				if(column.Text == item.Text)
				{
					if(visSetting.isVisibile)
						column.Width = visSetting.width;
					else
						column.Width = 0;

					// end the loop
					break;
				}
			}
		}

		protected void LoadColumnVisibilitySettings()
		{
			try
			{
				// get the unique key for this control
//				string key = this.ControlKey;

				// load the settings from the user preferences
				_ColumnSettingsHashtable = new Dictionary<string,ColumnVisibilitySetting>();

                /*
				if(Global.CurrentUser != null)
				{
					if(Global.CurrentUser.Preferences.Contains(key))
						_ColumnSettingsHashtable = Global.CurrentUser.Preferences[key] as Dictionary<string,ColumnVisibilitySetting>();	
				}
                */

				// update the columns 
				foreach(ColumnHeader header in this.Columns)
				{
					if(_ColumnSettingsHashtable.ContainsKey(header.Text))
					{
						ColumnVisibilitySetting setting = (ColumnVisibilitySetting)_ColumnSettingsHashtable[header.Text];
						if(setting.isVisibile)
							header.Width = setting.width;
						else
							header.Width = 0;
					}
					else
						_ColumnSettingsHashtable.Add(header.Text, new ColumnVisibilitySetting(true, _ColumnDefaultWidth));
				}
			}
			catch(Exception e)
			{
                Trace.WriteLine("Exception: DataObjectListView.LoadColumnVisibilitySettings", "UI");
                Trace.WriteLine(e.StackTrace, "UI");
                Trace.WriteLine(e.Message, "UI");
			}
		}
		protected void SaveColumnVisibilitySettings()
		{
			// step through the columns and save the width and visibility
			foreach(ColumnHeader column in this.Columns)
			{
				ColumnVisibilitySetting visSetting = 
					(ColumnVisibilitySetting)_ColumnSettingsHashtable[column.Text];

				visSetting.isVisibile = (column.Width != 0);
				if(column.Width > 0)
					visSetting.width = column.Width;
				else
					visSetting.width = _ColumnDefaultWidth;

				_ColumnSettingsHashtable[column.Text] = visSetting;
			}

            //TODO: Add support for saving user preferences
/*			Hashtable pref = Global.CurrentUser.Preferences;
			pref[this.ControlKey] = _ColumnSettingsHashtable;
			Global.CurrentUser.Preferences = pref;
			Global.CurrentUser.Save();
 */
		}
		protected string ControlKey
		{
			get
			{
				// building a key by appending all the control names together
				System.Text.StringBuilder key = new System.Text.StringBuilder();

				// columns depend on display type
				if(_DisplayType != null)
					key.Append(_DisplayType.ToString());

				Control currentControl = this;
				while(currentControl != null)
				{
                    Trace.WriteLine("DataObjectListView.ControlKey: currentControl.name=" + currentControl.Name, "UI");
					key.Append(currentControl.Name);
					currentControl = currentControl.Parent;
				}

				return key.ToString();
			}
		}
		#region Public Methods

		/// <summary>
		/// Returns true if the ListView contains the provided object. Otherwise false.
		/// </summary>
		/// <param name="Obj"></param>
		/// <returns></returns>
		public bool ContainsObj(object Obj)
		{
			return ItemForObject(Obj) != null;
		}

        
		public void ExportToExcel()
		{
            throw new NotImplementedException("Coming soon"); 
			//PlantMap.Utils.OfficeDocExporter.ToExcel(this);
		}
        

		#endregion

		protected void parentForm_Closing(object sender, CancelEventArgs e)
		{
			SaveColumnVisibilitySettings();
            
            //Remove all objects from our list so we aren't listening to events anymore
            RemoveAllObjects();
        }

	
		private CancelEventHandler OnParentFormClosing = null;
		private Form _ParentForm = null;

		protected override void OnParentBindingContextChanged(EventArgs e)
		{
			// do we have a parent?
			if(this.Parent == null)
				return;

			// walk up parents looking for a form
			Control currentControl = this;
			while(currentControl != null && (currentControl as Form) == null)
				currentControl = currentControl.Parent;

			// did we find a form?
			if(currentControl != null)
			{
				// is our parent different?
				if(_ParentForm != currentControl)
				{
					// do we have an old binding to remove
					if(_ParentForm != null)
						_ParentForm.Closing -= OnParentFormClosing;

					// add our new binding 
					(currentControl as Form).Closing += OnParentFormClosing;
				}
			}
			else
			{
                Trace.WriteLine("DataObjectListView.OnParentBindingContextChanged: couldn't find parent form to bind to", "UI");
			}
			
			base.OnParentBindingContextChanged (e);
		}
	}
}
