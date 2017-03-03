using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Monographics
{
    //Displays a mesh
    public class MeshView
    {
        ColorPositionMeshModel[] models;
         
        private Matrix world = Matrix.CreateTranslation(new Vector3(0, 0, 0));
        private Matrix view = Matrix.CreateLookAt(new Vector3(0, 0, 10), new Vector3(0, 0, 0), Vector3.UnitY);
        private Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45), 800f / 480f, 0.1f, 100f);

        public MeshView()
        {
        }


        /*
            public void Update(GameTime gameTime)
            {
                double angle = ((double)((gameTime.TotalGameTime.TotalMilliseconds / 500) % 1000)) / 1000.0;
                angle *= 360;

                view = Matrix.CreateLookAt(new Vector3((float)Math.Cos(angle) * 10,
                                                       (float)Math.Sin(angle) * 10,
                                                        -1),
                                           new Vector3(0, 0, 0), Vector3.UnitZ);
          
            }
                             */

        public void Draw(GraphicsDevice device)
        {
            if (models == null)
                return;

            using (BasicEffect effect = new BasicEffect(device))
            {
                effect.View = view;
                effect.World = world;
                effect.Projection = projection;

                foreach (ColorPositionMeshModel model in models)
                {   
                    effect.AmbientLightColor = Color.White.ToVector3();
                    effect.VertexColorEnabled = true; 

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        device.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.TriangleList, model.verts, 0, model.verts.Length, model.edges, 0, model.edges.Length / 3);
                    }
                }
            } 
        }
    }
}
