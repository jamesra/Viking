using System;

namespace Viking.Common
{
    /*
    
    */

    /// <summary>
    /// Attribute for the object has a number of methods
    /// tagged with the MenuItemAttribute which are used to
    /// extend or create a top-level menu
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MenuAttribute : System.Attribute
    {
        public string ParentMenuName;

        public MenuAttribute(string ParentMenu)
        {
            ParentMenuName = ParentMenu;
        }
    }

    /// <summary>
    /// Specifies this method should create a menu item with the specified name
    /// and this method should be the callback for the menu item.  Must be in a
    /// class with the MenuAttribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItemAttribute : System.Attribute
    {
        public string LabelName;

        public MenuItemAttribute(string Label)
        {
            LabelName = Label;
        }
    }


    /// <summary>
    /// Attribute for specifying that the object is a property page
    /// used to display information for the specified object
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PropertyPageAttribute : System.Attribute
    {
        public System.Type targetType;
        public int priority = 1;

        public PropertyPageAttribute(System.Type ObjType)
        {
            targetType = ObjType;
        }

        public PropertyPageAttribute(System.Type ObjType, int Priority)
            : this(ObjType)
        {
            priority = Priority;
        }
    }

    /// <summary>
    /// Determines which types of objects are valid targets for the command
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CommandAttribute : System.Attribute
    {
        /// <summary>
        /// Object types that the command is active for
        /// </summary>
        public System.Type ObjectType;

        public CommandAttribute(System.Type ObjectType)
        {
            this.ObjectType = ObjectType;
        }

        public CommandAttribute()
        {
            this.ObjectType = null;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ViewableAttribute : System.Attribute
    {
        public string MenuText = "";
        public string MenuCatagory = "";

        public ViewableAttribute() { }

        public ViewableAttribute(string catagory, string menuItemText)
        {
            MenuCatagory = catagory;
            MenuText = menuItemText;
        }
    }

    /// <summary>
    /// Indicates which IUIObject supporting types a control displays natively
    /// Used at the moment to build context menus when no item is selected and
    /// determine when drag drop operations are allowed
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SupportedUITypesAttribute : System.Attribute
    {
        public System.Type[] Types;

        public SupportedUITypesAttribute(System.Type T)
        {
            this.Types = new Type[] { T };
        }

        public SupportedUITypesAttribute(System.Type[] types)
        {
            this.Types = types;
            if (this.Types == null)
                this.Types = new Type[0];
        }
    }


    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SectionOverlayAttribute : System.Attribute
    {
        public string Name = "";

        public SectionOverlayAttribute(string Name)
        {
            this.Name = Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Name.Equals(obj);
        }
    }

    /// <summary>
    /// Extensions with this attribute located in the modules directory will be loaded as extensions to the UI
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class VikingExtensionAttribute : System.Attribute, IComparable
    {
        /// <summary>
        /// Name of the extension
        /// </summary>
        public string Name = "";


        public VikingExtensionAttribute(string Name)
        {
            this.Name = Name;
        }

        public override string ToString()
        {
            return Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Name.Equals(obj);
        }

        #region IComparable Members

        public int CompareTo(object obj)
        {
            VikingExtensionAttribute attrib = obj as VikingExtensionAttribute;
            if (obj == null)
                return -1;

            return Name.CompareTo(attrib.Name);
        }

        #endregion
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ExtensionTabAttribute : ViewableAttribute
    {
        public string Name = "";
        public TABCATEGORY Category;
        public readonly string CategoryName = "";

        public ExtensionTabAttribute(string Name, TABCATEGORY Category)
        {
            this.MenuText = Name;
            this.Name = Name;
            this.Category = Category;

            switch (Category)
            {
                case TABCATEGORY.ACTION:
                    CategoryName = "Actions";
                    break;
                case TABCATEGORY.NAVIGATION:
                    CategoryName = "Navigation";
                    break;
                default:
                    break;
            }
            MenuCatagory = this.CategoryName;
        }

        public ExtensionTabAttribute(string Name, string Category)
        {
            this.Name = Name;
            this.CategoryName = Category;
            this.Category = TABCATEGORY.CUSTOM;
        }
    }

    /// <summary>
    /// Event sent when the user selects an object
    /// Object can be null if the user deselects
    /// </summary>
    public class ObjectSelectedEventArgs : System.EventArgs
    {
        public IUIObjectBasic Object;

        public ObjectSelectedEventArgs(IUIObjectBasic Selected)
        {
            Object = Selected;
        }
    }
    public delegate void ObjectSelectedEventHandler(object sender, Viking.Common.ObjectSelectedEventArgs e);
}
