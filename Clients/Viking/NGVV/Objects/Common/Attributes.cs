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
    public sealed class MenuAttribute(string ParentMenu) : System.Attribute
    {
        public string ParentMenuName = ParentMenu;
    }

    /// <summary>
    /// Specifies this method should create a menu item with the specified name
    /// and this method should be the callback for the menu item.  Must be in a
    /// class with the MenuAttribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItemAttribute(string Label) : System.Attribute
    {
        public string LabelName = Label;
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
        public System.Type? ObjectType;

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
            this.Types = [T];
        }

        public SupportedUITypesAttribute(System.Type[] types)
        {
            this.Types = types;
            this.Types ??= [];
        }
    }


    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SectionOverlayAttribute(string Name) : System.Attribute
    {
        public string Name = Name;

        public override int GetHashCode() => Name.GetHashCode();

        public override bool Equals(object obj) => Name.Equals(obj);
    }

    /// <summary>
    /// Extensions with this attribute located in the modules directory will be loaded as extensions to the UI
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class VikingExtensionAttribute(string Name) : System.Attribute, IComparable
    {
        /// <summary>
        /// Name of the extension
        /// </summary>
        public string Name = Name;

        public override string ToString() => Name;

        public override int GetHashCode() => Name.GetHashCode();

        public override bool Equals(object obj) => Name.Equals(obj);

        #region IComparable Members

        public int CompareTo(object? obj)
        {
            if (obj is not VikingExtensionAttribute attrib)
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
    public class ObjectSelectedEventArgs(IUIObjectBasic Selected) : System.EventArgs
    {
        public IUIObjectBasic Object = Selected;
    }
    public delegate void ObjectSelectedEventHandler(object sender, Viking.Common.ObjectSelectedEventArgs e);
}
