using System;
using Viking.Common;

namespace Viking.UI.BaseClasses
{
    /// <summary>
    /// A menu item with the tag set to a IUIObject
    /// </summary>
    public class TagMenuItem : System.Windows.Forms.MenuItem
    {
        public IUIObject TagObject = null;

        public TagMenuItem()
            : base()
        {
        }

        public TagMenuItem(string Text)
            : base(Text)
        {
        }

        public TagMenuItem(string Text, IUIObject Tag)
            : base(Text)
        {
            this.TagObject = Tag;
        }

        public TagMenuItem(string Text, IUIObject Tag, EventHandler onClick)
            : this(Text, Tag)
        {
            this.Click += onClick;
        }
    }

}
