using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Viking;
using Viking.Rendering;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using Rectangle = Geometry.Rectangle;

namespace WebAnnotation
{
    /// <summary>
    /// Draws and hit-tests section annotations on a shared MonoGame device.
    /// </summary>
    public sealed class AnnotationScene : IAnnotationScene
    {
        readonly Volume _volume;
        readonly VolumeTransformProvider _transforms;
        readonly Dictionary<int, SectionAnnotationsView> _sections = new();
        SpriteBatch _spriteBatch;
        SpriteFont _font;
        BasicEffect _basicEffect;
        BlendState _defaultBlendState;

        public AnnotationScene(Volume volume)
        {
            _volume = volume;
            _transforms = new VolumeTransformProvider(volume);
#if !NETFRAMEWORK
            AnnotationOverlay.SectionViewLookup = GetOrCreate;
#endif
        }

        public VolumeTransformProvider Transforms => _transforms;

        internal SectionAnnotationsView GetOrCreate(int sectionNumber)
        {
            if (_sections.TryGetValue(sectionNumber, out SectionAnnotationsView existing))
                return existing;

            if (!_volume.Sections.TryGetValue(sectionNumber, out Section section))
                return null;

            SectionAnnotationsView view = new(section, _transforms, _volume);
            _sections[sectionNumber] = view;
            return view;
        }

        public void LoadVisible(VikingXNA.Scene scene, int sectionNumber)
        {
            SectionAnnotationsView view = GetOrCreate(sectionNumber);
            view?.LoadAnnotationsInRegion(scene, System.Threading.CancellationToken.None);
        }

        public object HitTest(int sectionNumber, Geometry.Vector2 worldPosition, out double distance)
        {
            distance = double.MaxValue;
            SectionAnnotationsView locView = GetOrCreate(sectionNumber);
            if (locView is null)
                return null;

            List<HitTestResult> listObjects = locView.GetAnnotations(worldPosition);
            HitTestResult bestHit = listObjects.NearestObjectOnCurrentSectionThenAdjacent(sectionNumber);
            if (bestHit is null)
                return null;

            distance = bestHit.Distance;
            ICanvasGeometryView bestObj = bestHit.obj as ICanvasGeometryView;
            if (bestObj is ICanvasViewContainer container)
            {
                bestObj = container.GetAnnotationAtPosition(worldPosition) as ICanvasGeometryView;
                if (bestObj != null)
                    distance = bestObj.DistanceFromCenterNormalized(worldPosition);
            }

            return bestObj;
        }

        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, int sectionNumber, Texture backgroundLuma, Texture backgroundColors, ref int nextStencilValue)
        {
            SectionAnnotationsView current = GetOrCreate(sectionNumber);
            if (current is null || scene is null)
                return;

            LoadVisible(scene, sectionNumber);

            ContentManager content = VikingXNAGraphics.Global.Content;
            OverlayShaderEffect overlayEffect = DeviceEffectsStore<OverlayShaderEffect>.GetOrCreateForDevice(graphicsDevice, content);
            RoundLineCode.LumaOverlayRoundLineManager lineManager = DeviceEffectsStore<RoundLineCode.LumaOverlayRoundLineManager>.GetOrCreateForDevice(graphicsDevice, content);
            RoundCurve.CurveManagerHSV curveManager = DeviceEffectsStore<RoundCurve.CurveManagerHSV>.GetOrCreateForDevice(graphicsDevice, content);

            if (backgroundLuma != null && overlayEffect != null)
            {
                overlayEffect.LumaTexture = backgroundLuma;
                overlayEffect.RenderTargetSize = graphicsDevice.Viewport;
            }

            if (backgroundLuma != null && lineManager != null)
            {
                lineManager.LumaTexture = backgroundLuma;
                lineManager.RenderTargetSize = graphicsDevice.Viewport;
            }

            if (_basicEffect is null || _basicEffect.IsDisposed)
                _basicEffect = new BasicEffect(graphicsDevice);
            _basicEffect.SetScene(scene);

            if (_spriteBatch is null || _spriteBatch.GraphicsDevice.IsDisposed)
                _spriteBatch = new SpriteBatch(graphicsDevice);
            _font ??= content?.Load<SpriteFont>("Arial");

            BlendState originalBlendState = graphicsDevice.BlendState;
            RasterizerState originalRaster = graphicsDevice.RasterizerState;
            int startingStencil = nextStencilValue;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, startingStencil);

            Rectangle bounds = scene.VisibleWorldBounds;
            ICollection<LocationCanvasView> locations = current.GetLocations(bounds);
            List<LocationCanvasView> visible = locations.Where(l => l != null && l.Parent != null && l.Parent.Type != null && l.IsVisible(scene)).ToList();
            ICollection<LocationCanvasView> adjacent = current.AdjacentLocationsNotOverlappedInRegion(bounds);
            List<LocationCanvasView> visibleAdjacent = adjacent.Where(l => l != null && l.IsVisible(scene)).ToList();

            LocationObjRenderer.DrawBackgrounds(visible, graphicsDevice, _basicEffect, overlayEffect, lineManager, curveManager, scene, sectionNumber);
            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);
            LocationObjRenderer.DrawBackgrounds(visibleAdjacent, graphicsDevice, _basicEffect, overlayEffect, lineManager, curveManager, scene, sectionNumber);

            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, startingStencil);
            LocationLinkView.Draw(graphicsDevice, scene, lineManager, _basicEffect, overlayEffect, current.NonOverlappedLocationLinksInRegion(bounds));

            graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, 1, 0);

            if (_defaultBlendState is null || _defaultBlendState.IsDisposed)
            {
                _defaultBlendState = new BlendState
                {
                    AlphaBlendFunction = BlendFunction.Add,
                    AlphaSourceBlend = Blend.SourceAlpha,
                    AlphaDestinationBlend = Blend.DestinationAlpha,
                    ColorSourceBlend = Blend.SourceColor,
                    ColorDestinationBlend = Blend.DestinationColor,
                    ColorBlendFunction = BlendFunction.Add
                };
            }

            graphicsDevice.BlendState = _defaultBlendState;
            DeviceStateManager.SetRasterizerStateForShapes(graphicsDevice);
            DeviceStateManager.SetRenderStateForShapes(graphicsDevice);
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);

            List<StructureLinkViewModelBase> structureLinks = current.VisibleStructureLinks(scene);
            StructureLinkCirclesView.Draw(graphicsDevice, scene, lineManager, structureLinks.OfType<StructureLinkCirclesView>().ToArray());
            StructureLinkCurvesView.Draw(graphicsDevice, scene, lineManager, structureLinks.OfType<StructureLinkCurvesView>().ToArray());

            if (_font != null)
            {
                _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                foreach (LocationCanvasView loc in visible.Concat(visibleAdjacent))
                {
                    if (loc is ILabelView label && label.IsLabelVisible(scene))
                        label.DrawLabel(_spriteBatch, _font, scene);
                }
                _spriteBatch.End();
            }

            if (originalRaster != null && !originalRaster.IsDisposed)
                graphicsDevice.RasterizerState = originalRaster;
            if (originalBlendState != null)
                graphicsDevice.BlendState = originalBlendState;
            nextStencilValue++;
        }
    }
}
