using System;

namespace WebAnnotation
{
    class Export
    {
        public Uri ExportURL;


        /// <summary>
        /// URL to use for file exports
        /// </summary>
        public Export(Uri URL)
        {
            ExportURL = URL;
        }


        //Fetch morphology
        protected static Uri BuildMorphologyRequest(Uri exportURL, long id)
        {
            string subpath_template = "morphology/tlp?id={0}";
            string subpath = string.Format(subpath_template, id);
            Uri uri_path = new Uri(exportURL, subpath);

            return uri_path;
        }

        public void OpenMorphology(long id)
        {
            Uri uri_path = BuildMorphologyRequest(ExportURL, id);
            System.Diagnostics.Process.Start(uri_path.ToString());
        }

        //Fetch morphology
        protected static Uri BuildNetworkRequest(Uri exportURL, long id, long? hops)
        {
            string subpath;
            if (hops.HasValue)
            {
                string subpath_template = "network/tlp?id={0}&hops={1}";
                subpath = string.Format(subpath_template, id, hops.Value);
            }
            else
            {
                string subpath_template = "network/tlp?id={0}";
                subpath = string.Format(subpath_template, id);
            }

            Uri uri_path = new Uri(exportURL, subpath);

            return uri_path;
        }

        public void OpenNetwork(long id, long? hops)
        {
            Uri uri_path = BuildNetworkRequest(ExportURL, id, hops);
            System.Diagnostics.Process.Start(uri_path.ToString());
        }

        //Fetch network motifs
        protected static Uri BuildMotifRequest(Uri exportURL)
        {
            string subpath_template = "motifs/tlp";
            Uri uri_path = new Uri(exportURL, subpath_template);

            return uri_path;
        }

        public void OpenMotif()
        {
            Uri uri_path = BuildMotifRequest(ExportURL);
            System.Diagnostics.Process.Start(uri_path.ToString());
        }
    }
}
