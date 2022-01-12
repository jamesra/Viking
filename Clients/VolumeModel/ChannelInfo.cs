using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Linq;
using Utils;

namespace Viking.VolumeModel
{

    /// <summary>
    /// This class informs the UI where each channel should be loaded from
    /// </summary>
    [Serializable()]
    public class ChannelInfo : ICloneable
    {
        public enum SectionInfo
        {
            SELECTED = 0, //The visible section
            ABOVE = 1, //The reference section above
            BELOW = 2, //The reference section below
            FIXED = 3  //A fixed section number
        };

        //For compatability with older version of VikingXML, the first channel with a grey color is considered the background for multi-channel blending
        public bool Greyscale
        {
            get
            {
                return (Color.B == Color.G) && (Color.B == Color.R);
            }
        }

        public override string ToString()
        {
            return "R: " + this.Color.R.ToString() + " G: " + this.Color.G.ToString() + " B: " + this.Color.B.ToString();
        }

        /// <summary>
        /// Which section we should load the channel from
        /// </summary>
        public SectionInfo SectionSource = SectionInfo.SELECTED;

        /// <summary>
        /// If SectionSource = FIXED this contains the number of the section
        /// </summary>
        public int? FixedSectionNumber = new int?();

        /// <summary>
        /// Which image we should pull the channel images from.
        /// Empty means the selected channel
        /// </summary>
        public string ChannelName = "";

        /// <summary>
        /// The color to map the images to
        /// </summary>
        public Geometry.Graphics.Color Color = new Geometry.Graphics.Color(1f, 1f, 1f);

        public System.Drawing.Color FormColor
        {
            get
            {
                System.Drawing.Color formColor = System.Drawing.Color.FromArgb(Color.A,
                                                                               Color.R,
                                                                               Color.G,
                                                                               Color.B);
                return formColor;
            }

            set
            {
                Color = new Geometry.Graphics.Color(value.R,
                                                                        value.G,
                                                                        value.B,
                                                                        value.A);
            }
        }

        #region ICloneable Members

        public object Clone()
        {
            ChannelInfo clone = new ChannelInfo();

            clone.ChannelName = this.ChannelName;
            clone.Color = this.Color;
            clone.FixedSectionNumber = this.FixedSectionNumber;
            clone.SectionSource = this.SectionSource;

            return clone;
        }

        #endregion

        /// <summary>
        /// Channel info can be specified in an XML file, this reads that information
        /// </summary>
        /// <param name="elem"></param>
        /// <returns></returns>
        public static ChannelInfo[] FromXML(XElement elemChannelInfo)
        {
            if (elemChannelInfo == null)
                return new ChannelInfo[0];

            List<ChannelInfo> channels = new List<ChannelInfo>();
            foreach (XNode node in elemChannelInfo.Nodes())
            {
                XElement elem = node as XElement;
                if (elem == null)
                    continue;

                switch (elem.Name.LocalName)
                {
                    case "Channel":
                        bool CreateChannel = true;
                        string Section = "Selected";
                        if (elem.GetAttributeCaseInsensitive("Section") != null)
                            Section = elem.GetAttributeCaseInsensitive("Section").Value;

                        int? SectionNumber = new int?();
                        ChannelInfo.SectionInfo refSectionInfo = SectionInfo.FIXED;
                        switch (Section.ToLower())
                        {
                            case "selected":
                                refSectionInfo = SectionInfo.SELECTED;
                                break;
                            case "above":
                                refSectionInfo = SectionInfo.ABOVE;
                                break;
                            case "below":
                                refSectionInfo = SectionInfo.BELOW;
                                break;
                            default:
                                try
                                {
                                    SectionNumber = new int?(System.Convert.ToInt32(Section));
                                    refSectionInfo = SectionInfo.FIXED;
                                }
                                catch (FormatException)
                                {
                                    Trace.WriteLine("Cannot format Section attribute of ChannelInfo: " + elem.ToString(), "VolumeModel");
                                    CreateChannel = false;
                                }

                                break;
                        }


                        string Channel = "";
                        if (elem.GetAttributeCaseInsensitive("Channel") != null)
                        {
                            Channel = elem.GetAttributeCaseInsensitive("Channel").Value;
                            if (Channel == "Selected")
                                Channel = "";
                        }

                        string Color = "#FFFFFF";
                        if (elem.GetAttributeCaseInsensitive("Color") != null)
                            Color = elem.GetAttributeCaseInsensitive("Color").Value;

                        //Convert the color to a valid value
                        Geometry.Graphics.Color ChannelColor;
                        if (!TryParseColor(Color, out ChannelColor))
                        {
                            CreateChannel = false;
                        }

                        if (CreateChannel)
                        {
                            ChannelInfo newChannel = new ChannelInfo();
                            newChannel.ChannelName = Channel;
                            newChannel.Color = ChannelColor;
                            newChannel.SectionSource = refSectionInfo;
                            newChannel.FixedSectionNumber = SectionNumber;
                            channels.Add(newChannel);
                        }
                        break;
                }
            }

            return channels.ToArray();
        }

        public static bool TryParseColor(string Color, out Geometry.Graphics.Color Output)
        {
            System.Drawing.Color FromNameColor = System.Drawing.Color.FromName(Color);
            //If the color name is unknown we have all zeros in the color
            if (FromNameColor.A == 0 &&
                FromNameColor.B == 0 &&
                FromNameColor.G == 0 &&
                FromNameColor.R == 0)
            {
                try
                {
                    Output = Geometry.Graphics.Color.FromInteger(Color);
                }
                catch (FormatException)
                {
                    Trace.WriteLine("Cannot format Color attribute of ChannelInfo: " + Color, "VolumeModel");
                    Output = new Geometry.Graphics.Color();
                    return false;
                }
            }
            else
            {
                Output = new Geometry.Graphics.Color(FromNameColor.R,
                                                    FromNameColor.G,
                                                    FromNameColor.B,
                                                    FromNameColor.A);
            }

            return true;
        }
    }
}
