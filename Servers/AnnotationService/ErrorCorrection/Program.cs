using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Annotation.Database;
using Geometry;

namespace ErrorCorrection
{
    class Program
    {
        static Annotation.Database.AnnotationDataContext db;

        static void Main(string[] args)
        {
            db = new Annotation.Database.AnnotationDataContext("Data Source=MARCLABRETINADA\\SQLEXPRESS;Initial Catalog=Rabbit;Integrated Security=True");
            RepairDuplicateSynapses();
        }

        static void RepairDuplicateSynapses()
        {
            IQueryable<DBStructureType> structTypes = from s in db.DBStructureTypes where s.ParentID != null select s;

            foreach (DBStructureType t in structTypes)
            {
                CheckType(t); 
            }
        }

        static void CheckType(DBStructureType t)
        {
            Console.WriteLine("Check Type: " + t.Name.ToString() + " (" + t.ID.ToString() + ")");

            IQueryable<DBStructure> structs = from s in db.DBStructures where s.TypeID == t.ID select s;

            SortedDictionary<long, DBStructure> DictStruct = new SortedDictionary<long, DBStructure>();
            foreach (DBStructure s in structs)
            {
                DictStruct.Add(s.ID, s);
            }

            foreach (long ID in DictStruct.Keys)
            {
                if (DictStruct.ContainsKey(ID + 1) == false)
                {
                    continue; 
                }

                DBStructure structA = DictStruct[ID];
                DBStructure structB = DictStruct[ID + 1];

                if (structA.ParentID != structB.ParentID)
                    continue;

                CheckOverlap(t, structA, structB);

               
            }
        }

        static void CheckOverlap(DBStructureType t, DBStructure structA, DBStructure structB)
        {
            IQueryable<DBLocation> LocsA = from l in db.DBLocations where l.ParentID == structA.ID select l;
            IQueryable<DBLocation> LocsB = from l in db.DBLocations where l.ParentID == structB.ID select l;

            List<DBLocation> listA = new List<DBLocation>(); 
            List<DBLocation> listB = new List<DBLocation>(); 

            List<GridVector3> pointsA = new List<GridVector3>();
            List<GridVector3> pointsB = new List<GridVector3>();

            //Scale values
            double XYScale = 2.18;
            double ZScale = 90;

            foreach(DBLocation l in LocsA)
            {
                listA.Add(l);
                pointsA.Add( new GridVector3(l.VolumeX * XYScale, l.VolumeY * XYScale, l.Z * ZScale) );
            }

            foreach (DBLocation l in LocsB)
            {
                listB.Add(l);
                pointsB.Add(new GridVector3(l.VolumeX * XYScale, l.VolumeY * XYScale, l.Z * ZScale));
            }

            if (listB.Count == 0 || listA.Count == 0)
            {
                return; 
            }

            //Find the nearest point
            int iA;
            int iB;
            double distance;
            GridVector3.Nearest(pointsA.ToArray(), pointsB.ToArray(), out iA, out iB, out distance); 

            if(distance < listA[iA].Radius + listB[iB].Radius)
            {
                Console.WriteLine("[" + structA.ParentID.ToString() + "] " + listA[iA].ParentID.ToString() + " <-> " + listB[iB].ParentID.ToString() + " distance " + distance.ToString());

                long newParentID = structB.ID; 

                foreach (DBLocation l in LocsA)
                {
                    l.ParentID = newParentID;  
                }

                IQueryable<DBLocationLink> locLinks = from l in db.DBLocationLinks
                                                      where (l.LinkedFrom == listA[iA].ID && l.LinkedTo == listB[iB].ID) ||
                                                            (l.LinkedFrom == listB[iB].ID && l.LinkedTo == listA[iA].ID)
                                                      select l;

                bool ExistingLinkFound = false;
                foreach (DBLocationLink link in locLinks)
                {
                    ExistingLinkFound = true;
                    break;
                }

                if (ExistingLinkFound == false)
                {
                    DBLocationLink link = new DBLocationLink();
                    link.LinkedFrom = listA[iA].ID;
                    link.LinkedTo = listB[iB].ID;
                    link.Username = "Autorepair";

                    db.DBLocationLinks.InsertOnSubmit(link);
                }

                //Update structureLinks
                IQueryable<DBStructureLink> linkFrom = from l in db.DBStructureLinks where l.SourceID == structA.ID select l; 
                foreach(DBStructureLink l in linkFrom)
                {
                    DBStructureLink newLink = new DBStructureLink();
                    newLink.SourceID = newParentID;
                    newLink.TargetID = l.TargetID;
                    newLink.Username = l.Username;
                    newLink.Tags = l.Tags; 
                    newLink.Bidirectional = l.Bidirectional; 
                    
                    db.DBStructureLinks.DeleteOnSubmit(l);

                    DBStructureLink existingLink = (from el in db.DBStructureLinks where el.SourceID == newLink.SourceID && el.TargetID == newLink.TargetID select el).SingleOrDefault<DBStructureLink>();
                    if(existingLink == null)
                        db.DBStructureLinks.InsertOnSubmit(newLink);
                }

                IQueryable<DBStructureLink> linkTo = from l in db.DBStructureLinks where l.TargetID == structA.ID select l; 
                foreach(DBStructureLink l in linkTo)
                {
                    DBStructureLink newLink = new DBStructureLink();
                    newLink.SourceID = l.SourceID;
                    newLink.TargetID = newParentID;
                    newLink.Username = l.Username;
                    newLink.Bidirectional = l.Bidirectional; 

                    newLink.Tags = l.Tags;

                    db.DBStructureLinks.DeleteOnSubmit(l);

                    DBStructureLink existingLink = (from el in db.DBStructureLinks where el.TargetID == newLink.TargetID && el.SourceID == newLink.SourceID select el).SingleOrDefault<DBStructureLink>();
                    if (existingLink == null)
                        db.DBStructureLinks.InsertOnSubmit(newLink);
                }

                db.DBStructures.DeleteOnSubmit(structA);

                db.SubmitChanges(); 
            }

        }
    }
}
