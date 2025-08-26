using Geometry;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.Common
{

    public interface ISectionOverlayExtension
    {
        /// <summary>
        /// Name of the overlay for UI purposes
        /// </summary>
        /// <returns></returns>
        string Name();

        /// <summary>
        /// Used to sort all extensions to determine draw order
        /// </summary>
        /// <returns></returns>
        int DrawOrder();

        /// <summary>
        /// Must be called before draw
        /// </summary>
        /// <param name="parent"></param>
        void SetParent(Viking.UI.Controls.SectionViewerControl parent);

        /// <summary>
        /// The UI is being asked to select an object.  The extension should respond to this method with the object if it exists 
        /// and the distance to object.  Return null if no object can be selected at the given point
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        object ObjectAtPosition(GridVector2 WorldPosition, out double distance);

        /// <summary>
        /// Draw the specified overlay extension on the render target.  
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="Bounds"></param>
        /// <param name="DownSample"></param>
        /// <param name="BackgroundLuma">Texture matching size of client with Luma value of each pixel</param>
        /// <param name="BackgroundColors">Texture matching size of client window with RGB values for each pixel.  May be null of no color data available</param>
        void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Texture BackgroundLuma, Texture BackgroundColors, ref int NextStencilValue);
    }
}
