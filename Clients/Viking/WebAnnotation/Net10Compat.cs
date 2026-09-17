#if !NETFRAMEWORK
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;

namespace Viking.Common
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class VikingExtensionAttribute(string name) : Attribute, IComparable
    {
        public string Name { get; } = name;

        public int CompareTo(object obj)
        {
            if (obj is VikingExtensionAttribute other)
                return string.Compare(Name, other.Name, StringComparison.Ordinal);
            return 1;
        }
    }

    public interface IExtensionLoadContext
    {
        System.Xml.Linq.XDocument VikingXML { get; }

        System.Xml.Linq.XElement VolumeElement { get; }

        string VolumeName { get; }
    }

    public interface IHelpStrings
    {
        string[] HelpStrings { get; }
    }

    public interface IContextMenu
    {
    }

    public interface IUIObjectBasic
    {
        void ShowProperties();

        string ToolTip { get; }

        void Save();
    }

    public interface IUIObject : IUIObjectBasic
    {
        event PropertyChangedEventHandler ValueChanged;
    }
}

namespace Viking.Common.UI
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
    public sealed class ColumnAttribute : Attribute
    {
        public ColumnAttribute()
        {
        }

        public ColumnAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ThisToManyRelationAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TreeViewVisibleAttribute : Attribute
    {
    }
}

namespace Viking.Objects
{
    public abstract class UIObjBase : Viking.Common.IUIObject
    {
        public abstract void Delete();

        public virtual void ShowProperties()
        {
        }

        public virtual string ToolTip => string.Empty;

        public virtual void Save()
        {
        }

        public virtual Type[] AssignableParentTypes => Type.EmptyTypes;

        public virtual int TreeImageIndex => 0;

        public virtual int TreeSelectedImageIndex => 0;

        public virtual System.Drawing.Image SmallThumbnail => null;

        public virtual void SetParent(Viking.Common.IUIObject parent)
        {
        }

        public virtual event NotifyCollectionChangedEventHandler ChildChanged;

        public event PropertyChangedEventHandler ValueChanged;

        protected void ValueChangedEvent(string column)
        {
            PropertyChangedEventHandler handler = ValueChanged;
            if (handler != null)
                Dispatcher.CurrentDispatcher.BeginInvoke(handler, this, new PropertyChangedEventArgs(column));
        }

        protected void CallBeforeDelete()
        {
        }

        protected void CallAfterDelete()
        {
        }
    }
}
#endif
