using System;
using System.Threading;
using System.Threading.Tasks;
using Viking.ViewModels;
using System.Collections;
using System.ComponentModel;

namespace Viking.Common
{

    public delegate void RefreshDelegate();

    /// <summary>
    /// Fires when a command completes an action
    /// </summary>
    public class CommandCompletedEventArgs(bool Refresh) : System.EventArgs
    {
        public bool Refresh = Refresh;
    }
    public delegate void CommandCompleteEventHandler(object sender, System.EventArgs e);

    public class TransformChangedEventArgs(string newSectionTransform, string oldSectionTransform) : System.EventArgs
    {
        public string NewTransform = newSectionTransform;
        public string OldTransform = oldSectionTransform;
    }
    public delegate void TransformChangedEventHandler(object sender, TransformChangedEventArgs e);


    /// <summary>
    /// Used for progress bars
    /// </summary>
    public class LoadProgressEventArgs(string Info, int Progress, int MaxProgress) : EventArgs
    {
        public string Info = Info;
        public int MaxProgress = MaxProgress;
        public int Progress = Progress;
    }

    public delegate void LoadProgressEventHandler(object sender, LoadProgressEventArgs e);

    /// <summary>
    /// Fired when an object has it's value changed
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void ValueChangedEventHandler(object sender, ValueChangedEventArgs e);

    public class ValueChangedEventArgs(string column) : System.EventArgs
    {
        /// <summary>
        /// Name of column that changed
        /// </summary>
        public string Column = column;
    }

    /// <summary>
    /// Fired when the user selects a control from the view menu to hide/show it
    /// </summary>
    public class ViewChangeEventArgs(string Text, string TypeString, bool Visible)
    {
        public string Text = Text;
        public string Catagory = string.Empty;
        public string TypeString = TypeString;
        public bool Visible = Visible;
    }

    /// <summary>
    /// Fired when the user selects a control from the view menu to hide/show it
    /// </summary>
    public delegate void ViewChangeEventHandler(object sender, Viking.Common.ViewChangeEventArgs e);
}
