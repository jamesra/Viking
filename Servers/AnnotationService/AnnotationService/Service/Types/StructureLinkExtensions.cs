namespace AnnotationService.Types
{
    public static class StructureLinkExtensions
    {
        public static StructureLink Create(this ConnectomeDataModel.StructureLink obj)
        {
            StructureLink sl = new StructureLink();
            ConnectomeDataModel.StructureLink db = obj;

            sl.SourceID = db.SourceID;
            sl.TargetID = db.TargetID;
            sl.Bidirectional = db.Bidirectional;
            sl.Tags = db.Tags;
            sl.Username = db.Username;

            return sl; 
        }

        public static void Sync(this StructureLink sl, ConnectomeDataModel.StructureLink db)
        {
            db.SourceID = sl.SourceID;
            db.TargetID = sl.TargetID;
            db.Bidirectional = sl.Bidirectional;
            db.Tags = sl.Tags;
            db.Username = Annotation.ServiceModelUtil.GetUserForCall();
        }
    }
}
