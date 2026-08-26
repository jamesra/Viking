using System.Windows.Input;
using Geometry;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// One current-tool object. The thin host forwards pointer/key events until Done/Cancel.
    /// </summary>
    public interface IAnnotationTool
    {
        string StatusText { get; }

        AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount);

        void OnMouseMove(Vector2 screen);

        void OnMouseUp();

        AnnotationToolResult OnKeyDown(Key key);

        void Cancel();
    }
}
