using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Viking
{

    public static class ViewportExtension
    {
        public static Vector3 UnprojectEx(this Viewport viewport, Vector3 screenSpace, Matrix projection, Matrix view, Matrix world)
        {
            //First, convert raw screen coords to unprojectable ones 
            Vector3 position = new Vector3
            {
                X = (((screenSpace.X - (float)viewport.X) / ((float)viewport.Width)) * 2f) - 1f,
                Y = -((((screenSpace.Y - (float)viewport.Y) / ((float)viewport.Height)) * 2f) - 1f),
                Z = (screenSpace.Z - viewport.MinDepth) / (viewport.MaxDepth - viewport.MinDepth)
            };

            //Unproject by transforming the 4d vector by the inverse of the projecttion matrix, followed by the inverse of the view matrix.   
            Vector4 us4 = new Vector4(position, 1f);
            Vector4 up4 = Vector4.Transform(us4, Matrix.Invert(Matrix.Multiply(Matrix.Multiply(world, view), projection)));
            // Vector4 up4 = Vector4.Transform(us4, Matrix.Invert(Matrix.Multiply(view, projection)));
            Vector3 up3 = new Vector3(up4.X, up4.Y, up4.Z);
            return up3 / up4.W; //better to do this here to reduce precision loss..   
        }
    }
}
