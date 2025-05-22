using VikingXNAGraphics;

namespace WebAnnotation.Actions
{
    /// <summary>
    /// Fades an IColorView object 
    /// </summary>
    internal class MouseOverLocationCanvasViewEffect
    {
        private object _viewObj = null;

        public object viewObj
        {
            get => _viewObj;
            set
            {
                if (_viewObj != null)
                {
                    RemoveEffect(_viewObj);
                }

                if (value != null)
                {
                    _viewObj = value;
                    ApplyEffect(_viewObj);
                }
            }
        }

        private float OriginalAlpha;

        public MouseOverLocationCanvasViewEffect()
        {
        }

        protected void ApplyEffect(object view_obj)
        {
            IColorView cView = view_obj as IColorView;
            if (cView != null)
            {
                OriginalAlpha = cView.Alpha;
                cView.Alpha /= 2.0f;
            }
        }

        protected void RemoveEffect(object view_obj)
        {
            IColorView cView = view_obj as IColorView;
            if (cView != null)
            {
                cView.Alpha = OriginalAlpha;
            }
        }
    }
}
