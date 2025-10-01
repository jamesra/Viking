using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Viking.UI;
using Viking.UI.Commands;
using Viking.UI.Controls;
using VikingXNA;
using VikingXNAGraphics;
using VikingXNAGraphics.Controls;
using WebAnnotation.UI.Actions;
using WebAnnotation.UI.ActionViews;

namespace WebAnnotation.UI.Commands.Segmentation
{
    public class SegmentationCommand : Viking.UI.Commands.Command
    {
        public SegmentationCommand(Viking.UI.Controls.SectionViewerControl parent) : base(parent)
        {
            // Initialize the command with default values or behaviors
            ID = Command._NextID++;
            oldMouse = null;
            oldPen = null;
            oldWorldPosition = new GridVector2(0, 0);
        }
        
        private bool isActive = false;
        private List<object> visibleAnnotations; // Adjust type based on your annotation model
        private Texture2D currentViewImage;

        /// <summary>
        /// Called when the command is activated
        /// </summary>
        public override void OnActivate()
        {
            base.OnActivate();
            isActive = true;
            
            // Start a task to gather annotations and capture the current view
            System.Threading.Tasks.Task.Run(() => GatherVisibleData());
        }

        /// <summary>
        /// Called when the command is deactivated
        /// </summary>
        protected override void OnDeactivate()
        {
            isActive = false;
            base.OnDeactivate();
        }

        /// <summary>
        /// Gathers all visible annotations and captures the current view image
        /// </summary>
        private void GatherVisibleData()
        {
            try
            {
                var viewer = this.Parent;
                if (viewer == null) return;

                // Execute on the UI thread since we're accessing UI elements
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() =>
                {
                    // Get all visible annotations from the viewer
                    visibleAnnotations = GetVisibleAnnotations(viewer);
                    
                    // Capture the current view as an image
                    currentViewImage = CaptureCurrentView(viewer);
                    
                    // Notify that we have gathered all necessary data
                    OnDataGathered();
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error gathering visible data: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all visible annotations from the viewer
        /// </summary>
        private List<object> GetVisibleAnnotations(SectionViewerControl viewer)
        {
            // Implementation will depend on how annotations are stored in the viewer
            var annotations = new List<object>();
            // Example: annotations = viewer.GetAnnotations().Where(a => a.IsVisible).ToList();
            return annotations;
        }

        /// <summary>
        /// Captures the current view as a texture
        /// </summary>
        private Texture2D CaptureCurrentView(SectionViewerControl viewer)
        {
            // Implementation depends on how the view is rendered in XNA
            var graphicsDevice = viewer.Device;
            if (graphicsDevice == null) return null;
            
            int width = viewer.Width;
            int height = viewer.Height;
            
            RenderTarget2D renderTarget = new RenderTarget2D(
                graphicsDevice,
                width,
                height,
                false,
                graphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);
            
            graphicsDevice.SetRenderTarget(renderTarget);
            // Add rendering code specific to your application
            graphicsDevice.SetRenderTarget(null);
            
            return renderTarget;
        }

        /// <summary>
        /// Called when all data has been gathered
        /// </summary>
        private void OnDataGathered()
        {
            Debug.WriteLine($"Gathered {visibleAnnotations?.Count ?? 0} visible annotations and captured current view");
            // Implement what to do with the gathered data
        }
    }
}