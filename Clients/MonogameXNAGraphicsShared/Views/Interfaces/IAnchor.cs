using System;

namespace VikingXNAGraphics
{
    public enum HorizontalAlignment
    {
        CENTER,
        LEFT,
        RIGHT
    };

    public enum VerticalAlignment
    {
        CENTER,
        TOP,
        BOTTOM
    };
   

    /// <summary>
    /// Supports specifying where a view is drawn relative to a position.
    /// This has the same properties as IAnchor, but it does not change the interior contents of the view, only the bounding box position.
    /// Initially added for text
    /// </summary>
    interface IAnchor
    {
        /// <summary> 
        /// Center means the view is centered on the position.
        /// Left means the view begins at the position and is drawn to the right
        /// Right means the view ends at the position
        /// </summary>
        HorizontalAlignment Horizontal { get; set; }

        /// <summary> 
        /// Center means the view is centered on the position.
        /// Bottom means the view's bounding rectangles bottom edge is at the position
        /// Top means the view's bounding rectangles top edge is at the position
        /// </summary>
        VerticalAlignment Vertical { get; set; }
    }

    /// <summary>
    /// Supports specifying where information, such as text, is drawn within the boundaries of a view.
    /// This has the same properties as IAnchor, but it does not change the position of the view, only the contents.
    /// 
    /// </summary>
    interface IAlignment
    {
        /// <summary> 
        /// Center means the view is centered on the position.
        /// Left means the view begins at the position and is drawn to the right
        /// Right means the view ends at the position
        /// </summary>
        HorizontalAlignment Horizontal { get; set; }

        /// <summary> 
        /// Center means the view is centered on the position.
        /// Bottom means the view's bounding rectangles bottom edge is at the position
        /// Top means the view's bounding rectangles top edge is at the position
        /// </summary>
        VerticalAlignment Vertical { get; set; }
    }

    public abstract class AlignmentBase
    {
        HorizontalAlignment _Horizontal = HorizontalAlignment.CENTER;
        public HorizontalAlignment Horizontal
        {
            get { return _Horizontal; }
            set
            {
                if (_Horizontal != value)
                {
                    _Horizontal = value;
                    OnChange?.Invoke();
                }
            }
        }

        VerticalAlignment _Vertical = VerticalAlignment.CENTER;
        public VerticalAlignment Vertical
        {
            get { return _Vertical; }
            set
            {
                if (_Vertical != value)
                {
                    _Vertical = value;
                    OnChange?.Invoke();
                }
            }
        }

        public Action OnChange = null;

        public override string ToString()
        {
            return string.Format("{0} {1}", _Vertical, _Horizontal);
        }

        public override bool Equals(object obj)
        {
            var @base = obj as AlignmentBase;
            return @base != null &&
                   _Horizontal == @base._Horizontal &&
                   _Vertical == @base._Vertical;
        }

        public override int GetHashCode()
        {
            var hashCode = 1963821654;
            hashCode = hashCode * -1521134295 + _Horizontal.GetHashCode();
            hashCode = hashCode * -1521134295 + _Vertical.GetHashCode();
            return hashCode;
        }
    }


    public class Anchor : AlignmentBase, IAnchor
    {
        public static Anchor TopLeft => new Anchor {Horizontal = HorizontalAlignment.LEFT, Vertical = VerticalAlignment.TOP };
        public static Anchor TopCenter => new Anchor { Horizontal = HorizontalAlignment.CENTER, Vertical = VerticalAlignment.TOP };
        public static Anchor TopRight => new Anchor { Horizontal = HorizontalAlignment.RIGHT, Vertical = VerticalAlignment.TOP };
        public static Anchor CenterLeft => new Anchor { Horizontal = HorizontalAlignment.LEFT, Vertical = VerticalAlignment.CENTER };
        public static Anchor CenterCenter => new Anchor { Horizontal = HorizontalAlignment.CENTER, Vertical = VerticalAlignment.CENTER };
        public static Anchor CenterRight => new Anchor { Horizontal = HorizontalAlignment.RIGHT, Vertical = VerticalAlignment.CENTER };
        public static Anchor BottomLeft => new Anchor { Horizontal = HorizontalAlignment.LEFT, Vertical = VerticalAlignment.BOTTOM };
        public static Anchor BottomCenter => new Anchor { Horizontal = HorizontalAlignment.CENTER, Vertical = VerticalAlignment.BOTTOM };
        public static Anchor BottomRight => new Anchor { Horizontal = HorizontalAlignment.RIGHT, Vertical = VerticalAlignment.BOTTOM };

        
    }

    public class Alignment : AlignmentBase, IAlignment
    {
        public static Alignment TopLeft => new Alignment { Horizontal = HorizontalAlignment.LEFT, Vertical = VerticalAlignment.TOP };
        public static Alignment TopCenter => new Alignment { Horizontal = HorizontalAlignment.CENTER, Vertical = VerticalAlignment.TOP };
        public static Alignment TopRight => new Alignment { Horizontal = HorizontalAlignment.RIGHT, Vertical = VerticalAlignment.TOP };
        public static Alignment CenterLeft => new Alignment { Horizontal = HorizontalAlignment.LEFT, Vertical = VerticalAlignment.CENTER };
        public static Alignment CenterCenter => new Alignment { Horizontal = HorizontalAlignment.CENTER, Vertical = VerticalAlignment.CENTER };
        public static Alignment CenterRight => new Alignment { Horizontal = HorizontalAlignment.RIGHT, Vertical = VerticalAlignment.CENTER };
        public static Alignment BottomLeft => new Alignment { Horizontal = HorizontalAlignment.LEFT, Vertical = VerticalAlignment.BOTTOM };
        public static Alignment BottomCenter => new Alignment { Horizontal = HorizontalAlignment.CENTER, Vertical = VerticalAlignment.BOTTOM };
        public static Alignment BottomRight => new Alignment { Horizontal = HorizontalAlignment.RIGHT, Vertical = VerticalAlignment.BOTTOM }; 
    }


}
