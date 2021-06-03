using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Configuration;
using VikingXNA;

namespace MonogameTestbed
{
    internal static class TestViewState
    {
        static bool Initialized = false;

        static SortedSet<string> ExistingProperties;

        static void Init()
        {
            if(TestViewState.Initialized)
            {
                return;
            }

            ExistingProperties = new SortedSet<string>();

            foreach(SettingsProperty propname in Properties.Settings.Default.Properties)
            {
                ExistingProperties.Add(propname.Name);
            }

            TestViewState.Initialized = true;
        }

        public static bool RestoreCamera(this Scene scene, TestMode test)
        {
            Init();

            try
            {
                string lookatXkey = test.ToString() + "_lookat_X";
                string lookatYkey = test.ToString() + "_lookat_Y";
                string downsamplekey = test.ToString() + "_downsample";
                var XObj = Properties.Settings.Default.Properties[lookatXkey];
                var YObj = Properties.Settings.Default.Properties[lookatYkey];

                if (XObj == null || YObj == null)
                    return false; 

                float X = (float)XObj.DefaultValue;
                float Y = (float)YObj.DefaultValue;
                scene.Camera.LookAt = new Vector2(X, Y);
                scene.Camera.Downsample = (double)Properties.Settings.Default.Properties[downsamplekey].DefaultValue;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryAddProperty(string name, System.Type type)
        {
            if (ExistingProperties.Contains(name))
                return false;

            LocalFileSettingsProvider localFileSettingsProvider = new LocalFileSettingsProvider();
            
            var existingProperty = Properties.Settings.Default.Properties["Template"];

            SettingsProperty prop = new SettingsProperty(name, type, existingProperty.Provider, false, null, SettingsSerializeAs.Xml, null, true, true);
            Properties.Settings.Default.Properties.Remove(name);
            Properties.Settings.Default.Properties.Add(prop);
            return true;
        }

        public static bool SaveCamera(this Scene scene, TestMode test)
        {
            Init();

            try
            {
                if (scene == null)
                    return false; 

                string lookatXkey = test.ToString() + "_lookat_X";
                string lookatYkey = test.ToString() + "_lookat_Y";
                string downsamplekey = test.ToString() + "_downsample";

                TryAddProperty(lookatXkey, typeof(float));
                TryAddProperty(lookatYkey, typeof(float));
                TryAddProperty(downsamplekey, typeof(double));

                Properties.Settings.Default.Properties[lookatXkey].DefaultValue = scene.Camera.LookAt.X;
                Properties.Settings.Default.Properties[lookatYkey].DefaultValue = scene.Camera.LookAt.Y;
                Properties.Settings.Default.Properties[downsamplekey].DefaultValue = scene.Camera.Downsample;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class CameraTarget2D
    {
        public float X;
        public float Y;
        public float Downsample;

        public void UpdateCamera(Scene scene)
        {
            scene.Camera.LookAt = new Vector2(X, Y);
            scene.Camera.Downsample = this.Downsample;
        }
    }
}
