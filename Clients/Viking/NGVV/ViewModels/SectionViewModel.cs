using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Viking.Common;
using Viking.Common.UI;
using Viking.VolumeModel;

namespace Viking.ViewModels
{
    /// <summary>
    /// Encapsulates a section within the UI
    /// </summary>
    public class SectionViewModel : IUIObject, INotifyPropertyChanged
    {
        public readonly Section section;

        /// <summary>
        /// Fires when the transform used to render the section changes
        /// </summary>
        public event Viking.Common.TransformChangedEventHandler TransformChanged;

        public event PropertyChangedEventHandler PropertyChanged;


        [Column("Name")]
        public string Name { get { return section.Name; } }

        [Column("Number")]
        public int Number { get { return section.Number; } }

        [Column("Notes")]
        public string Notes { get { return section.Notes; } }

        public string Path { get { return section.Path; } }

        public string SubPath { get { return section.SectionSubPath; } }

        public override string ToString()
        {
            return section.ToString();
        }

        public string DefaultChannel { get { return section.DefaultChannel; } }
        public IList<string> Channels { get { return section.Channels; } }

        public string DefaultPyramidTransform { get { return section.DefaultPyramidTransform; } }
        public string DefaultPyramid { get { return section.DefaultPyramid; } }
        public List<string> TilesetNames { get { return section.TilesetNames; } }
        public List<string> PyramidTransformNames { get { return section.PyramidTransformNames; } }
        public SortedList<string, Pyramid> ImagePyramids { get { return section.ImagePyramids; } }

        /// <summary>
        /// The currently displayed channels
        /// </summary>
        public ChannelInfo[] ChannelInfoArray
        {
            get
            {
                if (section.ChannelInfoArray == null || section.ChannelInfoArray.Length == 0)
                {
                    ChannelInfo channel = new ChannelInfo();
                    channel.ChannelName = this.ActiveChannel;
                    channel.SectionSource = ChannelInfo.SectionInfo.FIXED;
                    channel.FixedSectionNumber = this.section.Number;
                    return new ChannelInfo[] { channel };
                }
                return section.ChannelInfoArray;
            }
            set { section.ChannelInfoArray = value; }
        }

        /// <summary>
        /// The names of all channels supported by this section
        /// </summary>
        public List<string> ChannelNames { get { return section.ChannelNames; } }

        #region Reference Sections

        public bool ReferenceSectionsWereSet = false;

        /// <summary>
        /// Fires when one of the reference sections has been changed
        /// </summary>
        public event ReferenceSectionChangedEventHandler OnReferenceSectionChanged;

        /// <summary>
        /// Pointer to a section above this one, user configurable to point to a properly registered section suitable as a reference
        /// </summary>
        private Section _ReferenceSectionAbove;

        public Section ReferenceSectionAbove
        {
            get
            {
                //We want the user to be able to set the reference section to null, so only update the reference sections if we haven't initialized them
                if (false == ReferenceSectionsWereSet)
                {
                    this._ReferenceSectionAbove = section.volume.GetReferenceSectionAbove(section);
                    this._ReferenceSectionBelow = section.volume.GetReferenceSectionBelow(section);
                    ReferenceSectionsWereSet = true;
                }

                return _ReferenceSectionAbove;
            }

            set
            {
                bool SendEvent = false;
                Debug.Assert(section != value);
                if (section == value)
                    return;

                Section _oldReference = this._ReferenceSectionAbove;

                //See if the new section is really above us
                if (value != null)
                {
                    Debug.Assert(section.Number < value.Number);
                }

                if (this._ReferenceSectionAbove != value)
                    SendEvent = true;

                this._ReferenceSectionAbove = value;
                ReferenceSectionsWereSet = true;

                if (SendEvent && OnReferenceSectionChanged != null)
                {
                    OnReferenceSectionChanged(this, new ReferenceSectionChangedEventArgs(this,
                                                                                         _oldReference == null ? new long?() : _oldReference.Number,
                                                                                         value == null ? new long?() : value.Number));
                }
            }
        }

        /// <summary>
        /// Pointer to a section below this one, user configurable to point to a properly registered section suitable as a reference
        /// </summary>
        private Section _ReferenceSectionBelow = null;

        public Section ReferenceSectionBelow
        {
            get
            {
                //We want the user to be able to set the reference section to null, so only update the reference sections if we haven't initialized them
                if (false == ReferenceSectionsWereSet)
                {
                    this._ReferenceSectionAbove = section.volume.GetReferenceSectionAbove(section);
                    this._ReferenceSectionBelow = section.volume.GetReferenceSectionBelow(section);
                    ReferenceSectionsWereSet = true;
                }

                return _ReferenceSectionBelow;
            }

            set
            {
                bool SendEvent = false;
                Debug.Assert(section != value);
                if (section == value)
                    return;

                Section _oldReference = this._ReferenceSectionBelow;

                //See if the new section is really below us
                if (value != null)
                {
                    Debug.Assert(section.Number > value.Number);
                }

                if (this._ReferenceSectionBelow != value)
                    SendEvent = true;

                this._ReferenceSectionBelow = value;
                ReferenceSectionsWereSet = true;

                if (SendEvent && OnReferenceSectionChanged != null)
                {
                    OnReferenceSectionChanged(this, new ReferenceSectionChangedEventArgs(this,
                                                                                         _oldReference == null ? new long?() : _oldReference.Number,
                                                                                         value == null ? new long?() : value.Number));
                }
            }
        }

        #endregion

        private VolumeViewModel _VolumeViewModel;
        public VolumeViewModel VolumeViewModel { get { return _VolumeViewModel; } }

        public SectionViewModel(VolumeViewModel Volume, Section section)
        {
            this._VolumeViewModel = Volume;
            this.section = section;
        }

        #region IUIObject Members

        void IUIObjectBasic.ShowProperties()
        {
            Viking.UI.Forms.PropertySheetForm.Show(this);
        }

        public System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                System.Windows.Forms.ContextMenu menu = new System.Windows.Forms.ContextMenu();

                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);

                return menu;
            }
        }

        string IUIObjectBasic.ToolTip
        {
            get { return this.ToString(); }
        }

        void IUIObjectBasic.Save()
        {
            //There is no backing store for the section
            return;
        }


        private event System.ComponentModel.PropertyChangedEventHandler OnValueChanged;
        internal event EventHandler OnBeforeDelete;
        internal event EventHandler OnAfterDelete;
        internal event EventHandler OnBeforeSave;
        internal event EventHandler OnAfterSave;
        private event System.Collections.Specialized.NotifyCollectionChangedEventHandler OnChildChanged;

        event System.ComponentModel.PropertyChangedEventHandler IUIObject.ValueChanged
        {
            add { OnValueChanged += value; }
            remove { OnValueChanged -= value; }
        }

        event EventHandler IUIObject.BeforeDelete
        {
            add { OnBeforeDelete += value; }
            remove { OnBeforeDelete -= value; }
        }

        event EventHandler IUIObject.AfterDelete
        {
            add { OnAfterDelete += value; }
            remove { OnAfterDelete -= value; }
        }

        event EventHandler IUIObject.BeforeSave
        {
            add { OnBeforeSave += value; }
            remove { OnBeforeSave -= value; }
        }

        event EventHandler IUIObject.AfterSave
        {
            add { OnAfterSave += value; }
            remove { OnAfterSave -= value; }
        }

        event System.Collections.Specialized.NotifyCollectionChangedEventHandler IUIObject.ChildChanged
        {
            add { OnChildChanged += value; }
            remove { OnChildChanged -= value; }
        }

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        System.Drawing.Image IUIObject.SmallThumbnail
        {
            get { throw new NotImplementedException(); }
        }

        Type[] IUIObject.AssignableParentTypes
        {
            get { return new Type[0]; }
        }

        void IUIObject.SetParent(IUIObject parent)
        {
            throw new NotImplementedException();
        }

        Viking.UI.Controls.GenericTreeNode IUIObject.CreateNode()
        {
            throw new NotImplementedException();
        }

        int IUIObject.TreeImageIndex
        {
            get { throw new NotImplementedException(); }
        }

        int IUIObject.TreeSelectedImageIndex
        {
            get { throw new NotImplementedException(); }
        }



        #endregion


        protected void ContextMenu_OnProperties(object sender, EventArgs e)
        {
            Viking.UI.Forms.PropertySheetForm.Show(this);
        }

        public async System.Threading.Tasks.Task PrepareTransform(string transform)
        {
            await this.section.PrepareTransform(transform);
        }

        public IVolumeToSectionTransform ActiveSectionToVolumeTransform
        {
            get
            {
                return this._VolumeViewModel.GetSectionToVolumeTransform(this.section.Number);
            }
        }


        /// <summary>
        /// Determines which transform should be used when rendering the section
        /// </summary>
        protected string _ActiveTileTransform;
        public string ActiveTileTransform
        {
            get { return _ActiveTileTransform; }
            set
            {
                bool NewValue = _ActiveTileTransform != value;
                if (NewValue)
                {
                    string OldTransform = _ActiveTileTransform;
                    _ActiveTileTransform = value;
                    if (TransformChanged != null && NewValue)
                    {
                        TransformChanged(this, new TransformChangedEventArgs(_ActiveTileTransform, OldTransform));
                    }
                }
            }
        }

        /// <summary>
        /// Currently selected tileset 
        /// </summary>
        protected string _ActiveChannel;
        public string ActiveChannel
        {
            get { return _ActiveChannel; }
            set
            {
                bool NewValue = value != _ActiveChannel;
                if (NewValue)
                {
                    _ActiveChannel = value;
                    RaisePropertyChanged();
                }
            }
        }

        private void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public Section GetSectionToDrawForChannel(ChannelInfo channel)
        {
            Section sectionToDraw = null;

            switch (channel.SectionSource)
            {
                case ChannelInfo.SectionInfo.SELECTED:
                    sectionToDraw = this.section;
                    break;
                case ChannelInfo.SectionInfo.ABOVE:
                    sectionToDraw = this.ReferenceSectionAbove;
                    break;
                case ChannelInfo.SectionInfo.BELOW:
                    sectionToDraw = this.ReferenceSectionBelow;
                    break;
                case ChannelInfo.SectionInfo.FIXED:
                    int SectionNumber = channel.FixedSectionNumber.Value;
                    if (false == UI.State.volume.SectionViewModels.ContainsKey(SectionNumber))
                        sectionToDraw = null;
                    else
                        sectionToDraw = UI.State.volume.SectionViewModels[SectionNumber].section;

                    break;
            }

            return sectionToDraw;
        }

    }
}
