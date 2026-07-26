using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VikingXNA;

namespace VikingXNAGraphics
{
    //Displays a mesh
    public class MeshView<VERTEXTYPE>
        where VERTEXTYPE : struct, IVertexType
    {
        private static int MeshViewDrawLogCount;
        private static readonly object RasterizerCacheLock = new();
        private static readonly Dictionary<(GraphicsDevice device, CullMode cull, FillMode fill), RasterizerState> RasterizerStateCache = [];

        private static RasterizerState GetOrCreateRasterizerState(GraphicsDevice device, CullMode cullMode, FillMode fillMode)
        {
            if (device == null || device.IsDisposed)
                return null;
            lock (RasterizerCacheLock)
            {
                var key = (device, cullMode, fillMode);
                if (RasterizerStateCache.TryGetValue(key, out var state) && state != null && !state.IsDisposed)
                    return state;
                RasterizerStateCache.Remove(key);
                var rstate = new RasterizerState { CullMode = cullMode, FillMode = fillMode };
                RasterizerStateCache[key] = rstate;
                return rstate;
            }
        }

        public bool WireFrame { get; set; }
        public readonly ObservableCollection<MeshModel<VERTEXTYPE>> models = [];

        BasicEffect effect;

        public string Name = "";

        public MeshView()
        {
        }

        public void Draw(GraphicsDevice device,
            IScene scene,
            CullMode cullmode = CullMode.CullCounterClockwiseFace)
        {
            if (models is null)
                return;

            if (effect is null || effect.IsDisposed)
            {
                effect = new BasicEffect(device);
            }

            RasterizerState originalRasterizerState = device.RasterizerState;
            FillMode fillMode = WireFrame ? FillMode.WireFrame : FillMode.Solid;
            RasterizerState rstate = GetOrCreateRasterizerState(device, cullmode, fillMode);
            if (rstate != null)
                device.RasterizerState = rstate;

            effect.SetScene(scene);
            effect.AmbientLightColor = Color.White.ToVector3();
            effect.TextureEnabled = false;
            effect.Alpha = 1f;
            effect.DiffuseColor = Color.Wheat.ToVector3();
            //effect.View = scene.View;
            //effect.Projection = scene.Projection;

            //effect.CurrentTechnique = effect.Techniques[0];

            //Find all of the models with something we can draw and group by characteristics
            var modelGroups = models.Where(m => m != null &&
                                                m.Edges != null &&
                                                m.Verticies != null &&
                                                m.Edges.Length != 0)
                                    .GroupBy(m => new { m.HasNormal, m.HasColor });

            foreach (var group in modelGroups)
            {
                if (group.Key.HasNormal)
                {
                    effect.EnableDefaultLighting();
                }
                else
                {
                    effect.LightingEnabled = false;
                }

                effect.VertexColorEnabled = group.Key.HasColor;

                Matrix sceneWorld = scene.World;
                foreach (MeshModel<VERTEXTYPE> model in group)
                {
                    if (!model.EnsureBuffers(device))
                        continue;

                    effect.World = model.ModelMatrix * sceneWorld;

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        device.SetVertexBuffer(model.VertexBuffer);
                        device.Indices = model.IndexBuffer;
                        device.DrawIndexedPrimitives(model.Primitive, 0, 0, model.PrimitiveCount);
                    }
                }
            }

            if (originalRasterizerState != null)
                device.RasterizerState = originalRasterizerState;
        }


        public static void Draw(GraphicsDevice device,
            IScene scene,
            BasicEffect effect = null,
            CullMode cullmode = CullMode.CullCounterClockwiseFace,
            FillMode fillMode = FillMode.Solid,
            IEnumerable<MeshView<VERTEXTYPE>> meshViews = null)
        {
            if (meshViews is null)
                return;

            IEnumerable<MeshModel<VERTEXTYPE>> all_models = meshViews.SelectMany(mv => mv.models);

            Draw(device, scene, effect, cullmode, fillMode, all_models);
        }

        public void Draw(GraphicsDevice device,
            IScene scene,
            PolygonOverlayEffect effect = null,
            CullMode cullmode = CullMode.CullClockwiseFace)
        {
            FillMode fillMode = this.WireFrame ? FillMode.WireFrame : FillMode.Solid;

            Draw(device, scene, effect, cullmode, fillMode, models);
        }

        public static void Draw(GraphicsDevice device,
            IScene scene,
            PolygonOverlayEffect effect = null,
            CullMode cullmode = CullMode.CullClockwiseFace,
            FillMode fillMode = FillMode.Solid,
            IEnumerable<MeshModel<VERTEXTYPE>> meshmodels = null)
        {
            if (effect is null)
            {
                effect = DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device);
                if (effect is null)
                    return;

                effect.InputLumaAlphaValue = 0.0f;
            }


            RasterizerState originalRasterizerState = device.RasterizerState;
            RasterizerState rstate = GetOrCreateRasterizerState(device, cullmode, fillMode);
            if (rstate != null)
                device.RasterizerState = rstate;

            Matrix worldViewProj = scene.World * scene.ViewProj;
            Matrix WorldViewProjOriginal = effect.WorldViewProjMatrix;

            foreach (MeshModel<VERTEXTYPE> model in meshmodels)
            {
                //This can occur if LazyInitialization has not occurred.
                if (model is null)
                    continue;

                if (!model.EnsureBuffers(device))
                    continue;

                effect.WorldViewProjMatrix = model.ModelMatrix * worldViewProj;

                foreach (EffectPass pass in effect.effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    device.SetVertexBuffer(model.VertexBuffer);
                    device.Indices = model.IndexBuffer;
                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, model.Edges.Length / 3);
                }
            }

            effect.WorldViewProjMatrix = WorldViewProjOriginal;

            if (originalRasterizerState != null)
                device.RasterizerState = originalRasterizerState;
        }

        public static void Draw(GraphicsDevice device,
            IScene scene,
            BasicEffect effect = null,
            CullMode cullmode = CullMode.CullCounterClockwiseFace,
            FillMode fillMode = FillMode.Solid,
            IEnumerable<MeshModel<VERTEXTYPE>> meshmodels = null)
        {
           if (meshmodels is null)
                return;

            #region agent log
            if (MeshViewDrawLogCount < 3)
            {
                var modelList = meshmodels.Where(m => m != null).ToList();
                int drawable = modelList.Count(m => m.Edges != null && m.Verticies != null && m.Edges.Length != 0 && m.Verticies.Length != 0);
                try
                {
                    string logPath = @"d:\src\git\VikingLegacy\debug-84f952.log";
                    long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    File.AppendAllText(logPath,
                        $"{{\"sessionId\":\"84f952\",\"timestamp\":{ts},\"location\":\"MeshView.cs:Draw\",\"message\":\"MeshView static draw\",\"hypothesisId\":\"D,E\",\"runId\":\"pre-fix\",\"data\":{{\"frame\":{MeshViewDrawLogCount},\"modelCount\":{modelList.Count},\"drawableCount\":{drawable},\"totalVerts\":{modelList.Sum(m => m.Verticies?.Length ?? 0)},\"totalEdges\":{modelList.Sum(m => m.Edges?.Length ?? 0)},\"cullMode\":\"{cullmode}\",\"fillMode\":\"{fillMode}\"}}}}\n");
                }
                catch { }
                MeshViewDrawLogCount++;
            }
            #endregion

            if (effect is null || effect.IsDisposed)
            {
                effect = new BasicEffect(device);
            }

            RasterizerState originalRasterizerState = device.RasterizerState;
            RasterizerState rstate = GetOrCreateRasterizerState(device, cullmode, fillMode);
            if (rstate != null)
                device.RasterizerState = rstate;

            effect.SetScene(scene);
            effect.AmbientLightColor = Color.White.ToVector3();
            effect.TextureEnabled = false;
            effect.Alpha = 1f;
            effect.DiffuseColor = Color.Wheat.ToVector3();

            Matrix worldOriginal = effect.World;
            Matrix sceneWorld = scene.World;
            var modelGroups = meshmodels.Where(m => m != null &&
                                                m.Edges != null &&
                                                m.Verticies != null &&
                                                m.Edges.Length != 0)
                                    .GroupBy(m => new { m.HasNormal, m.HasColor });

            foreach (var group in modelGroups)
            {
                if (group.Key.HasNormal)
                {
                    effect.EnableDefaultLighting();
                }
                else
                {
                    effect.LightingEnabled = false;
                }

                effect.VertexColorEnabled = group.Key.HasColor;

                foreach (MeshModel<VERTEXTYPE> model in group)
                {
                    if (!model.EnsureBuffers(device))
                        continue;

                    effect.World = model.ModelMatrix * sceneWorld;

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        device.SetVertexBuffer(model.VertexBuffer);
                        device.Indices = model.IndexBuffer;
                        device.DrawIndexedPrimitives(model.Primitive, 0, 0, model.PrimitiveCount);
                    }
                }
            }

            effect.World = worldOriginal;

            if (originalRasterizerState != null)
                device.RasterizerState = originalRasterizerState;
        }
    }
}
