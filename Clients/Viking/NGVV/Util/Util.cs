using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace Viking.Common
{
    public static class Extensions
    {
        

    }

    public class Util
    {
        public static string CoordinatesToURI(double X, double Y, int Z, double Downsample)
        {
            string URI = "http://connectomes.utah.edu/software/viking.application?" +
                             "Volume=" + Viking.UI.State.volume.Host + "&" +
                             "X=" + X.ToString("F1") + "&" +
                             "Y=" + Y.ToString("F1") + "&" +
                             "Z=" + Z.ToString() + "&" +
                             "DS=" + Downsample.ToString("F2");
            return URI; 
        }

        public static string CoordinatesToCopyPaste(double X, double Y, int Z, double Downsample)
        {
            string clip = "X: " + X.ToString("F1") + "\t" +
                          "Y: " + Y.ToString("F1") + "\t" +
                          "Z: " + Z.ToString() + "\t" +
                          "DS: " + Downsample.ToString("F2");
            return clip;
        }

        /// <summary>
        /// Returns a single attribute of type from an object
        /// </summary>
        public static Attribute GetAttribute(System.Type ObjType, System.Type AttribType)
        {
            MemberInfo info = ObjType;
            Attribute[] aAttributes = (Attribute[])info.GetCustomAttributes(AttribType, true);
            Debug.Assert(aAttributes.Length < 2);
            if (aAttributes.Length == 1)
                return aAttributes[0];

            return null;
        }

        
        public static float GetFadeFactor(double ratio, double minCutoff, double maxCuttoff)
        {
            if (ratio < minCutoff)
                return 1f;

            if (ratio > maxCuttoff)
                return 0f;

            return (float)(1.0 - Math.Sqrt((ratio - minCutoff) / (maxCuttoff - minCutoff))); 

        }


        /// <summary>
        /// Rounds the provided downsample level to nearest power of 2
        /// </summary>
        /// <returns></returns>
        public static int NearestPowerOfTwoDownsample(double DownSample)
        {
            DownSample = Math.Log(DownSample, 2);
            DownSample = Math.Floor(DownSample);
            DownSample = Math.Pow(2, DownSample);

            if (DownSample < 1)
                DownSample = 1;
            else if (DownSample > 64)
                DownSample = 64;

            return (int)DownSample;
        }
    }
}
