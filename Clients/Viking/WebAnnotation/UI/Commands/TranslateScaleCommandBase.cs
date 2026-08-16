using Geometry;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.Input;
using Viking.UI;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    internal abstract class TranslateScaleCommandBase : AnnotationCommandBase
    {
        public new static string[] DefaultMouseHelpStrings = [
           "Hold Left+Click Drag to move",
           "Release Left button to place",
           "Scroll wheel: Change size",
           "SHIFT + Scroll wheel: Change size slowly"
        ];

        protected Viking.VolumeModel.IVolumeToSectionTransform mapping;

        private double _SizeScale = 1.0;
        protected virtual double SizeScale
        {
            get => _SizeScale;
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

        protected Vector2 OriginalVolumePosition;
        protected Vector2 VolumePositionDeltaSum = new(0, 0);

        public abstract double AnnotationRadius { get; }

        /// <summary>
        /// Position of volume origin after applying this translation command
        /// </summary>
        protected Vector2 TranslatedVolumePosition
        {
            get; private set;
        }

        protected Vector2 OriginalMosaicPosition;
        protected Vector2 MosaicPositionDeltaSum = new(0, 0);

        /// <summary>
        /// Position of mosaic origin after applying this translation command
        /// </summary>
        protected Vector2 TranslatedMosaicPosition
        {
            get; private set;
        }

        protected abstract void OnTranslationChanged();

        /// <summary>
        /// Resets the command to have an origin at the given point
        /// </summary>
        /// <param name="VolumePoint"></param>
        protected void ResetCommandVolumeOrigin(Vector2 VolumePoint)
        {
            OriginalVolumePosition = VolumePoint;
            OriginalMosaicPosition = mapping.VolumeToSection(VolumePoint);
            VolumePositionDeltaSum = new Vector2(0, 0);
            MosaicPositionDeltaSum = new Vector2(0, 0);
            TranslatedVolumePosition = OriginalVolumePosition + VolumePositionDeltaSum;
            TranslatedMosaicPosition = OriginalMosaicPosition;
        }

        /// <summary>
        /// Resets the command to have an origin at the given point
        /// </summary>
        /// <param name="VolumePoint"></param>
        protected void ResetCommandMosaicOrigin(Vector2 MosaicPoint)
        {
            OriginalVolumePosition = mapping.SectionToVolume(MosaicPoint);
            OriginalMosaicPosition = MosaicPoint;
            VolumePositionDeltaSum = new Vector2(0, 0);
            MosaicPositionDeltaSum = new Vector2(0, 0);
            TranslatedVolumePosition = OriginalVolumePosition + VolumePositionDeltaSum;
            TranslatedMosaicPosition = OriginalMosaicPosition;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="OriginalVolumePosition">The point the command started, where the mouse cursor was, in mosaic space</param>
        public TranslateScaleCommandBase(Viking.UI.Controls.SectionViewerControl parent, Vector2 OriginalVolumePosition) : base(parent)
        {
            parent.OnSectionChanged += OnSectionChanged;
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            ResetCommandVolumeOrigin(OriginalVolumePosition);
            ScaleOrigin = OriginalVolumePosition;
        }

        protected Task OnSectionChanged(object sender, Viking.Common.SectionChangedEventArgs e, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            mapping = Parent.Section.ActiveSectionToVolumeTransform;
            return Task.CompletedTask;
        }

        protected override void OnDeactivate()
        {
            Parent.OnSectionChanged -= OnSectionChanged;

            base.OnDeactivate();
        }

        protected double GetScalarForScrollWheelDelta(int scroll_delta_sum)
        {
            if (Math.Abs(scroll_delta_sum) < 120)
            {
                return 1.0;
            }

            int adjusted_scroll_distance = Math.Abs(scroll_delta_sum) - 120;

            //OK, so lets figure out how far we need to scrool 
            const double Scroll_distance_to_double_size = 900.0;

            double num_doublings = adjusted_scroll_distance / (double)Scroll_distance_to_double_size;

            double scalar = Math.Pow(1.25, num_doublings);

            if (scroll_delta_sum < 0)
            {
                scalar = 1 / scalar;
            }

            Trace.WriteLine($"{adjusted_scroll_distance} {num_doublings} {scalar}");

            return scalar;
        }

        private int scroll_delta_sum = 0;
        protected override void OnMouseWheel(object sender, MouseEventArgs e)
        {
            Trace.WriteLine(e.Delta.ToString());

            if (ModifierKeysConverter.FromWinFormsKeys((int)Control.ModifierKeys).ShiftPressed())
            {
                scroll_delta_sum += (int)(e.Delta / 5.0);
            }
            else
            {
                scroll_delta_sum += e.Delta;
            }

            double scalar = GetScalarForScrollWheelDelta(scroll_delta_sum);

            //Trace.WriteLine(scalar.ToString());
            SizeScale = scalar;
            Parent.Invalidate();
        }


        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            //Redraw if we are dragging a location
            if (oldMouse != null)
            {
                if (e.Button.LeftOnly())
                {
                    //Need to use last saved mouse position, because if a rotation or other non-translate command
                    //we don't want the mouse to jump
                    Vector2 LastVolumePosition = Parent.ScreenToWorld(oldMouse.X, oldMouse.Y);
                    Vector2 NewVolumePosition = Parent.ScreenToWorld(e.X, e.Y);

                    VolumePositionDeltaSum += NewVolumePosition - LastVolumePosition;

                    Vector2 NewMosaicPosition = mapping.VolumeToSection(OriginalVolumePosition + VolumePositionDeltaSum);

                    MosaicPositionDeltaSum = NewMosaicPosition - OriginalMosaicPosition;

                    TranslatedVolumePosition = OriginalVolumePosition + VolumePositionDeltaSum;
                    TranslatedMosaicPosition = NewMosaicPosition;

                    //UpdateViewPosition(NewVolumePosition - LastVolumePosition);
                    OnTranslationChanged();
                    Parent.Invalidate();
                }
            }

            base.OnMouseMove(sender, e);
        }

        protected Vector2 ScaleOrigin = Vector2.Zero;
        private double LastSavedScalarValue = 1.0;

        protected override void OnPenContact(object sender, PenEventArgs e)
        {
            base.OnPenContact(sender, e);
            if (e.Erase == false)
            {
                ScaleOrigin = Parent.ScreenToWorld(e.X, e.Y);
            }
        }

        protected override void OnPenLeaveContact(object sender, PenEventArgs e)
        {
            base.OnPenLeaveContact(sender, e);
            if (e.Erase)
            {
                return;
            }

            //Write down that scalar value so if we scale again we are not using the original scale
            LastSavedScalarValue = SizeScale;
        }

        protected override void OnPenMove(object sender, PenEventArgs e)
        {
            //Redraw if we are dragging a location
            if (oldPen != null & e.Erase == false)
            {
                if (e.InContact == false)
                {
                    //Need to use last saved mouse position, because if a rotation or other non-translate command
                    //we don't want the mouse to jump
                    Vector2 LastVolumePosition = Parent.ScreenToWorld(oldPen.X, oldPen.Y);
                    Vector2 NewVolumePosition = Parent.ScreenToWorld(e.X, e.Y);

                    VolumePositionDeltaSum += NewVolumePosition - LastVolumePosition;

                    Vector2 NewMosaicPosition =
                        mapping.VolumeToSection(OriginalVolumePosition + VolumePositionDeltaSum);

                    MosaicPositionDeltaSum = NewMosaicPosition - OriginalMosaicPosition;

                    TranslatedVolumePosition = OriginalVolumePosition + VolumePositionDeltaSum;
                    TranslatedMosaicPosition = NewMosaicPosition;
                }
                else
                {
                    //Need to use last saved mouse position, because if a rotation or other non-translate command
                    //we don't want the mouse to jump
                    Vector2 LastVolumePosition = ScaleOrigin;
                    Vector2 NewVolumePosition = Parent.ScreenToWorld(e.X, e.Y);


                    Vector2 delta = NewVolumePosition - LastVolumePosition;

                    double BlockDistance = delta.X + delta.Y;
                    double scale = BlockDistance / AnnotationRadius;
                    SizeScale = scale + LastSavedScalarValue;
                }

                OnTranslationChanged();
                Parent.Invalidate();
            }

            base.OnPenMove(sender, e);
        }

        protected override void OnPenLeaveRange(object sender, PenEventArgs e)
        {
            base.OnPenLeaveRange(sender, e);
            Execute();
        }


        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            base.OnMouseUp(sender, e);
            if (e.Button.Left())
            {
                Execute();
            }
        }

    }
}