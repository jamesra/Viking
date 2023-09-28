using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;  

namespace VikingXNAGraphics
{
    public class FontRenderData : IInitEffect
    {
        public SpriteFont Font = null;

        public SpriteBatch SpriteBatch = null; 

        /// <summary>
        /// Must be set at program start before we request the default font
        /// </summary>
        public readonly string FontName = @"Arial";

        public FontRenderData(string fontName)
        {
            FontName = fontName;
        }

        public void Init(GraphicsDevice device, ContentManager content)
        {
            SpriteBatch = new SpriteBatch(device);
            SpriteBatch.Name = FontName;
            Font = content.Load<SpriteFont>(FontName);
        }

        public override bool Equals(object obj)
        {
            if(object.ReferenceEquals(obj, this))
                return true; 
            
            if(object.ReferenceEquals(obj, null))
                return false;
            
            FontRenderData other = obj as FontRenderData;
            if (obj == null)
                return false;

            return other.FontName == this.FontName;
        }

        public override int GetHashCode()
        {
            return FontName.GetHashCode();
        }
    }

    public static class DeviceFontStore  
    {
        private static readonly Dictionary<GraphicsDevice, Dictionary<string, FontRenderData>> ManagersForDevice = new Dictionary<GraphicsDevice, Dictionary<string, FontRenderData>>();

        public static string DefaultFont = @"Arial";

        public static FontRenderData GetOrCreateForDevice(GraphicsDevice device, ContentManager content, string FontName=null)
        {
            if (FontName == null)
                FontName = DefaultFont;

            Dictionary<string, FontRenderData> fontDict = null;

            if (ManagersForDevice.TryGetValue(device, out fontDict))
            {
                if(fontDict.TryGetValue(FontName, out FontRenderData result))
                {
                    return result; 
                }
            }
            else
            {
                fontDict = new Dictionary<string, FontRenderData>();
                ManagersForDevice.Add(device, fontDict);
            }

            FontRenderData fontData = new FontRenderData(FontName);
            
            fontData.Init(device, content);

            fontDict.Add(FontName, fontData); 

            //device.DeviceLost += OnDeviceLostOrReset;
            //device.DeviceResetting += OnDeviceLostOrReset;
            return fontData;
        }

        public static FontRenderData TryGet(GraphicsDevice device, string FontName = null)
        {
            if (FontName == null)
                FontName = DefaultFont;

            Dictionary<string, FontRenderData> fontDict = null;
            if (ManagersForDevice.TryGetValue(device, out fontDict))
            {
                if (fontDict.TryGetValue(FontName, out FontRenderData result))
                {
                    return result;
                }
            }

            return null; 
        }
    }
}
