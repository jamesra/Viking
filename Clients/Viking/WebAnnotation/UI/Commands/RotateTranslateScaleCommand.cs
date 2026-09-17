using Geometry;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    internal abstract class RotateTranslateScaleCommand(Viking.UI.Controls.SectionViewerControl parent, Geometry.Vector2 VolumePosition) : TranslateScaleCommandBase(parent, VolumePosition), Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        public new static string[] DefaultMouseHelpStrings =
        [
            "Hold Right click and drag: Rotate"
        ];

        /// <summary>
        /// This is the center point about which we are rotating.  Usually the center of the shape
        /// </summary>
        protected abstract Vector2 VolumeRotationOrigin
        { get; }

        /// <summary>
        /// The angle to the first click point
        /// </summary>
        protected double _AngleOffset;

        private double _Angle = 0;

        protected double Angle
        {
            get => _Angle;
            set
            {
                _Angle = value;
                OnAngleChanged();
            }
        }

        protected abstract void OnAngleChanged();


        public virtual string[] HelpStrings
        {
            get
            {
                List<string> s = [.. RotateTranslateScaleCommand.DefaultMouseHelpStrings];
                s.AddRange(TranslateScaleCommandBase.DefaultMouseHelpStrings);
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                s.Sort();
                return [.. s];
            }
        }

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            //Reset size scale if the middle mouse button is pushed
            if (e.Button.Middle())
            {
                SizeScale = 1.0;
                return;
            }
            else if (e.Button.Right())
            {
                Vector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
                // Vector2 Center = this.TranslatedVolumePosition;
                _AngleOffset = Vector2.Angle(VolumeRotationOrigin, WorldPosition) - Angle;
            }
            else
            {
                base.OnMouseDown(sender, e);
            }
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button.Right())
            {
                Vector2 worldPosition = Parent.ScreenToWorld(e.X, e.Y);
                //Vector2 origin = this.TranslatedVolumePosition;
                //Vector2 centroid = this.OriginalVolumePosition;

                if (VolumeRotationOrigin == worldPosition)
                {
                    return;
                }

                Angle = Vector2.Angle(VolumeRotationOrigin, worldPosition) - _AngleOffset;

                //Save as old mouse position so location doesn't jump when we release the right mouse button
                SaveAsOldMousePosition(e);
            }
            else
            {
                base.OnMouseMove(sender, e);
            }
        }

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);
    }
}
