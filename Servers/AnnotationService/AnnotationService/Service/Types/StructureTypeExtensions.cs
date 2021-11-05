using System.Linq;

namespace AnnotationService.Types
{
    public static class StructureTypeExtensions
    {
        private static PermittedStructureLink[] PopulateLinks(ConnectomeDataModel.StructureType typeObj)
        {
            if (!(typeObj.SourceOfLinks.Any() || typeObj.TargetOfLinks.Any()))
                return null;

            PermittedStructureLink[] _Links = new PermittedStructureLink[typeObj.SourceOfLinks.Count + typeObj.TargetOfLinks.Count];

            int i = 0;
            foreach (ConnectomeDataModel.PermittedStructureLink link in typeObj.SourceOfLinks)
            {
                _Links[i] = link.Create();
                i++;
            }

            foreach (ConnectomeDataModel.PermittedStructureLink link in typeObj.TargetOfLinks)
            {
                _Links[i] = link.Create();
                i++;
            }

            return _Links;
        }
         
        public static StructureType Create(this ConnectomeDataModel.StructureType type)
        {
            StructureType st = new StructureType();
            st.ID = type.ID;
            st.ParentID = type.ParentID;
            st.Name = type.Name.Trim();


            if (type.Notes != null)
                st.Notes = type.Notes.TrimEnd();
            st.MarkupType = type.MarkupType;

            if (type.Tags == null)
                st.Tags = new string[0];
            else
                st.Tags = type.Tags.Split(';');

            if (type.StructureTags == null)
                st.StructureTags = new string[0];
            else
                st.StructureTags = type.StructureTags.Split(';');

            st.Abstract = type.Abstract;
            st.Color = (uint)type.Color;
            st.Code = type.Code;
            st.HotKey = type.HotKey.Length > 0 ? type.HotKey[0] : '\0';
            st.PermittedLinks = PopulateLinks(type);

            return st;
        }

        public static void Sync(this StructureType st, ConnectomeDataModel.StructureType type)
        {

            type.ParentID = st.ParentID;
            type.Name = st.Name;
            type.Notes = st.Notes;
            type.MarkupType = st.MarkupType;
            
            string tags = "";
            foreach (string s in st.Tags)
            {
                tags = s + ';';
            }

            type.Tags = tags;

            string structuretags = "";
            foreach (string s in st.StructureTags)
            {
                tags = structuretags + ';';
            }

            type.StructureTags = structuretags;
            type.Abstract = st.Abstract;
            type.Color = (int)st.Color;
            type.Code = st.Code;
            type.HotKey = st.HotKey.ToString();
            type.Username = Annotation.ServiceModelUtil.GetUserForCall();
        }
    }
}
