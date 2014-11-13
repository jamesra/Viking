using System;
using System.IO;
using System.Text;


static class SVG
{
    public static bool InjectSVGViewer(string SVGPath, string virtualRoot)
    {
        try
        {
            if (System.IO.File.Exists(SVGPath))
            {
                StringBuilder contents = null;
                using (StreamReader sr = new StreamReader(SVGPath))
                {
                    contents = new StringBuilder(sr.ReadToEnd());
                }

                System.IO.File.Delete(SVGPath);

                string searchfor = "http://www.w3.org/1999/xlink\">";
                contents.Replace(searchfor, "http://www.w3.org/1999/xlink\" onload=\"init(evt)\" >");
                
                searchfor = "</svg>";
                contents.Replace(searchfor, "<script xlink:href=\"" + virtualRoot + "/Scripts/SVGzoom.js\"/>\n<script xlink:href=\"" + virtualRoot + "/Scripts/effect.js\"/>\n" + searchfor);

                using (StreamWriter sw = new StreamWriter(SVGPath, false, Encoding.Default, contents.Length))
                {
                    sw.Write(contents.ToString());
                    sw.Close();
                }
            }

        }
        catch (Exception e)
        {
            return false;
        }

        return true; 
    }

}