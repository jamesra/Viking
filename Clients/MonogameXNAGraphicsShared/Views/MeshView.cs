using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public bool WireFrame { get; set; }
        public ObservableCollection<MeshModel<VERTEXTYPE>> models = new ObservableCollection<MeshModel<VERTEXTYPE>>();

        BasicEffect effect;

        public string Name = "";

        public MeshView()
        {
        }
        
        public void Draw(GraphicsDevice device,
            IScene scene, 
            CullMode cullmode = CullMode.CullCounterClockwiseFace)
        {
            if (models == null)
                return;

            if(effect == null || effect.IsDisposed)
            {
                effect = new BasicEffect(device);
            }

            RasterizerState originalRasterizerState = device.RasterizerState;
            if (WireFrame)
            {
                RasterizerState rstate = new RasterizerState();
                rstate.CullMode = cullmode;
                rstate.FillMode = FillMode.WireFrame;
                //rstate.DepthClipEnable = true;
                device.RasterizerState = rstate;
            }
            else
            {
                RasterizerState rstate = new RasterizerState();
                rstate.CullMode = cullmode;
                rstate.FillMode = FillMode.Solid;
                //rstate.DepthClipEnable = true;
                device.RasterizerState = rstate; 
            }

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

                foreach (MeshModel<VERTEXTYPE> model in group)
                {
                    effect.World = model.ModelMatrix * scene.World;

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        device.DrawUserIndexedPrimitives<VERTEXTYPE>(model.Primitive, model.Verticies, 0, model.Verticies.Length, model.Edges, 0, model.PrimitiveCount);
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
            IEnumerable < MeshView<VERTEXTYPE>> meshViews = null)
        {
            if (meshViews == null)
                return;

            IEnumerable<MeshModel<VERTEXTYPE>> all_models = meshViews.SelectMany(mv => mv.models);

            Draw(device, scene, effect, cullmode, fillMode,  all_models);
        }

        public void Draw(GraphicsDevice device,
            IScene scene,
            PolygonOverlayEffect effect = null,
            CullMode cullmode = CullMode.CullCounterClockwiseFace)
        {  
            FillMode fillMode = this.WireFrame ? FillMode.WireFrame : FillMode.Solid;
             
            Draw(device, scene, effect, cullmode, fillMode, models);
        }

        public static void Draw(GraphicsDevice device,
            IScene scene,
            PolygonOverlayEffect effect = null,
            CullMode cullmode = CullMode.CullCounterClockwiseFace,
            FillMode fillMode = FillMode.Solid,
            IEnumerable<MeshModel<VERTEXTYPE>> meshmodels = null)
        {
            if (effect == null)
            {
                effect = DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device);
                if (effect == null)
                    return;

                effect.InputLumaAlphaValue = 0.0f;
            }


            RasterizerState originalRasterizerState = device.RasterizerState;

            RasterizerState rstate = new RasterizerState();
            rstate.CullMode = cullmode;
            rstate.FillMode = fillMode;
            // rstate.DepthClipEnable = true;
            device.RasterizerState = rstate;

            Matrix WorldViewProjOriginal = effect.WorldViewProjMatrix;

            foreach (MeshModel<VERTEXTYPE> model in meshmodels)
            {
                //This can occur if LazyInitialization has not occurred.
                if (model == null)
                    continue; 

                effect.WorldViewProjMatrix = (model.ModelMatrix * scene.World) * scene.ViewProj ;

                if (model.Verticies.Length == 0 || model.Edges.Length == 0)
                {
                    continue;
                }

                foreach (EffectPass pass in effect.effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    device.DrawUserIndexedPrimitives<VERTEXTYPE>(PrimitiveType.TriangleList, model.Verticies, 0, model.Verticies.Length, model.Edges, 0, model.Edges.Length / 3);
                }
            }

            effect.WorldViewProjMatrix = WorldViewProjOriginal;

            if (originalRasterizerState != null)
                device.RasterizerState = originalRasterizerState;
        }

        public static void Draw(GraphicsDevice device,
            IScene scene,
            BasicEffect effect=null,
            CullMode cullmode = CullMode.CullCounterClockwiseFace,
            FillMode fillMode = FillMode.Solid,
            IEnumerable<MeshModel<VERTEXTYPE>> meshmodels = null)
        {
            if (meshmodels == null)
                return;

            if (effect == null || effect.IsDisposed)
            {
                effect = new BasicEffect(device);
            }
             
            RasterizerState originalRasterizerState = device.RasterizerState;

            RasterizerState rstate = new RasterizerState();
            rstate.CullMode = cullmode;
            rstate.FillMode = fillMode; 
           // rstate.DepthClipEnable = true;
            device.RasterizerState = rstate;

            effect.SetScene(scene);
            effect.AmbientLightColor = Color.White.ToVector3();
            effect.TextureEnabled = false;
            effect.Alpha = 1f;
            effect.DiffuseColor = Color.Wheat.ToVector3();
            //effect.View = scene.View;
            //effect.Projection = scene.Projection;



            /*
            
            */
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
                    if (!group.Any())
                        continue;  

                    effect.World = model.ModelMatrix * scene.World;

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        device.DrawUserIndexedPrimitives<VERTEXTYPE>(model.Primitive, model.Verticies, 0, model.Verticies.Length, model.Edges, 0, model.PrimitiveCount);
                    }
                }
            }

            if (originalRasterizerState != null)
                device.RasterizerState = originalRasterizerState;
        } 
    }
}
