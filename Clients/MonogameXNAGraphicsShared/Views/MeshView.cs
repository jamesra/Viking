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
         
        public MeshView()
        {
        }
        
        public void Draw(GraphicsDevice device, IScene scene)
        {
            if (models == null)
                return;

            if (WireFrame)
            {
                RasterizerState rstate = new RasterizerState();
                rstate.CullMode = CullMode.None;
                rstate.FillMode = FillMode.WireFrame;
                device.RasterizerState = rstate;
            }
            else
            {
                RasterizerState rstate = new RasterizerState();
                rstate.CullMode = CullMode.None;
                rstate.FillMode = FillMode.Solid;
                device.RasterizerState = rstate;
            }

            foreach (MeshModel<VERTEXTYPE> model in models)
            {
                
                using (BasicEffect effect = new BasicEffect(device))
                {
                    effect.SetScene(scene);
                    effect.World = model.ModelMatrix;
                    effect.AmbientLightColor = Color.White.ToVector3();
                    effect.VertexColorEnabled = true;
                    effect.LightingEnabled = false;
                    effect.CurrentTechnique = effect.Techniques[0];

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                      
                        device.DrawUserIndexedPrimitives<VERTEXTYPE>(PrimitiveType.TriangleList, model.Verticies, 0, model.Verticies.Length, model.Edges, 0, model.Edges.Length / 3);
                    }
                }
            } 
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

            effect.InputLumaAlphaValue = 0.0f;

            Matrix WorldViewProjOriginal = effect.WorldViewProjMatrix;

            foreach (MeshModel<VERTEXTYPE> model in meshmodels)
            {
                effect.WorldViewProjMatrix = scene.ViewProj * model.ModelMatrix;                

                foreach (EffectPass pass in effect.effect.CurrentTechnique.Passes)
                {
                     pass.Apply();

                    device.DrawUserIndexedPrimitives<VERTEXTYPE>(PrimitiveType.TriangleList, model.Verticies, 0, model.Verticies.Length, model.Edges, 0, model.Edges.Length / 3);
                }
            }

            effect.WorldViewProjMatrix = WorldViewProjOriginal;
            
        }
    }
}
