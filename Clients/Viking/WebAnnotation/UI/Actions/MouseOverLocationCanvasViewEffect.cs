using VikingXNAGraphics;
using WebAnnotation.ViewModel;

namespace WebAnnotation.Actions
{
    /// <summary>
    /// Marks the object under the mouse: an IColorView is faded to half its alpha, so what is under it shows through, and
    /// a location link is drawn fully visible instead (<see cref="LocationLinkView.IsMouseOver"/>), since a link that is
    /// not hovered is faint and invisible in the middle.
    /// </summary>
    internal class MouseOverLocationCanvasViewEffect
    {
        private object? _viewObj = null;

        public object viewObj
        {
            get => _viewObj;
            set
            {
                if (_viewObj != null)
                {
                    RemoveEffect(_viewObj);
                }

                // Forget the old object even when the mouse left everything, or the next object would remove its effect again.
                _viewObj = value;
                if (_viewObj != null)
                {
                    ApplyEffect(_viewObj);
                }
            }
        }

        private float OriginalAlpha;

        /// <summary>The alpha the effect left on the view, read back from it, so a change made since can be recognized.</summary>
        private float EffectAlpha;

        public MouseOverLocationCanvasViewEffect()
        {
        }

        protected void ApplyEffect(object view_obj)
        {
            if (view_obj is LocationLinkView link)
            {
                link.IsMouseOver = true;
                return;
            }

            if (view_obj is IColorView cView)
            {
                OriginalAlpha = cView.Alpha;
                cView.Alpha /= 2.0f;
                EffectAlpha = cView.Alpha;
            }
        }

        /// <summary>
        /// Puts the alpha back, unless something set the view's alpha while the effect was on (the Annotation Preferences
        /// opacity sliders, OK or Apply). Restoring then would undo that new opacity, so the view keeps it.
        /// </summary>
        protected void RemoveEffect(object view_obj)
        {
            if (view_obj is LocationLinkView link)
            {
                link.IsMouseOver = false;
                return;
            }

            if (view_obj is IColorView cView && cView.Alpha == EffectAlpha)
            {
                cView.Alpha = OriginalAlpha;
            }
        }
    }
}
