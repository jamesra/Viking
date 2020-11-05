using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Viking.UI.Forms;
using Viking.VolumeModel;

namespace Viking.UI.Controls
{
    internal partial class ChannelPickerControl : UserControl
    {
        //I have to do this so the colors aren't named which messes up the default equality operator
        private System.Drawing.Color Red = Color.FromArgb(255, 255, 0, 0);
        private System.Drawing.Color Green = Color.FromArgb(255, 0, 255, 0);
        private System.Drawing.Color Blue = Color.FromArgb(255, 0, 0, 255);
        private System.Drawing.Color White = Color.FromArgb(255, 255, 255, 255);

        /// <summary>
        /// This is the name that we reserve to allow the user to specify the selected channel should be used 
        /// instead of a named one
        /// </summary>
        private readonly string DefaultChannelName = "Selected";

        private bool _ShowDelete; //Controls don't update the Visible property immediately so I cache the values
        public bool ShowDelete
        {
            get
            {
                return _ShowDelete;
            }
            set
            {
                _ShowDelete = value;
                buttonDelete.Visible = value;
                panelDeleteSpacer.Visible = value && ShowLabels;
            }
        }

        private bool _ShowLabels; //Controls don't update the Visible property immediately so I cache the values
        public bool ShowLabels
        {
            get
            {
                return _ShowLabels;
            }
            set
            {
                _ShowLabels = value;
                panelLabels.Visible = value;
                panelDeleteSpacer.Visible = ShowDelete;

                //Don't leave room for labels if they are hidden
                this.Size = new Size(this.Size.Width, value ? 34 : 21);
            }
        }

        /// <summary>
        /// I know there is a better way to do this, but I don't want my controls dialogs if the user isn't responsible for the edit
        /// </summary>
        private bool IsUserUpdate = false;

        public string[] Channels
        {
            get
            {
                if (comboChannel == null)
                {
                    return new string[0];
                }

                return comboChannel.Items.Cast<string>().ToArray();
            }
            set
            {
                if (comboChannel == null)
                    return;

                comboChannel.Items.Clear();

                if (value == null)
                {
                    comboChannel.Text = "";
                    return;
                }

                if (value.Length == 0)
                {
                    comboChannel.Text = "";
                    return;
                }

                IsUserUpdate = false;
                comboChannel.Items.Add(DefaultChannelName); //Add the default option
                comboChannel.Items.AddRange(value);


                if (false == Channels.Contains<string>(comboChannel.Text))
                {
                    comboChannel.Text = value[0];
                }

                comboChannel.SelectedText = "";
                IsUserUpdate = true;
            }
        }

        internal ChannelInfo _Info = new ChannelInfo();

        /// <summary>
        /// Channel info to be displayed.
        /// </summary>
        internal ChannelInfo Info
        {
            get { return _Info; }
            set
            {
                if (value == null)
                    throw new ArgumentException("Expecting non-null ChannelInfo in ChannelPickerControl.Info");

                _Info = (ChannelInfo)value.Clone();

                //Setup the section UI
                IsUserUpdate = false;
                if (_Info.SectionSource == ChannelInfo.SectionInfo.FIXED)
                {
                    if (_Info.FixedSectionNumber.HasValue)
                    {
                        comboSection.Items.Add(_Info.FixedSectionNumber.Value.ToString("D4"));
                        comboSection.Text = _Info.FixedSectionNumber.Value.ToString("D4");
                    }
                    else
                    {
                        comboSection.Items.Add("0001");
                        comboSection.Text = "0001";
                    }
                }
                else
                {
                    if ((int)_Info.SectionSource >= 0 && (int)_Info.SectionSource < SectionInfoList.Count)
                        comboSection.Text = SectionInfoList[(int)_Info.SectionSource];
                    else
                        comboSection.Text = "";
                }

                if (String.IsNullOrEmpty(Info.ChannelName))
                    comboChannel.Text = DefaultChannelName;
                else
                    comboChannel.Text = Info.ChannelName;

                panelColor.BackColor = Info.FormColor;

                if (Info.Color.R == 255 &&
                    Info.Color.G == 0 &&
                    Info.Color.B == 0)
                    comboColor.Text = "Red";
                else if (Info.Color.R == 0 &&
                    Info.Color.G == 255 &&
                    Info.Color.B == 0)
                    comboColor.Text = "Green";
                else if (Info.Color.R == 0 &&
                    Info.Color.G == 0 &&
                    Info.Color.B == 255)
                    comboColor.Text = "Blue";
                else if (Info.Color.R == 255 &&
                    Info.Color.G == 255 &&
                    Info.Color.B == 255)
                    comboColor.Text = "White";
                else
                    comboColor.Text = "Custom...";

                IsUserUpdate = true;
            }
        }

        /// <summary>
        /// The indicies of the values in this list should map to the enum values
        /// </summary>
        private List<string> SectionInfoList;

        internal ChannelPickerControl()
        {
            SectionInfoList = new List<string>();
            SectionInfoList.Add("Selected");
            SectionInfoList.Add("Above");
            SectionInfoList.Add("Below");
            SectionInfoList.Add("Choose...");

            InitializeComponent();

            comboSection.Items.AddRange(SectionInfoList.ToArray());

        }

        internal ChannelPickerControl(ChannelInfo info) : this()
        {
            this.Info = info;
        }

        private void comboColor_SelectedValueChanged(object sender, EventArgs e)
        {
            if (!IsUserUpdate)
                return;

            switch (comboColor.Text)
            {
                case "Custom...":
                    //Don't launch color picker until we've been loaded.
                    if (IsUserUpdate)
                    {
                        colorDialog = new ColorDialog();
                        colorDialog.Color = panelColor.BackColor;
                        if (colorDialog.ShowDialog() == DialogResult.OK)
                        {
                            Info.FormColor = colorDialog.Color;
                            panelColor.BackColor = colorDialog.Color;
                        }
                    }
                    break;
                case "White":
                    Info.FormColor = White;
                    panelColor.BackColor = White;
                    break;
                case "Red":
                    Info.FormColor = Red;
                    panelColor.BackColor = Red;
                    break;
                case "Green":
                    Info.FormColor = Green;
                    panelColor.BackColor = Green;
                    break;
                case "Blue":
                    Info.FormColor = Blue;
                    panelColor.BackColor = Blue;
                    break;
            }
        }

        private void comboChannel_SelectedValueChanged(object sender, EventArgs e)
        {
            if (comboChannel.Text == DefaultChannelName)
                this.Info.ChannelName = "";
            else
                this.Info.ChannelName = comboChannel.Text;
        }

        private void comboSection_SelectedValueChanged(object sender, EventArgs e)
        {
            int index = SectionInfoList.IndexOf(comboSection.Text);
            if (index < 0)
            {

                //We may have added section numbers to the list, see if it is a section number before checking other things
                try
                {
                    int sectionNumber = System.Convert.ToInt32(comboSection.Text);
                    this.Info.SectionSource = ChannelInfo.SectionInfo.FIXED;
                    this.Info.FixedSectionNumber = new int?(sectionNumber);
                    return;
                }
                catch (FormatException)
                {
                    //Just ignore a format exception and keep going
                }

                return;
            }
            else
            {
                ChannelInfo.SectionInfo value = (ChannelInfo.SectionInfo)index;
                this.Info.SectionSource = value;

                if (value == ChannelInfo.SectionInfo.FIXED)
                {
                    if (IsUserUpdate)
                    {
                        //TODO: Let the user choose a section
                        using (SectionChooserForm sectionChooser = new SectionChooserForm())
                        {
                            if (sectionChooser.ShowDialog() == DialogResult.OK)
                            {
                                this.Info.FixedSectionNumber = new int?(sectionChooser.SelectedSection.Number);
                                comboSection.Items.Add(sectionChooser.SelectedSection.ToString());
                                comboSection.Text = sectionChooser.SelectedSection.ToString();
                            }
                        }
                    }
                }
            }
        }

        private void ChannelPickerControl_Load(object sender, EventArgs e)
        {
            IsUserUpdate = true;
        }

        public event EventHandler OnDeleteClicked;

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            if (OnDeleteClicked != null)
                OnDeleteClicked(this, e);
        }

    }
}
