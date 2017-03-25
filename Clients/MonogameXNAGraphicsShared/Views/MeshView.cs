using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.ObjectModel;

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
        
        public void Draw(GraphicsDevice device, VikingXNA.Scene scene)
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

            using (BasicEffect effect = new BasicEffect(device))
            {
                effect.View = scene.Camera.View;
                effect.World = scene.World;
                effect.Projection = scene.Projection;
                effect.AmbientLightColor = Color.White.ToVector3();
                effect.VertexColorEnabled = true;
                effect.LightingEnabled = false;

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    foreach (MeshModel<VERTEXTYPE> model in models)
                    {   
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

            foreach (EffectPass pass in effect.effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                foreach (MeshModel<VERTEXTYPE> model in meshmodels)
                {
                    device.DrawUserIndexedPrimitives<VERTEXTYPE>(PrimitiveType.TriangleList, model.Verticies, 0, model.Verticies.Length, model.Edges, 0, model.Edges.Length / 3);
                }
            }
            
        }
    }
}
