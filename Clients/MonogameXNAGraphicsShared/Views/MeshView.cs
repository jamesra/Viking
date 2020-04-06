using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.ObjectModel;
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
        
        public void Draw(GraphicsDevice device, IScene scene, CullMode cullmode = CullMode.CullCounterClockwiseFace)
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
                rstate.DepthClipEnable = true;
                device.RasterizerState = rstate;
            }
            else
            {
                RasterizerState rstate = new RasterizerState();
                rstate.CullMode = cullmode;
                rstate.FillMode = FillMode.Solid;
                rstate.DepthClipEnable = true;
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

            foreach (MeshModel<VERTEXTYPE> model in models)
            {
                if (model == null)
                    continue;

                if (model.Edges == null)
                    continue;

                if (model.Verticies == null)
                    continue;

                effect.World = model.ModelMatrix * scene.World;
                if (model.Edges.Length == 0)
                    continue; 

                if (VertexHasNormals(model.Verticies))
                {
                    effect.EnableDefaultLighting();
                }
                else
                {
                    effect.LightingEnabled = false; 
                }

                effect.VertexColorEnabled = VertexHasColor(model.Verticies);
                
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                      
                    device.DrawUserIndexedPrimitives<VERTEXTYPE>(PrimitiveType.TriangleList, model.Verticies, 0, model.Verticies.Length, model.Edges, 0, model.Edges.Length / 3);
                } 
            }


            if (originalRasterizerState != null)
                device.RasterizerState = originalRasterizerState;
        }

        public static bool VertexHasNormals(VERTEXTYPE[] verticies)
        {
            if (verticies.Length == 0)
                return false;

            VertexElement[] elements = verticies[0].VertexDeclaration.GetVertexElements();

            return elements.Any(e => e.VertexElementUsage == VertexElementUsage.Normal);
        }

        public static bool VertexHasColor(VERTEXTYPE[] verticies)
        {
            if (verticies.Length == 0)
                return false;

            VertexElement[] elements = verticies[0].VertexDeclaration.GetVertexElements();

            return elements.Any(e => e.VertexElementUsage == VertexElementUsage.Color);
        }

        public static void Draw(GraphicsDevice device, IScene scene, BasicEffect effect = null, CullMode cullmode = CullMode.CullCounterClockwiseFace, ICollection < MeshView<VERTEXTYPE>> meshViews = null)
        {
            if (meshViews == null)
                return;

            IEnumerable<MeshModel<VERTEXTYPE>> all_models = meshViews.SelectMany(mv => mv.models);

            Draw(device, scene, effect, cullmode,  all_models);
        }

        public static void Draw(GraphicsDevice device, IScene scene, BasicEffect effect=null, CullMode cullmode = CullMode.CullCounterClockwiseFace, IEnumerable<MeshModel<VERTEXTYPE>> meshmodels = null)
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
            rstate.FillMode = FillMode.Solid; 
            rstate.DepthClipEnable = true;
            device.RasterizerState = rstate;

            effect.SetScene(scene);
            effect.AmbientLightColor = Color.White.ToVector3();
            effect.TextureEnabled = false;
            effect.Alpha = 1f;
            effect.DiffuseColor = Color.Wheat.ToVector3();
            //effect.View = scene.View;
            //effect.Projection = scene.Projection;



            /*
            PolygonOverlayEffect effect = DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device);
            if (effect == null)
                return;


            effect.InputLumaAlphaValue = 0.0f;

            Matrix WorldViewProjOriginal = effect.WorldViewProjMatrix;

            foreach (MeshModel<VERTEXTYPE> model in meshmodels)
            {
                effect.WorldViewProjMatrix = scene.ViewProj * model.ModelMatrix;    
                
                if(model.Verticies.Length == 0 || model.Edges.Length == 0)
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
            */

            foreach (MeshModel<VERTEXTYPE> model in meshmodels)
            {
                if (model == null)
                    continue;

                if (model.Edges == null)
                    continue;

                if (model.Verticies == null)
                    continue;

                effect.World = model.ModelMatrix * scene.World;
                if (model.Edges.Length == 0)
                    continue;

                if (VertexHasNormals(model.Verticies))
                {
                    effect.EnableDefaultLighting();
                }
                else
                {
                    effect.LightingEnabled = false;
                }

                effect.VertexColorEnabled = VertexHasColor(model.Verticies);

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    device.DrawUserIndexedPrimitives<VERTEXTYPE>(PrimitiveType.TriangleList, model.Verticies, 0, model.Verticies.Length, model.Edges, 0, model.Edges.Length / 3);
                }
            }



            if (originalRasterizerState != null)
                device.RasterizerState = originalRasterizerState;
        }
    }
}
