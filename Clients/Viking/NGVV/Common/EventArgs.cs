using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.VolumeModel;
using Viking.ViewModels; 

namespace Viking.Common
{

    public delegate void RefreshDelegate();

    /// <summary>
    /// Fires when a command completes an action
    /// </summary>
    public class CommandCompletedEventArgs : System.EventArgs
    {
        public bool Refresh;

        public CommandCompletedEventArgs(bool Refresh)
        {
            this.Refresh = Refresh;
        }
    }
    public delegate void CommandCompleteEventHandler(object sender, System.EventArgs e);

    public class SectionChangedEventArgs : System.EventArgs
    {
        public SectionViewModel NewSection;
        public SectionViewModel OldSection;

        public SectionChangedEventArgs(SectionViewModel newSection, SectionViewModel oldSection)
        {
            this.NewSection = newSection;
            this.OldSection = oldSection; 
        }
    }
    public delegate void SectionChangedEventHandler(object sender, SectionChangedEventArgs e);

    public class TransformChangedEventArgs : System.EventArgs
    {
        public string NewTransform;
        public string OldTransform;

        public TransformChangedEventArgs(string newSectionTransform, string oldSectionTransform)
        {
            this.NewTransform = newSectionTransform;
            this.OldTransform = oldSectionTransform;
        }

    }
    public delegate void TransformChangedEventHandler(object sender, TransformChangedEventArgs e);

    
    /// <summary>
    /// Used for progress bars
    /// </summary>
    public class LoadProgressEventArgs : EventArgs
    {
        public string Info;
        public int MaxProgress;
        public int Progress;

        public LoadProgressEventArgs(string Info, int Progress, int MaxProgress)
        {
            this.Info = Info; 
            this.Progress = Progress; 
            this.MaxProgress = MaxProgress; 
        }
    }
    
    public delegate void LoadProgressEventHandler(object sender, LoadProgressEventArgs e);

    /// <summary>
    /// Fired when an object has it's value changed
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void ValueChangedEventHandler(object sender, Viking.Common.ValueChangedEventArgs e);
    public class ValueChangedEventArgs : System.EventArgs
    {
        /// <summary>
        /// Name of column that changed
        /// </summary>
        public string Column;

        public ValueChangedEventArgs(string column)
        {
            this.Column = column;
        }
    }

    /// <summary>
    /// Fired when the user selects a control from the view menu to hide/show it
    /// </summary>
    public class ViewChangeEventArgs
    {
        public string Text;
        public string Catagory;
        public string TypeString;
        public bool Visible;

        public ViewChangeEventArgs(string Text, string TypeString, bool Visible)
        {
            this.Text = Text;
            this.TypeString = TypeString;
            this.Visible = Visible;
        }
    }

    /// <summary>
    /// Fired when the user selects a control from the view menu to hide/show it
    /// </summary>
    public delegate void ViewChangeEventHandler(object sender, Viking.Common.ViewChangeEventArgs e);
}
