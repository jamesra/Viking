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

            if (WireFrame)
            {
                RasterizerState rstate = new RasterizerState();
                rstate.CullMode = cullmode;
                rstate.FillMode = FillMode.WireFrame;
                device.RasterizerState = rstate;
            }
            else
            {
                RasterizerState rstate = new RasterizerState();
                rstate.CullMode = cullmode;
                rstate.FillMode = FillMode.Solid;
                device.RasterizerState = rstate;
            }

            effect.SetScene(scene);
            effect.AmbientLightColor = Color.White.ToVector3();
            effect.VertexColorEnabled = true;
            //effect.CurrentTechnique = effect.Techniques[0];

            foreach (MeshModel<VERTEXTYPE> model in models)
            {
                if (model == null)
                    continue; 

                effect.World = model.ModelMatrix;
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

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                      
                    device.DrawUserIndexedPrimitives<VERTEXTYPE>(PrimitiveType.TriangleList, model.Verticies, 0, model.Verticies.Length, model.Edges, 0, model.Edges.Length / 3);
                } 
            } 
        }

        public static bool VertexHasNormals(VERTEXTYPE[] verticies)
        {
            if (verticies.Length == 0)
                return false;

            VertexElement[] elements = verticies[0].VertexDeclaration.GetVertexElements();

            return elements.Any(e => e.VertexElementUsage == VertexElementUsage.Normal);
        }

        public static void Draw(GraphicsDevice device, VikingXNA.Scene scene, ICollection<MeshView<VERTEXTYPE>> meshViews)
        {
            if (meshViews == null)
                return;

            IEnumerable<MeshModel<VERTEXTYPE>> all_models = meshViews.SelectMany(mv => mv.models);

            Draw(device, scene, all_models);
        }

        public static void Draw(GraphicsDevice device, VikingXNA.Scene scene, IEnumerable<MeshModel<VERTEXTYPE>> meshmodels)
        {
            if (meshmodels == null)
                return;

            PolygonOverlayEffect effect = DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device); 
            if (effect == null)
                return;

            RasterizerState originalRasterizerState = device.RasterizerState;

            RasterizerState rstate = new RasterizerState();
            rstate.CullMode = CullMode.CullClockwiseFace;
            rstate.FillMode = FillMode.Solid;
            device.RasterizerState = rstate;

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

            if(originalRasterizerState != null)
                device.RasterizerState = originalRasterizerState;
        }
    }
}
