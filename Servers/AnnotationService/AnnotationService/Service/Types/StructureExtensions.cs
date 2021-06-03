using System.Linq;

namespace AnnotationService.Types
{
    public static class StructureExtensions
    {
        private static StructureLink[] PopulateLinks(ConnectomeDataModel.Structure dbStructObj)
        {
            if (!(dbStructObj.SourceOfLinks.Any() || dbStructObj.TargetOfLinks.Any()))
                return null;

            StructureLink[] _Links = new StructureLink[dbStructObj.SourceOfLinks.Count + dbStructObj.TargetOfLinks.Count];

            int i = 0;
            foreach (ConnectomeDataModel.StructureLink link in dbStructObj.SourceOfLinks)
            {
                _Links[i] = link.Create();
                i++;
            }

            foreach (ConnectomeDataModel.StructureLink link in dbStructObj.TargetOfLinks)
            {
                _Links[i] = link.Create();
                i++;
            }

            return _Links;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="IncludeChildren">Include children is set to false to save space when children aren't needed</param>
        public static Structure Create(this ConnectomeDataModel.Structure obj, bool IncludeChildren)
        {
            ConnectomeDataModel.Structure dbStructObj = obj as ConnectomeDataModel.Structure;

            Structure s = new Structure();

            s.ID = dbStructObj.ID;
            s.TypeID = dbStructObj.TypeID;
            s.Notes = dbStructObj.Notes;
            s.Verified = dbStructObj.Verified;


            if (dbStructObj.Tags == null)
            {
                //_Tags = new string[0];
                s.AttributesXml = "";
            }
            else
            {
                //    _Tags = dbStructObj.Tags.Split(';');
                s.AttributesXml = dbStructObj.Tags;
            }


            s.Confidence = dbStructObj.Confidence;
            s.ParentID = dbStructObj.ParentID;

            s.Links = PopulateLinks(dbStructObj);

            if (IncludeChildren)
            {
                s.ChildIDs = dbStructObj.Children.Select(child => child.ID).ToArray();
            }
            else
            {
                s.ChildIDs = null;
            }

            s.Label = dbStructObj.Label;
            s.Username = dbStructObj.Username;

            return s;
        }

        public static void Sync(this Structure s, ConnectomeDataModel.Structure dbStructObj)
        {
            dbStructObj.TypeID = s.TypeID;
            dbStructObj.Notes = s.Notes;
            dbStructObj.Verified = s.Verified;
            /*
            string tags = "";
            foreach (string s in _Tags)
            {
                if (tags.Length > 0)
                    tags = tags + ';' + s;
                else
                    tags = s; 
            }
            */
            dbStructObj.Tags = s.AttributesXml;
            dbStructObj.Confidence = s.Confidence;
            dbStructObj.ParentID = s.ParentID;
            dbStructObj.Label = s.Label;
            dbStructObj.Username = Annotation.ServiceModelUtil.GetUserForCall();
        }
    }
}
