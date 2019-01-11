using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel;
using Geometry;
using WebAnnotation.View;
using Viking.VolumeModel;
using SqlGeometryUtils;
using VikingXNAGraphics;
using System.Windows.Forms;
using System.Diagnostics;
using WebAnnotation;
using System.Collections.ObjectModel;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    abstract class TranslateScaleCommandBase : AnnotationCommandBase
    {
        public new static string[] DefaultMouseHelpStrings = new String[] {
           "Hold Left+Click Drag to move",
           "Release Left button to place",
           "Scroll wheel: Change size",
           "SHIFT + Scroll wheel: Change size slowly"
        };
         
        protected Viking.VolumeModel.IVolumeToSectionTransform mapping;

        private double _SizeScale = 1.0;
        protected virtual double SizeScale
        {
            get { return _SizeScale; }
            set
            {
                if (value != _SizeScale)
                {
                    _SizeScale = value;
                    OnSizeScaleChanged();
                }
            }
        }

        protected abstract void OnSizeScaleChanged();

        protected GridVector2 OriginalVolumePosition;
        protected GridVector2 VolumePositionDeltaSum = new GridVector2(0, 0);
         
        /// <summary>
        /// Position of volume origin after applying this translation command
        /// </summary>
        protected GridVector2 TranslatedVolumePosition
        {
            get; private set;
        }

        protected GridVector2 OriginalMosaicPosition;
        protected GridVector2 MosaicPositionDeltaSum = new GridVector2(0, 0);

        /// <summary>
        /// Position of mosaic origin after applying this translation command
        /// </summary>
        protected GridVector2 TranslatedMosaicPosition
        {
            get; private set;
        }

        protected abstract void OnTranslationChanged();

        /// <summary>
        /// Resets the command to have an origin at the given point
        /// </summary>
        /// <param name="VolumePoint"></param>
        protected void ResetCommandVolumeOrigin(GridVector2 VolumePoint)
        {
            OriginalVolumePosition = VolumePoint;
            OriginalMosaicPosition = mapping.VolumeToSection(VolumePoint);
            VolumePositionDeltaSum = new GridVector2(0, 0);
            MosaicPositionDeltaSum = new GridVector2(0, 0);
            TranslatedVolumePosition = OriginalVolumePosition + VolumePositionDeltaSum;
            TranslatedMosaicPosition = OriginalMosaicPosition;
        }

        /// <summary>
        /// Resets the command to have an origin at the given point
        /// </summary>
        /// <param name="VolumePoint"></param>
        protected void ResetCommandMosaicOrigin(GridVector2 MosaicPoint)
        {
            OriginalVolumePosition = mapping.SectionToVolume(MosaicPoint);
            OriginalMosaicPosition = MosaicPoint;
            VolumePositionDeltaSum = new GridVector2(0, 0);
            MosaicPositionDeltaSum = new GridVector2(0, 0);
            TranslatedVolumePosition = OriginalVolumePosition + VolumePositionDeltaSum;
            TranslatedMosaicPosition = OriginalMosaicPosition;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="OriginalVolumePosition">The point the command started, where the mouse cursor was, in mosaic space</param>
        public TranslateScaleCommandBase(Viking.UI.Controls.SectionViewerControl parent, GridVector2 OriginalVolumePosition) : base(parent)
        {
            parent.OnSectionChanged += this.OnSectionChanged;
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            ResetCommandVolumeOrigin(OriginalVolumePosition);
        }

        protected void OnSectionChanged(object sender, Viking.Common.SectionChangedEventArgs e)
        {
            mapping = Parent.Section.ActiveSectionToVolumeTransform;
        }
        
        public override void OnDeactivate()
        {
            Parent.OnSectionChanged -= this.OnSectionChanged;

            base.OnDeactivate();
        }

        protected double GetScalarForScrollWheelDelta(int scroll_delta_sum)
        {
            if (Math.Abs(scroll_delta_sum) < 120)
                return 1.0;

            int adjusted_scroll_distance = Math.Abs(scroll_delta_sum) - 120;

            //OK, so lets figure out how far we need to scrool 
            const double Scroll_distance_to_double_size = 900.0;

            double num_doublings = (double)adjusted_scroll_distance / (double)Scroll_distance_to_double_size;

            double scalar = Math.Pow(1.25, num_doublings);

            if (scroll_delta_sum < 0)
                scalar = 1 / scalar;

            Trace.WriteLine(string.Format("{0} {1} {2}", adjusted_scroll_distance, num_doublings, scalar));

            return scalar;
        }

        private int scroll_delta_sum = 0;
        protected override void OnMouseWheel(object sender, MouseEventArgs e)
        {
            Trace.WriteLine(e.Delta.ToString());

            if (Control.ModifierKeys.ShiftPressed())
                scroll_delta_sum += (int)(e.Delta / 5.0);
            else
                scroll_delta_sum += e.Delta;

            double scalar = GetScalarForScrollWheelDelta(scroll_delta_sum);

            //Trace.WriteLine(scalar.ToString());
            SizeScale = scalar;
            Parent.Invalidate();
        }


        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            //Redraw if we are dragging a location
            if (this.oldMouse != null)
            {
                if (e.Button.LeftOnly())
                {
                    //Need to use last saved mouse position, because if a rotation or other non-translate command
                    //we don't want the mouse to jump
                    GridVector2 LastVolumePosition = Parent.ScreenToWorld(oldMouse.X, oldMouse.Y);
                    GridVector2 NewVolumePosition = Parent.ScreenToWorld(e.X, e.Y); 

                    VolumePositionDeltaSum += NewVolumePosition - LastVolumePosition;

                    GridVector2 NewMosaicPosition = mapping.VolumeToSection(OriginalVolumePosition + VolumePositionDeltaSum);

                    MosaicPositionDeltaSum = NewMosaicPosition - this.OriginalMosaicPosition;

                    TranslatedVolumePosition = OriginalVolumePosition + VolumePositionDeltaSum;
                    TranslatedMosaicPosition = NewMosaicPosition;

                    //UpdateViewPosition(NewVolumePosition - LastVolumePosition);
                    OnTranslationChanged();
                    Parent.Invalidate();
                }
            }

            base.OnMouseMove(sender, e);
        }


        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            base.OnMouseUp(sender, e);
            if (e.Button.Left())
                this.Execute();
        }

    }
}